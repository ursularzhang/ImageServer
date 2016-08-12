using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Xml;

namespace ImageService
{
    public class MilestoneTcpViewer
    {
        private List<Frame> _frames = new List<Frame>();

        public bool m_bAvailable
        {
            get { return (_frames.Count > 0); }
        }

        public Frame m_frame
        {
            get 
            {
                if (_frames.Count > 0)
                {
                    Frame frame = _frames.Last();
                    return frame;
                }
                else
                    return null;
            }
        }

        public int width = 0;
        public int height;
        public int stride = 0;
        public int channel;
        
        public bool m_bConnected = false;
        public string m_status = null;
        public bool m_isPTZ = false;

        private SystemInfo _sysInfo = new SystemInfo();
        private ImageServerConnection _isc = null;
        private XmlDocument _sysdoc = null;

        private PlayType _type = PlayType.Live;
        private DateTime _start, _end; //UTC
        private string _camGUID = null;
        private int _camPort = 0;
        private string _token = "Not logged in. No _token.";
        private Thread _thISCRecv = null;
        private Thread _thGetFrame = null;
        private bool _stop = false, _pause = false;
        private string _server = null;
        public string _camName;

        public void ConnectServer(string _serverAddr, string username, string password, string auth)
        {
            SystemInfo.AuthenticationType type = SystemInfo.AuthenticationType.Basic;
            if (auth == "Basic")
                type = SystemInfo.AuthenticationType.Basic;
            else if (auth == "Windows")
                type = SystemInfo.AuthenticationType.Windows;
            else if (auth == "WindowsDefault")
                type = SystemInfo.AuthenticationType.WindowsDefault;

            //Console.WriteLine(_serverAddr + " " + username + " " + password);
            string[] addr = _serverAddr.Split(':');
            _server = addr[0];
            int rc = _sysInfo.Connect(_serverAddr, username, password, type);
            if (rc != 200)
            {
                m_bConnected = false;
                m_status = "_server not found.";
            }
            else
            {
                m_bConnected = true;
                m_status = "Connected.";
                //Console.WriteLine(m_status);
            }
            _token = _sysInfo.Token;
            _sysdoc = _sysInfo.GetSystemInfoXml(_sysInfo.Token);
        }

        public void SetCamera(string camName)
        {
            bool found = false;
            XmlNodeList nodes = _sysdoc.GetElementsByTagName("camera");
            foreach (XmlNode node in nodes)
            {
                string name;
                name = string.Empty;
                foreach (XmlAttribute att in node.Attributes)
                {
                    string s = att.Name.ToLower();
                    if (s == "cameraid")
                    {
                        name = att.InnerText;
                        break;
                    }
                }
                if (!name.Equals(camName))
                    continue;
                found = true;
				_camName = camName;
                foreach (XmlNode subnode in node.ChildNodes)
                {
                    string s = subnode.Name.ToLower();
                    if (s == "guid")
                    {
                        _camGUID = subnode.InnerText;
                    }
                    if (s == "port")
                    {
                        int p;
                        if (int.TryParse(subnode.InnerText, out p))
                            _camPort = p;
                    }
                }
                break;
            }

            if (found)
                m_status = "Camera " + camName + " found.";
            else
                m_status = "No camera found with a matching name.";

            //Console.WriteLine(m_status);
            return;
        }

        public bool gotoPreset(string presetName)
        {
            _sysInfo.GotoPreset(_token, _camGUID, presetName);
            return true;
        }

        public double[] getPTZAbsolute(string cameraName)
        {
            XmlDocument result = new XmlDocument();
            result = _sysInfo.GetPTZAbsolute(_token, _camGUID);
            XmlNodeList nodelist = result.GetElementsByTagName("movement");
            double[] ret = new double[3];
            foreach (XmlNode movement in nodelist)
            {
                string name = movement["name"].InnerText;
                switch (name)
                {
                    case "pan":
                        ret[0] = Convert.ToDouble(movement["value"].InnerText);
                        break;
                    case "tilt":
                        ret[1] = Convert.ToDouble(movement["value"].InnerText);
                        break;
                    case "zoom":
                        ret[2] = Convert.ToDouble(movement["value"].InnerText);
                        break;
                }
            }
            return ret;
        }

        public bool gotoPTZAbsolute(string cameraName, double pan, double tilt, double zoom)
        {
            _sysInfo.GoPTZAbsolute(_token, _camGUID, pan, tilt, 0);
            _sysInfo.GoPTZAbsolute(_token, _camGUID, 0, 0, zoom);
            return true;
        }

        public PlayType GetPlayMode()
        {
            return _type;
        }

        public void SetPlayMode(PlayType type, int frameRate, Format format = Format.JpegMedium, int _width = -1, int _height = -1, string start = "", string end = "")
        {
            int qual = 100;
            switch (format)
            {
                case Format.JpegMaximum:
                    qual = 100;
                    break;
                case Format.JpegHigh:
                    qual = 90;
                    break;
                case Format.JpegMedium:
                    qual = 75;
                    break;
                case Format.JpegLow:
                    qual = 50;
                    break;
            }
            _type = type;
            width = _width;
            height = _height;
            _isc = new ImageServerConnection(_server, _camPort, _camGUID, qual, width, height, _camName);
            _isc.SetCredentials(_sysInfo);
            _isc.OnConnectionStoppedMethod += new ImageServerConnection.OnConnectionStopped(OnLiveConnectionStoppedMethod);
            if (_type == PlayType.Playback)
            {
                IFormatProvider provider = null;
                System.Globalization.DateTimeStyles style = System.Globalization.DateTimeStyles.None;
                DateTime.TryParseExact(start, "yyyyMMddHHmmss.fff", provider, style, out _start);
                DateTime.TryParseExact(end, "yyyyMMddHHmmss.fff", provider, style, out _end);
                _start = _start.ToUniversalTime();
                _end = _end.ToUniversalTime(); //Convert to UTC
            }
        }

        public void OnLiveConnectionStoppedMethod(object p)
        {
            _isc = null;
            _thISCRecv = null;
        }

        public void Start()
        {
            if (_type == PlayType.Live)
            {
                _thISCRecv = new Thread(_isc.Live);
                _thISCRecv.Start();
            }
            else
            {
                double d_Start = LocalTimeToUnix(_start);
                double d_End = LocalTimeToUnix(_end);
                if (_isc._atEnd)
                    _isc._atEnd = false;
                _isc.PlaybackStartTime = d_Start;//_sequenceStartTimes[_sequenceList.SelectedIndex];
                _isc.PlaybackEndTime = d_End;// _sequenceEndTimes[_sequenceList.SelectedIndex]; ;
                _thISCRecv = new Thread(_isc.Playback);
                _thISCRecv.Start();
            }
			_stop = false;

            _thGetFrame = new Thread(new ThreadStart(GetImg));
            _thGetFrame.Start();
            m_status = "Starting...";
        }

        public void Pause()
        {
            _pause = true;
            m_status = "paused";
        }

        public void Resume()
        {
            _pause = false;
            m_status = "resumed";
        }

        public void Stop()
        {
            if (_thISCRecv != null)
                _thISCRecv.Abort();
            if (_type == PlayType.Live)
                _isc.StopLive();
            else
                _isc.StopPlayback();

            _stop = true;
            _thGetFrame.Abort();
            _thGetFrame.Join();

            _frames.Clear();
            m_bConnected = false;
            m_status = "Disconnected.";
        }

        // public byte[] Resize(byte[] input)
        // {
            // MemoryStream orgMS = new MemoryStream(input);
            // Bitmap orgBmp = new Bitmap(orgMS);
            // Bitmap bmp = new Bitmap(orgBmp, new Size(width, height));
            // MemoryStream ms = new MemoryStream();
            // bmp.Save(ms, ImageFormat.Jpeg);
            // return ms.ToArray();
        // }

        private void GetImg()
        {
            DateTime ts = _start;
            DateTime prevTS = DateTime.MinValue;
            while (true)
            {
                if (!_stop && !_pause)
                {
                    if (_frames.Count > 0)
                        m_status = "Playing...";
                    if ((_isc.img != null) && (_isc.img.Data != null))
                    {
                        DateTime frameTS = TimeConverter.FromString(_isc.img.Current);
                        if (frameTS.Subtract(prevTS).TotalMilliseconds <= 0.0)
                            continue;
                        byte[] curr_img = (byte[])_isc.img.Data;
                        Frame currFrame = new Frame();
                        currFrame.viewImg = new byte[curr_img.Length];
                        currFrame.ts = frameTS;
                        if (_type == PlayType.Playback)
                            currFrame.nextTS = TimeConverter.FromString(_isc.img.Next);
                        Buffer.BlockCopy(curr_img, 0, currFrame.viewImg, 0, curr_img.Length);
						prevTS = currFrame.ts;
                        _frames.Add(currFrame);
                        if (_frames.Count >= 3)
                            _frames.RemoveAt(0);

                        GC.Collect();
                    }
                    //Thread.Sleep(50);
                }
                else if (_pause)
                {
                    Thread.Sleep(80);
                }
            }
        }

        private double LocalTimeToUnix(DateTime local)
        {
            DateTime epoch = DateTime.Parse("1970/01/01 00:00:00 AM");
            DateTime utc = local; //already pass in the utc time
            TimeSpan unix = utc.Subtract(epoch);
            return (unix.Ticks / 10000);
        }
    }
 
}
