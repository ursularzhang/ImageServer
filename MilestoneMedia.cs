using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Net;
using System.IO;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Xml;
using System.Diagnostics;

using VideoOS.Platform;
using VideoOS.Platform.SDK.Platform;
using VideoOS.Platform.Live;
using VideoOS.Platform.Data;
using VideoOS.Platform.Messaging;

using System.Runtime.ExceptionServices;


namespace ImageService
{
    public class MilestoneMediaServer
    {
        static public bool m_bConnected = false;
        public string m_status = null;

        public void StopServer()
        {
            VideoOS.Platform.SDK.Environment.Logout();
            m_status = "Server disconnected.";
        }

        public void ConnectServer(string serverAddr, string username, string password, string type)
        {
            VideoOS.Platform.SDK.Environment.Initialize();		// Initialize the standalone Environment
            VideoOS.Platform.SDK.Media.Environment.Initialize();		// Initialize the standalone Environment

            serverAddr = "http://" + serverAddr;
            Uri uri = new Uri(serverAddr);

            switch (type)
            {
                case "Basic":
                    CredentialCache cc = VideoOS.Platform.Login.Util.BuildCredentialCache(uri, username, password, "Basic");
                    VideoOS.Platform.SDK.Environment.AddServer(uri, cc);
                    break;
                case "Windows":
                    NetworkCredential nc = new NetworkCredential("me", "mypassword");
                    VideoOS.Platform.SDK.Environment.AddServer(uri, nc);
                    break;
                case "WindowsDefault":
                    NetworkCredential nc1 = System.Net.CredentialCache.DefaultNetworkCredentials;
                    VideoOS.Platform.SDK.Environment.AddServer(uri, nc1);
                    break;
            }

            try
            {
                VideoOS.Platform.SDK.Environment.Login(uri);
            }
            catch (ServerNotFoundMIPException snfe)
            {
                //System.Diagnostics.Trace.WriteLine("Server not found: " + snfe.Message);
                m_status = "Server not found: " + snfe.Message;
                return;
            }
            catch (InvalidCredentialsMIPException ice)
            {
                //System.Diagnostics.Trace.WriteLine("Invalid credentials for: " + ice.Message);
                m_status = "Invalid credentials for: " + ice.Message;
                return;
            }
            catch (Exception)
            {
                //System.Diagnostics.Trace.WriteLine("Internal error connecting to: " + uri.DnsSafeHoste);
                m_status = "Unknown error.";
                return;
            }

            m_bConnected = true;
            m_status = "Server connected.";
        }
    }

    public class MilestoneMedia : ImageServer
    {
        #region implementation of interface functions

        MilestoneMediaServer server = new MilestoneMediaServer();

        public void ConnectServer(string str)
        {
            string[] paras = str.Split(';');
            if (!MilestoneMediaServer.m_bConnected)
                server.ConnectServer(paras[0], paras[1], paras[2], paras[3]);
            SetCamera(paras[4]);
            if (paras[8] == "")
                SetPlayMode(PlayType.Live, Convert.ToInt16(paras[5]), Format.JpegMedium, 
                    Convert.ToInt16(paras[6]), Convert.ToInt16(paras[7]), "", "");
            else
                SetPlayMode(PlayType.Playback, Convert.ToInt16(paras[5]), Format.JpegMedium,
                    Convert.ToInt16(paras[6]), Convert.ToInt16(paras[7]), paras[8], paras[9]);
        }

        public void SetPlaybackSpeed(double speed)
        {
            _speed = speed;
        }

        public void SetLoop(bool toLoop)
        {
            _loop = toLoop;
        }

        public void StartStream()
        {
            _thGetFrame = new Thread(new ThreadStart(GetFrameThread));
            _thGetFrame.Start();

            m_status = "Starting...";
        }

        public void PauseStream()
        {
            _pause = true;
            m_status = "paused";
        }

        public void ResumeStream()
        {
            _pause = false;
            m_status = "resumed";
        }

        public void StopStream()
        {
            _stop = true;
            Thread.Sleep(1000);
            _thGetFrame.Abort();
            _thGetFrame.Join();
            if (_type == PlayType.Live)
            {
                _jpegLiveSource.Close();
                _jpegLiveSource.LiveContentEvent -= OnJpegLiveSourceLiveNotificationEvent;
            }
            else if (_type == PlayType.Playback)
            {
                _jpegVideoSource.Close();
            }
            _frames.Clear();
            GC.Collect();
            m_status = "Disconnected.";
        }

        bool ImageServer.isAvailable()
        {
            return m_bAvailable;
        }

        Frame ImageServer.getFirstFrame()
        {
            return m_firstFrame;
        }

        Frame ImageServer.getLastFrame()
        {
            return m_lastFrame;
        }

        void ImageServer.getFrame(DateTime ts)
        {
            GetFrame(ts.ToString("yyyyMMddHHmmss.fff"), 0);
        }

        void ImageServer.clearBuffer()
        {
            int count = _frames.Count;
            for (int i = 0; i < count - 1; i++)
            {
                _frames.RemoveAt(i);
            }
        }

        void ImageServer.removeFrame()
        {
            _frames.RemoveAt(0);
        }

        int ImageServer.width()
        {
            return width;
        }

        int ImageServer.height()
        {
            return height;
        }

        int ImageServer.channel()
        {
            return channel;
        }

        string ImageServer.status()
        {
            return m_status;
        }
        #endregion

        public bool m_bAvailable
        {
            get { return (_frames.Count > 0); }
        }
        public Frame m_firstFrame
        {
            get {
                if (_frames.Count > 0)
                    return _frames.First();
                else
                    return null;
            }
        }
        public Frame m_lastFrame
        {
            get
            {
                if (_frames.Count > 0)
                    return _frames.Last();
                else
                    return null;
            }
        }


        public bool m_isPTZ = false;
        public int width = 0;
        public int height;
        public int stride;
        public int channel;
        public string m_status = null;

        //private Queue<Frame> _frames = new Queue<Frame>();
        //private LinkedList<Frame> _frames = new LinkedList<Frame>();
        private List<Frame> _frames = new List<Frame>();
        private JPEGLiveSource _jpegLiveSource = null;
        private JPEGVideoSource _jpegVideoSource = null;
        private Item _camera;
        private PlayType _type;
        private DateTime _start = DateTime.MinValue, _end = DateTime.MinValue; //UTC
        private Thread _thGetFrame;
        private bool _stop = false, _pause = false;
        private bool _loop = true;
        private double _speed = 1.0;
        private int _frameRate = 12;

        private DateTime _curr; //UTC; Added by ZJ 18Nov2015 in order or the playback to be controlable by user dragging a timeline bar...
        private bool _setCurrTime = false;

        #region Config Camera
        private Item CheckChildren(VideoOS.Platform.Item parent, string name, string kind)
        {
            Item _ret = null;
            List<VideoOS.Platform.Item> itemsOnNextLevel = parent.GetChildren(); // This causes the configuration Items to be loaded.
            if (itemsOnNextLevel != null)
            {
                foreach (VideoOS.Platform.Item item in itemsOnNextLevel)
                {
                    // If we find the camera we want, remember it and return with no further checks
                    // It must have Kind == Camera and it must not be a folder (It seems that camera folders have Kind == Camera)
                    if (kind == "camera")
                    {
                        if (item.FQID.Kind == VideoOS.Platform.Kind.Camera && item.FQID.FolderType == VideoOS.Platform.FolderType.No)
                        {
                            // Does the name match the camera name we are looking for? Here we accept a non-perfect match
                            if (item.Name.Equals(name))
                            {
                                // Remember this camera and stop checking.
                                _ret = item;
                                break;
                            }
                        }
                        else
                        {
                            // We have not found our camera, so check the next level of Items in case this Item has children.
                            if (item.HasChildren != VideoOS.Platform.HasChildren.No)
                                _ret = CheckChildren(item, name, kind);
                            if (_ret != null)
                                break;
                        }
                    }
                    else if (kind == "preset")
                    {
                        if (item.FQID.Kind == VideoOS.Platform.Kind.Preset)
                        {
                            // Does the name match the camera name we are looking for? Here we accept a non-perfect match
                            if (item.Name.Equals(name))
                            {
                                // Remember this camera and stop checking.
                                _ret = item;
                                break;
                            }
                        }
                        else
                        {
                            // We have not found our camera, so check the next level of Items in case this Item has children.
                            if (item.HasChildren != VideoOS.Platform.HasChildren.No)
                                _ret = CheckChildren(item, name, kind);
                            if (_ret != null)
                                break;
                        }
                    }

                }
            }
            return _ret;
        }

        public void SetCamera(string cameraName)
        {
            _camera = null;
            List<VideoOS.Platform.Item> list = new List<VideoOS.Platform.Item>();

            // Get the root level Items, which are the servers added. Configuration is not loaded implicitly yet.
            list = VideoOS.Platform.Configuration.Instance.GetItems();

            // For each root level Item, check the children. We are certain, none of the root level Items is a camera
            foreach (VideoOS.Platform.Item item in list)
            {
                _camera = CheckChildren(item, cameraName, "camera");
            }

            if (_camera == null)
                m_status = "No camera found with a matching name";
            else
            {
                m_status = "Camera " + cameraName + " found.";
                if (_camera.Properties != null)
                {
                    foreach (string key in _camera.Properties.Keys)
                    {
                        if (key == "PTZ" && _camera.Properties[key] == "Yes")
                        {
                            m_isPTZ = true;
                            break;
                        }
                    }
                }
            }
        }

        public void SetPlayMode(PlayType type, int frameRate, Format format = Format.JpegMedium, int width = 0, int height = 0, string start = "", string end = "")
        {
            _type = type;
            if (_type == PlayType.Live)
            {
                if (_jpegLiveSource != null)
                {
                    _jpegLiveSource.Close();
                    _jpegLiveSource = null;
                }
                _jpegLiveSource = new JPEGLiveSource(_camera);
                _jpegLiveSource.Width = width;
                _jpegLiveSource.Height = height;
                _jpegLiveSource.SetWidthHeight();
                _jpegLiveSource.LiveModeStart = true;
                _jpegLiveSource.SetKeepAspectRatio(false, false);
                //_jpegLiveSource.FPS = frameRate;
                _jpegLiveSource.Init();
                _jpegLiveSource.LiveContentEvent += new EventHandler(OnJpegLiveSourceLiveNotificationEvent);
            }
            else if (_type == PlayType.Playback)
            {
                if (_jpegVideoSource != null)
                {
                    _jpegVideoSource.Close();
                    _jpegVideoSource = null;
                }
                _jpegVideoSource = new JPEGVideoSource(_camera);
                _jpegVideoSource.Width = width;
                _jpegVideoSource.Height = height;
                _jpegVideoSource.SetWidthHeight();
                _jpegVideoSource.SetKeepAspectRatio(false, false);
                switch (format)
                {
                    case Format.JpegMaximum:
                        _jpegVideoSource.Compression = 100;
                        break;
                    case Format.JpegHigh:
                        _jpegVideoSource.Compression = 90;
                        break;
                    case Format.JpegMedium:
                        _jpegVideoSource.Compression = 75;
                        break;
                    case Format.JpegLow:
                        _jpegVideoSource.Compression = 50;
                        break;
                }
                _jpegVideoSource.Init();
                _frameRate = frameRate;
                IFormatProvider provider = null;
                System.Globalization.DateTimeStyles style = System.Globalization.DateTimeStyles.None;
                DateTime.TryParseExact(start, "yyyyMMddHHmmss.fff", provider, style, out _start);
                DateTime.TryParseExact(end, "yyyyMMddHHmmss.fff", provider, style, out _end);
                _start = _start.ToUniversalTime();
                _end = _end.ToUniversalTime(); //Convert to UTC
            }
        }


        #endregion

        #region Control Camera


        //get one specific frame at (ts + delaySec)
        public void GetFrame(string ts, int delaySec)
        {
            DateTime frameTS = DateTime.MinValue;
            IFormatProvider provider = null;
            System.Globalization.DateTimeStyles style = System.Globalization.DateTimeStyles.None;
            DateTime.TryParseExact(ts, "yyyyMMddHHmmss.fff", provider, style, out frameTS);
            frameTS = frameTS.AddSeconds(delaySec);
            JPEGData jpegData;
            //var stopwatch = new Stopwatch();
            //stopwatch.Start();
            jpegData = _jpegVideoSource.GetAtOrBefore(frameTS.ToUniversalTime()) as JPEGData;
            //stopwatch.Stop();
            //Console.WriteLine("Milestone get image:" + stopwatch.ElapsedMilliseconds);
            if (jpegData != null)
            {
                MemoryStream ms = new MemoryStream(jpegData.Bytes);
                Bitmap newBitmap = new Bitmap(ms);
                //System.Drawing.Imaging.BitmapData bmpData = newBitmap.LockBits(new Rectangle(0, 0, newBitmap.Width, newBitmap.Height),
                //    System.Drawing.Imaging.ImageLockMode.ReadOnly, newBitmap.PixelFormat);
                if (width == 0)
                {
                    width = newBitmap.Width; height = newBitmap.Height; stride = newBitmap.Width*3;
                    channel = stride / width;
                } 
                Frame currFrame = new Frame(width, height, stride);
                currFrame.ts = jpegData.DateTime.ToLocalTime();
                currFrame.nextTS = jpegData.NextDateTime.ToLocalTime();
                //Marshal.Copy(bmpData.Scan0, currFrame.img, 0, height * stride);
                currFrame.bitmap = newBitmap;
                _frames.Clear();
                _frames.Add(currFrame);

                //string filename = ".\\" + _camera.Name + "\\" + currFrame.ts.ToString("yyyyMMddHHmmss.fff") + ".jpg";
                //newBitmap.Save(filename);
                //newBitmap.UnlockBits(bmpData);
                ms.Close();
                ms.Dispose();
            }
        }

        private void GetFrameThread()
        {
         
            DateTime ts = _start, prevTS = DateTime.MinValue;
            int interval =  (int)(1000.0 / _frameRate);
            while (true)
            {
                try
                {

                    if (!_stop && !_pause)
                    {
                        if (_type == PlayType.Live)
                        {
                            if (_frames.Count > 0)
                                m_status = "Playing...";

                            Thread.Sleep(100);
                        }
                        else if (_type == PlayType.Playback)
                        {
                            if (_setCurrTime)
                            {
                                ts = _curr;
                                _setCurrTime = false;
                            }

                            DateTime t0 = DateTime.Now;
                            JPEGData jpegData;
                            jpegData = _jpegVideoSource.GetAtOrBefore(ts) as JPEGData;
                            if (jpegData != null)
                            {
                                m_status = "Playing...";

                                MemoryStream ms = new MemoryStream(jpegData.Bytes);
                                Bitmap newBitmap = new Bitmap(ms);
                                if (width == 0)
                                {
                                    width = newBitmap.Width; height = newBitmap.Height; stride = newBitmap.Width * 3;
                                    channel = stride / width;
                                }
                                Frame currFrame = new Frame(width, height, stride);
                                currFrame.ts = jpegData.DateTime.ToLocalTime();
                                currFrame.nextTS = jpegData.NextDateTime.ToLocalTime();
                                currFrame.bitmap = newBitmap;
                                if (ts.Subtract(prevTS).TotalMilliseconds > 0)
                                {
                                    _frames.Add(currFrame);
                                    prevTS = ts;
                                }

                                if (_frames.Count > 10)
                                {
                                    _frames[0].bitmap.Dispose();
                                    _frames.RemoveAt(0);
                                }

                                //string filename = ".\\bufferLog_" + _camera.Name + ".txt";
                                //using (System.IO.StreamWriter file = new System.IO.StreamWriter(filename, true))
                                //{
                                //    file.WriteLine(_frames.Count + " " + currFrame.ts.ToString("yyyyMMddHHmmss.fff") + " " + DateTime.Now.ToString("yyyyMMddHHmmss.fff"));
                                //}

                                ms.Close();
                                ms.Dispose();
                            }
                            ts = ts.AddMilliseconds(interval);
                            if (ts > _end)
                            {
                                if (!_loop)
                                    _stop = true;
                                else
                                    ts = _start;
                            }
                            //Thread.Sleep(40);
                            double taken = DateTime.Now.Subtract(t0).TotalMilliseconds;
                            if (interval > (int)taken)
                            {
                                Thread.Sleep((int)(interval / _speed - taken));
                            }

                        }
                    }
                    else if (_pause)
                    {
                        Thread.Sleep(200);
                    }
                    else if (_stop)
                        break;
                }
                catch (Exception e) { }
            }
        }

        public void setCurrTS(DateTime ts)
        {
            if (_type == PlayType.Playback)
            {
                _curr = ts.ToUniversalTime();
                _setCurrTime = true;
            }
        }

        /// <summary>
        /// This event is called when JPEG is available or some exception has occurred
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        DateTime prevTS = DateTime.MinValue;
        void OnJpegLiveSourceLiveNotificationEvent(object sender, EventArgs e)
        {
            LiveContentEventArgs args = e as LiveContentEventArgs;
            if (args != null)
            {
                if (args.LiveContent != null && !_pause)
                {
                    if (args.LiveContent.BeginTime.ToLocalTime().Subtract(prevTS).TotalMilliseconds <= 0)
                    {
                        Thread.Sleep(5);
                        args.LiveContent.Dispose(true); 
                        return;
                    }

                    _jpegLiveSource.Width = args.LiveContent.Width;
                    _jpegLiveSource.Height = args.LiveContent.Height;

                    MemoryStream ms = new MemoryStream(args.LiveContent.Content);
                    Bitmap newBitmap = new Bitmap(ms);
                    //System.Drawing.Imaging.BitmapData bmpData = newBitmap.LockBits(new Rectangle(0, 0, newBitmap.Width, newBitmap.Height),
                    //    System.Drawing.Imaging.ImageLockMode.ReadOnly, newBitmap.PixelFormat);
                    if (width == 0)
                    {
                        width = newBitmap.Width; height = newBitmap.Height; stride = newBitmap.Width * 3;
                        channel = stride / width;
                    }

                    Frame currFrame = new Frame(width, height, stride);
                    currFrame.ts = args.LiveContent.BeginTime.ToLocalTime();
                    currFrame.bitmap = newBitmap;
                    //Marshal.Copy(bmpData.Scan0, currFrame.img, 0, height * stride);
                        
                    _frames.Add(currFrame);

                    //string filename = ".\\bufferLog_" + _camera.Name + ".txt";
                    //using (System.IO.StreamWriter file = new System.IO.StreamWriter(filename, true))
                    //{
                    //    file.WriteLine(_frames.Count + " " + currFrame.ts.ToString("yyyyMMddHHmmss.fff") + " " + currFrame.ts.Subtract(prevTS).TotalMilliseconds + " " + DateTime.Now.ToString("yyyyMMddHHmmss.fff"));
                    //}
                    prevTS = currFrame.ts;

                    if (_frames.Count > 10)
                    {
                        _frames[0].bitmap.Dispose();
                        _frames.RemoveAt(0);
                    }

                    //newBitmap.UnlockBits(bmpData);
                    ms.Close();
                    ms.Dispose();

                    args.LiveContent.Dispose(true);
                }
                else if (args.Exception != null)
                {
                    // Handle any exceptions occurred inside toolkit or on the communication to the VMS

                    //EnvironmentManager.Instance.ExceptionDialog("JpegLiveSourceLiveNotificationEvent", args.Exception);
                }

            }
        }

        public void MoveRelative(double[] ptz)
        {
            double[] ori_ptz = new double[3];
            double[] new_ptz = new double[3];
            ori_ptz = GetAbsolute();
            for (int i = 0; i < 3; i++)
                new_ptz[i] = ori_ptz[i] + ptz[i];
            MoveAbsolute(new_ptz);
        }

        public double[] GetAbsolute()
        {
            double[] ret = new double[3];
            System.Collections.ObjectModel.Collection<object> objResult = EnvironmentManager.Instance.SendMessage(
            new VideoOS.Platform.Messaging.Message(MessageId.Control.PTZGetAbsoluteRequest), _camera.FQID);

            PTZGetAbsoluteRequestData datRequestData = (PTZGetAbsoluteRequestData)objResult[0];
            ret[0] = datRequestData.Pan;
            ret[1] = datRequestData.Tilt;
            ret[2] = datRequestData.Zoom;
            objResult.Clear();
            return ret;
        }

        public void MoveAbsolute(double[] ptz)
        {
            PTZMoveAbsoluteCommandData datMoveAbsolute = new PTZMoveAbsoluteCommandData();
            datMoveAbsolute.Pan = ptz[0];
            datMoveAbsolute.Tilt = ptz[1];
            datMoveAbsolute.Zoom = ptz[2];

            VideoOS.Platform.Messaging.Message msg = new VideoOS.Platform.Messaging.Message(MessageId.Control.PTZMoveAbsoluteCommand, datMoveAbsolute);
            EnvironmentManager.Instance.SendMessage(msg, _camera.FQID);
        }

        public void GotoPreset(string presetName)
        {
            VideoOS.Platform.Item preset = FindPreset(presetName);
            if (preset != null)
            {
                // This constucts a "trigger" message
                VideoOS.Platform.Messaging.Message triggerMessage =
                    new VideoOS.Platform.Messaging.Message(MessageId.Control.TriggerCommand);

                // This sends the trigger message to the preset, eventually causing the camera to actually move.
                EnvironmentManager.Instance.SendMessage(triggerMessage, preset.FQID);
            }
        }

        private Item FindPreset(string presetName)
        {
            Item _ret = null;
            _ret = CheckChildren(_camera, presetName, "preset");

            if (_ret == null)
                System.Diagnostics.Trace.WriteLine("No preset with a matching name");
            else
                System.Diagnostics.Trace.WriteLine(string.Format("Preset: {0}", presetName));

            return _ret;
        }

        #endregion
    }

}
