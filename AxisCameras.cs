using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Threading;
using System.Drawing;
using System.Net;
using System.IO;
using System.Runtime.InteropServices;

namespace ImageService
{

    public class AxisCamera : ImageServer
    {
        #region implementation of interface members
        public void ConnectServer(string str)
        {
            string[] paras = str.Split(';');
            SetAddress(paras[0], paras[1], paras[2]);
            SetParam(Convert.ToInt16(paras[6]), Convert.ToInt16(paras[7]), Convert.ToInt16(paras[5]), Convert.ToInt16(paras[10]), paras[11]);
        }

        public void SetPlaybackSpeed(double speed)
        {
        }

        public void SetLoop(bool toLoop)
        {
        }

        public void StartStream()
        {
            if (fps > 0)
            {
                _getImg = new Thread(new ThreadStart(GetImg));
                _getImg.Start();
                _stop = false;
                m_status = "Starting...";
            }
            else
                m_status = "Error. Please check frame rate setting.";
        }

        public void PauseStream()
        {
            _stop = true;
            m_status = "Stoped.";
        }

        public void ResumeStream()
        {
            _stop = false;
            m_status = "Resumed";
        }

        public void StopStream()
        {
            frameBuffer.Clear();
            _stop = true;
            Thread.Sleep(1000);
            _getImg.Abort();
            _getImg.Join();
        }

        void ImageServer.getFrame(DateTime ts)
        {
        }
        
        void ImageServer.clearBuffer()
        {
        }

        void ImageServer.removeFrame()
        {
        }

        bool ImageServer.isAvailable()
        {
            return m_bAvailable;
        }

        Frame ImageServer.getFirstFrame()
        {
            return m_frame;
        }
        Frame ImageServer.getLastFrame()
        {
            return m_frame;
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

        public int width = 0, height = 0, stride = 0, channel = 0;
        public int fps = 0;
        public int compression = 0;
        public bool m_bAvailable { get { return frameBuffer.Count > 0; } }
        public Frame m_frame { get { return frameBuffer.Peek(); } }
        public byte[] img;
        public string streamIdx;
        public string m_status = null;

        private string _addr;
        private string _user;
        private string _pw;
        private bool _stop = true;
        private Queue<Frame> frameBuffer = new Queue<Frame>(3);
        private Thread _getImg;

        public void SetAddress(string IPAddr, string username, string pw)
        {
            _addr = IPAddr;
            _user = username;
            _pw = pw;
        }

        public void SetParam(int w, int h, int f, int c, string idx)
        {
            width = w;
            height = h;
            fps = f;
            compression = c;
            streamIdx = idx;
        }


        private void GetAxisFrame()
        {
            try
            {
                //DateTime start = DateTime.Now;

                string sourceURL = "http://" + _addr + "/axis-cgi/jpg/image.cgi?resolution=" + width + "x" + height + "&des_fps=" + fps + "&compression=" + compression + "&camera=" + streamIdx;
                byte[] buffer = new byte[width * height * 3];
                
                int read, total = 0;
                // create HTTP request
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(sourceURL);
                // set login and password
                req.Credentials = new NetworkCredential(_user, _pw);
                // get response
                WebResponse resp = req.GetResponse();
                // get response stream
                Stream stream = resp.GetResponseStream();
                // read data from stream
                while ((read = stream.Read(buffer, total, 1000)) != 0)
                {
                    total += read;
                }
                // get bitmap
                Bitmap frm = (Bitmap)Bitmap.FromStream(new MemoryStream(buffer, 0, total));
                resp.Close();

                if (stride == 0)
                {                
                    System.Drawing.Imaging.BitmapData bmpData = frm.LockBits(new Rectangle(0, 0, frm.Width, frm.Height),
                        System.Drawing.Imaging.ImageLockMode.ReadOnly, frm.PixelFormat);
                    width = bmpData.Width; height = bmpData.Height; stride = bmpData.Stride;
                    channel = stride / width;
                    frm.UnlockBits(bmpData);
                }
                Frame currFrame = new Frame(width, height, stride);
                currFrame.ts = DateTime.Now;
                currFrame.bitmap = frm;
                //Marshal.Copy(bmpData.Scan0, currFrame.img, 0, height * stride);

                frameBuffer.Enqueue(currFrame);
                if (frameBuffer.Count >= 3)
                    frameBuffer.Dequeue();

                //Console.WriteLine(DateTime.Now.Subtract(start).TotalMilliseconds);
            }
            catch (WebException e)
            {
                Console.WriteLine("image source time out. " + e.Message);
                return;
            }
        }

        private void GetImg()
        {
            int sleepTime = (int)(1000 / fps);
            while (true)
            {
                if (!_stop)
                {
                    DateTime prev = DateTime.Now;
                    GetAxisFrame();
                    //Console.WriteLine(DateTime.Now.Subtract(prev).TotalMilliseconds);
                    //Thread.Sleep(sleepTime);
                }
            }
        }

    }
}
