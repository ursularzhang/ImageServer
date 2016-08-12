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
    public enum Brands { Axis };

    public class IPCameras
    {
        public int width = 0, height = 0;
        public int fps = 0;
        public int compression = 0;
        public bool available { get { return frameBuffer.Count > 0; } }
        public Bitmap frame { get { return frameBuffer.Peek(); } }
        public byte[] img;
        public string streamIdx;

        private string _addr;
        private string _user;
        private string _pw;
        private bool _stop = true;
        private Queue<Bitmap> frameBuffer = new Queue<Bitmap>(3);
        private Brands _brand;
        private Thread _getImg;

        public IPCameras(string brand, string IPAddr, string username, string pw)
        {
            switch (brand.ToLower())
            {
                case "axis":
                    _brand = Brands.Axis;
                    break;
                default:
                    _brand = Brands.Axis;
                    break;
            }
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

        public int StartStream()
        {
            if (fps > 0)
            {
                _getImg = new Thread(new ThreadStart(GetImg));
                _getImg.Start();
                _stop = false;
                return 0;
            }
            else
                return -1;
        }

        public void StopStream()
        {
            frameBuffer.Clear();
            _stop = true;
            Thread.Sleep(1000);
            _getImg.Abort();
            _getImg.Join();
        }

        private void GetAxisFrame()
        {
            try
            {
                DateTime start = DateTime.Now;

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

                frameBuffer.Enqueue(frm);
                if (frameBuffer.Count >= 3)
                    frameBuffer.Dequeue();
                Console.WriteLine(DateTime.Now.Subtract(start).TotalMilliseconds);
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
                    switch (_brand)
                    {
                        case Brands.Axis:
                            DateTime prev = DateTime.Now;
                            GetAxisFrame();
                            //Console.WriteLine(DateTime.Now.Subtract(prev).TotalMilliseconds);
                            break;
                        default:
                            break;
                    }
                    //Thread.Sleep(sleepTime);
                }
            }
        }

    }
}
