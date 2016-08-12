using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.InteropServices;
using System.Collections;
using System.IO;
using System.Xml;

using System.Windows.Controls; // Limitation: This code is hardcoded to WPF style callbacks.

namespace ImageService
{
    class ImageServerConnection
    {
        public delegate void OnImageReceived(object p);
        public delegate void OnConnectionStopped(object p);
        public delegate void OnPresetsReceived(object p);
        public delegate void OnStatusItemReceived(object p);

        private OnImageReceived _onImagereceived = null;
        private OnConnectionStopped _onConnectionStopped = null;
        private OnPresetsReceived _onPresetsReceived = null;
        private OnStatusItemReceived _onStatusItemReceived = null;

        public ImageInfo img;
        public bool _atEnd = false;

        private string _camName;
        private object _renderObject;
        private bool _live = false;
        private bool _playback = false;
        private double _playbackStartTime;
        private double _playbackEndTime;
        private double _playbackTime;
        private Socket _sockLive = null;
        private int _reqCounter = 0;
        private string _token = "";
        private string _user = "";
        private string _pwd = "";
        private string _cameraGuid = "";
        private string _imageServer = "";
        private int _imagePort = 7563;
        private int _quality = 100;
        private byte[] _lastJpeg = null;
        private int _speed = 1;
        private int _width, _height;
        private object _liveSocketSendLock = new object();
        private bool _playbackSendConnectUpdateFlag = false;

        public byte[] LastJPEG
        {
            get
            {
                return _lastJpeg;
            }
        }

#if DEBUG
        public ImageServerConnection() // For testing on static camera
        {
            _imageServer = "192.168.235.131";
            _imagePort = 7563;
            _cameraGuid = "a9adc052-e793-4ed0-a1e2-8b975ba8e020";
            _token = "";
        }
#endif

        public ImageServerConnection(string imageServer, int imagePort, string cameraGuid, int qual, int width, int height, string camName)
        {
            _imageServer = imageServer;
            _imagePort = imagePort;
            _cameraGuid = cameraGuid;
            _quality = qual;
            _width = width;
            _height = height;
            _camName = camName;
        }

        public void SetBasicCredentials(string user, string pwd)
        {
            _token = "BASIC";
            _user = user;
            _pwd = pwd;
        }

        public void SetTokenCredentials(string token)
        {
            _token = token;
            _user = "#";
            _pwd = "#";
        }

        public void SetToken(string token)
        {
            _token = token;
        }

        public void SetCredentials(SystemInfo sysInfo)
        {
            if (sysInfo.Token != "BASIC")
                SetTokenCredentials(sysInfo.Token);
            else
                SetBasicCredentials(sysInfo.User, sysInfo.Password);
        }

        public OnStatusItemReceived OnStatusItemReceivedMethod
        {
            set
            {
                _onStatusItemReceived = value;
            }
            get
            {
                return _onStatusItemReceived;
            }
        }

        public OnImageReceived OnImageReceivedMethod
        {
            set
            {
                _onImagereceived = value;
            }
            get
            {
                return _onImagereceived;
            }
        }

        public OnConnectionStopped OnConnectionStoppedMethod
        {
            set
            {
                _onConnectionStopped = value;
            }
            get
            {
                return _onConnectionStopped;
            }
        }

        public OnPresetsReceived OnPresetsReceivedMethod
        {
            set
            {
                _onPresetsReceived = value;
            }
            get
            {
                return _onPresetsReceived;
            }
        }

        public object RenderObject
        {
            set
            {
                _renderObject = value;
            }
        }

        public bool PlaybackSendConnectUpdateFlag
        {
            set
            {
                _playbackSendConnectUpdateFlag = value;
            }
        }

        public double PlaybackStartTime
        {
            set
            {
                _playbackStartTime = value;
            }
            get
            {
                return _playbackStartTime;
            }
        }

        public double PlaybackEndTime
        {
            set
            {
                _playbackEndTime = value;
            }
            get
            {
                return _playbackEndTime;
            }
        }

        public void StopLive()
        {
            _live = false;
            _sockLive = null;
        }

        public void StopPlayback()
        {
            _playback = false;
        }
        
        public string FormatConnectUpdate()
        {
            string sendBuffer = string.Format(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><methodcall><requestid>{0}</requestid>" +
                "<methodname>connectupdate</methodname>" +
                "<connectparam>id={1}&amp;connectiontoken={2}</connectparam>" +
                "</methodcall>\r\n\r\n",
                ++_reqCounter, _cameraGuid, _token);

            return sendBuffer;
        }

        private string FormatLive()
        {
            string sendBuffer;

            if (_quality == 100 || _quality < 1 || _quality > 104)
            {
                sendBuffer = string.Format(
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?><methodcall><requestid>1</requestid>" +
                    "<methodname>live</methodname>" +
                    "<compressionrate>90</compressionrate>" +
                    "</methodcall>\r\n\r\n");
            }
            else
            {
                sendBuffer = string.Format(
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?><methodcall><requestid>1</requestid>" +
                    "<methodname>live</methodname>" +
                    "<compressionrate>{0}</compressionrate>" +
                    "</methodcall>\r\n\r\n",
                    _quality);
            }

            return sendBuffer;
        }

        private string FormatGetPresets()
        {
            string sendBuffer = string.Format(
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?><methodcall><requestid>1</requestid><methodname>getpresetlist</methodname></methodcall>\r\n\r\n");
            return sendBuffer;
        }

        private string XmlEscapeGt127(string raw)
        {
            string str = "";

            foreach (char c in raw)
            {
                if (c < 128)
                {
                    str += c;
                }
                else
                {
                    str += string.Format("&#{0};", Convert.ToUInt32(c));
                }
            }

            return str;
        }

        private string FormatConnect()
        {
            string sendBuffer;

            if (_token == "BASIC")
            {
                // TBD: XML-escape characters over 127, like &#xF8; for ø
                sendBuffer = string.Format(
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?><methodcall><requestid>0</requestid>" +
                    "<methodname>connect</methodname><username>{0}</username><password>{1}</password>" +
                    "<cameraid>{2}</cameraid><alwaysstdjpeg>yes</alwaysstdjpeg>" +
                    "<transcode><width>{3}</width><height>{4}</height></transcode>" +
                    "</methodcall>\r\n\r\n",
                    XmlEscapeGt127(_user), XmlEscapeGt127(_pwd), _cameraGuid, _width, _height);
            }
            else
            {
                sendBuffer = string.Format(
                     "<?xml version=\"1.0\" encoding=\"utf-8\"?><methodcall><requestid>0</requestid>" +
                     "<methodname>connect</methodname><username>a</username><password>a</password>" +
                     "<cameraid>{0}</cameraid><alwaysstdjpeg>yes</alwaysstdjpeg>" +
                     "<connectparam>id={1}&amp;connectiontoken={2}</connectparam>" +
                     "<transcode><width>{3}</width><height>{4}</height></transcode>" +
                     "</methodcall>\r\n\r\n",
                     _cameraGuid, _cameraGuid, _token, _width, _height);
            }

            return sendBuffer;
        }

        public void Playback()
        {
            Socket sock = null;
            IPAddress ipaddr = null;

            try
            {
                _playback = true;

                // Go
                try
                {
                    sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                    try
                    {
                        ipaddr = IPAddress.Parse(_imageServer);
                    }
                    catch
                    {
                        ipaddr = ConnInfo.ToIpv4(_imageServer);
                    }

                    IPEndPoint ipe = new IPEndPoint(ipaddr, _imagePort);
                    sock.Connect(ipe);
                }
                catch (Exception e)
                {
                    // Tell the application I'm done
                    if (_onConnectionStopped != null)
                    {
                        Control pj = (Control)_renderObject;
                        pj.Dispatcher.Invoke(_onConnectionStopped, new Object[] { new ConnInfo(IscErrorCode.NoConnect, e.Message) });
                    }

                    return; // This is a thread. It won't help returning an error code
                }

                int maxbuf = 1024 * 64;

                string sendBuffer = FormatConnect();

                // Deliberately not encoded as UTF-8
                // With XPCO and XPE/WinAuth only the camera GUID and the token are used. These are always 7 bit ASCII
                // With XPE/BasicAuth, the username and password are in clear text. The server expect bytes in it's own current code page.
                // Encoding this as UTF-8 will prevent corrent authentication with other than 7-bit ASCII characters in username and password
                // Encoding this with "Default" will at least make other than 7-bit ASCII work when client's and server's code pages are alike
                // XPE's Image Server Manager has an option of manually selecting a code page.
                // But there is no way in which a client can obtain the XPE server's code page selection.
                Byte[] bytesSent = Encoding.Default.GetBytes(sendBuffer);
                sock.Send(bytesSent, bytesSent.Length, 0);

                Byte[] bytesReceived = new Byte[maxbuf];

                int bytes = RecvUntil(sock, bytesReceived, 0, maxbuf);
                string page = Encoding.UTF8.GetString(bytesReceived, 0, bytes);

                PlaybackSendConnectUpdateFlag = false; // We just got a new token, old renewal requests can be ignored.
                
                bool authenticated = false;
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(page);
                XmlNodeList nodes = doc.GetElementsByTagName("connected");
                foreach (XmlNode node in nodes)
                {
                    if (node.InnerText.ToLower() == "yes")
                    {
                        authenticated = true;
                    }
                }

                if (!authenticated)
                {
                    // Tell the application I'm done
                    if (_onConnectionStopped != null)
                    {
                        Control pj = (Control)_renderObject;
                        pj.Dispatcher.Invoke(_onConnectionStopped, new Object[] { new ConnInfo(IscErrorCode.InvalidCredentials, "" )});
                    }

                    return;
                }

                int count = 1; 
                //bool atEnd = false;
                _playbackTime = _playbackStartTime;
                //while (_playback && !atEnd)
                DateTime prevTime = DateTime.Now;
                while (_playback)
                {
                    if (_playbackSendConnectUpdateFlag)
                    {
                        _playbackSendConnectUpdateFlag = false;
                        sendBuffer = FormatConnectUpdate();
                        bytesSent = Encoding.UTF8.GetBytes(sendBuffer);
                        sock.Send(bytesSent, bytesSent.Length, 0);
                        
                        bytes = RecvUntil(sock, bytesReceived, 0, maxbuf);
                    }

                    DateTime realTimeLastImage = DateTime.MinValue;
                    int curbufsize = maxbuf;

                    int qual = _quality;
                    if (_quality < 1 || _quality > 104)
                    {
                        qual = 100;
                    }
                    sendBuffer = string.Format(
                        "<?xml version=\"1.0\" encoding=\"utf-8\"?><methodcall><requestid>{0}</requestid>" +
                        "<methodname>goto</methodname>" +
                        "<time>{1}</time>" +
                        "<compressionrate>{2}</compressionrate>" +
                        "<keyframesonly>no</keyframesonly>" +
                        "</methodcall>\r\n\r\n",
                        ++count, _playbackTime, qual);

                    bytesSent = Encoding.UTF8.GetBytes(sendBuffer);
                    sock.Send(bytesSent, bytesSent.Length, 0);

                    bytes = RecvUntil(sock, bytesReceived, 0, maxbuf);
                    if (bytes < 0)
                    {
                        throw new Exception("Receive error A");
                    }
                    page = Encoding.UTF8.GetString(bytesReceived, 0, bytes);

                    if (bytesReceived[0] == '<')
                    {
                        // This is XML status message
                        continue;
                    }

                    if (bytesReceived[0] == 'I')
                    {
                        // Image
                        ImageInfo h = ParseHeader(bytesReceived, 0, bytes);
                        DateTime cur = TimeConverter.FromString(h.Current);

                        Console.WriteLine(DateTime.Now.ToString("yyyyMMddHHmmss.fff") + " " + cur.ToString("yyyyMMddHHmmss.fff"));

                        // Taste two first bytes
                        bytes = RecvFixed(sock, bytesReceived, 0, 2);
                        if (!(bytes == 2 || bytes == -2))
                        {
                            throw new Exception("Receive error 2");
                        }

                        // if (h.Type.Contains("image/jpeg")) // No, XPCO 3.0a can send jpeg with genericbytedata headers
                        if (bytesReceived[0] == 0xFF && bytesReceived[1] == 0xD8)
                        {
                            int neededbufsize = h.Length + 4;
                            if (neededbufsize > curbufsize)
                            {
                                int newbufsize = RoundUpBufSize(neededbufsize);
                                curbufsize = newbufsize;
                                byte b0 = bytesReceived[0];
                                byte b1 = bytesReceived[1];
                                bytesReceived = new byte[curbufsize];
                                bytesReceived[0] = b0;
                                bytesReceived[1] = b1;
                            }
                            bytes = RecvFixed(sock, bytesReceived, 2, neededbufsize - 2);
                            if (bytes < 0)
                            {
                                throw new Exception("Receive error B");
                            }
                        }
                        else
                        {
                            bytes = RecvFixed(sock, bytesReceived, 2, 34);
                            if (!(bytes == 34 || bytes == -34))
                            {
                                throw new Exception("Receive error C");
                            }
                            int neededbufsize = h.Length - 36 + 4;
                            if (neededbufsize > curbufsize)
                            {
                                int newbufsize = RoundUpBufSize(neededbufsize);
                                curbufsize = newbufsize;
                                bytesReceived = new byte[curbufsize];
                            }
                            bytes = RecvFixed(sock, bytesReceived, 0, neededbufsize);
                            if (bytes < 0)
                            {
                                throw new Exception("Receive error D");
                            }
                        }

                        byte[] ms = new byte[bytes];
                        Buffer.BlockCopy(bytesReceived, 0, ms, 0, bytes);
                        h.Data = ms;
                        img = h;

                        //if (_onImagereceived != null)
                        //{
                        //    Control pi = (Control)_renderObject;
                        //    pi.Dispatcher.Invoke(_onImagereceived, new Object[] { h });
                        //}

                        // Stop if we reach the end of the sequence
                        double nextTime = double.Parse(h.Next);
                        if (nextTime >= _playbackEndTime)
                        //if (Math.Abs(nextTime - _playbackEndTime) < 500)
                        {
                            //_atEnd = true;
                            break;
                        }

                        // If there is no more video, do not keep repeating the last image, but stop the playback.
                        if (nextTime == _playbackTime)
                        {
                            //_atEnd = true;
                            break;
                        }

                        int interval = (int)(nextTime - _playbackTime); // We ought to subtract also the number of milliseconds elapsed in real time since last update
                        _playbackTime = nextTime;
                        //_playbackTime += DateTime.Now.Subtract(prevTime).TotalMilliseconds;
                        prevTime = DateTime.Now;
                    }
                }

                // Tell the application I'm done
                //if (_onConnectionStopped != null)
                //{
                //    Control pij = (Control)_renderObject;
                //    pij.Dispatcher.Invoke(_onConnectionStopped, new Object[] { new ConnInfo(IscErrorCode.Success, "") });
                //}
            }

            catch (OutOfMemoryException)
            {
                //Control ipj = (Control)_renderObject;
                //ipj.Dispatcher.Invoke(_onConnectionStopped, new Object[] { new ConnInfo(IscErrorCode.OutOfMemory, "") });
            }
            catch (System.Net.Sockets.SocketException e)
            {
                //string s = e.Message;
                //Control ipj = (Control)_renderObject;
                //ipj.Dispatcher.Invoke(_onConnectionStopped, new Object[] { new ConnInfo(IscErrorCode.SocketError, e.Message) });
            }
            catch (Exception e)
            {
                //Control ipj = (Control)_renderObject;
                //ipj.Dispatcher.Invoke(_onConnectionStopped, new Object[] { new ConnInfo(IscErrorCode.InternalError, e.Message) });
            }

            try
            {
                // If the answer was OK, you may now use the traditional requests like Goto or Live
                // When done with that, you should Disconnect the SOAP Login so as to declare the token obsolete
                sock.Close();
            }
            catch 
            { 
            }
        }

        public string GetSequences(DateTime dt, int max)
        {
            try
            {
                Socket sock;
                IPAddress ipaddr = null;
                try
                {
                    sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                    try
                    {
                        ipaddr = IPAddress.Parse(_imageServer);
                    }
                    catch
                    {
                        ipaddr = ConnInfo.ToIpv4(_imageServer);
                    }

                    IPEndPoint ipe = new IPEndPoint(ipaddr, _imagePort);
                    sock.Connect(ipe);
                }
                catch
                {
                    return "#No response";
                }

                int maxbuf = 512 * max;

                string sendBuffer = FormatConnect();

                // Deliberately not encoded as UTF-8
                // With XPCO and XPE/WinAuth only the camera GUID and the token are used. These are always 7 bit ASCII
                // With XPE/BasicAuth, the username and password are in clear text. The server expect bytes in it's own current code page.
                // Encoding this as UTF-8 will prevent corrent authentication with other than 7-bit ASCII characters in username and password
                // Encoding this with "Default" will at least make other than 7-bit ASCII work when client's and server's code pages are alike
                // XPE's Image Server Manager has an option of manually selecting a code page.
                // But there is no way in which a client can obtain the XPE server's code page selection.
                Byte[] bytesSent = Encoding.Default.GetBytes(sendBuffer);
                sock.Send(bytesSent, bytesSent.Length, 0);

                Byte[] bytesReceived = new Byte[maxbuf];
                int bytes = RecvUntil(sock, bytesReceived, 0, maxbuf);
                string page = Encoding.UTF8.GetString(bytesReceived, 0, bytes);

                bool authenticated = false;
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(page);
                XmlNodeList nodes = doc.GetElementsByTagName("connected");
                foreach (XmlNode node in nodes)
                {
                    if (node.InnerText.ToLower() == "yes")
                    {
                        authenticated = true;
                    }
                }

                if (!authenticated)
                {
                   return "#Invalid credentials";
                }

                double centertime = TimeConverter.ToDouble(dt);
                double starttime = TimeConverter.ToDouble(dt - TimeSpan.FromHours(24));
                double timespan = centertime - starttime;
                sendBuffer = string.Format(
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?><methodcall><requestid>1</requestid>" +
                    "<methodname>alarms</methodname>" +
                    "<centertime>{0}</centertime>" +
                    "<timespan>{1}</timespan>" +
                    "<numalarms>{2}</numalarms>" +
                    "</methodcall>\r\n\r\n", centertime.ToString(), timespan.ToString(), max);

                bytesSent = Encoding.UTF8.GetBytes(sendBuffer);
                sock.Send(bytesSent, bytesSent.Length, 0);

                bytes = RecvUntil(sock, bytesReceived, 0, maxbuf);
                if (bytes < 0) bytes = -bytes;
                page = Encoding.UTF8.GetString(bytesReceived, 0, bytes);

                sock.Close();

                return page;
            }
            catch
            {
                return "";
            }
        }

        public void DoLiveCmd(string cmd)
        {
            if (_sockLive == null)
                return;

            try
            {
                Byte[] bytesSent = Encoding.UTF8.GetBytes(cmd);
                lock (_liveSocketSendLock)
                {
                    int rc = _sockLive.Send(bytesSent, bytesSent.Length, 0);
                }
            }
            catch
            {
            }
        }

        public void Live()
        {
            Socket sock = null; ;

            try
            {
                IPAddress ipaddr = null;
                _live = true;
                string oper = "Live()";

                // Go
                try
                {
                    oper = "new Socket";
                    sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                    try
                    {
                        oper = "IPAddress.Parse " + _imageServer;
                        ipaddr = IPAddress.Parse(_imageServer);
                    }
                    catch
                    {
                        oper = "ConnInfo.ToIpv4 " + _imageServer;
                        ipaddr = ConnInfo.ToIpv4(_imageServer);
                    }

                    oper = "new IPEndPoint " + ipaddr.ToString();
                    IPEndPoint ipe = new IPEndPoint(ipaddr, _imagePort);
                    sock.Connect(ipe);
                }
                catch (SocketException se)
                {
                    // Tell the application I'm done
                    if (_onConnectionStopped != null)
                    {
                        Control pj = (Control)_renderObject;
                        string emsg = string.Format("Socket error {0}. Win32 error {1}", se.ErrorCode, se.NativeErrorCode);
                        pj.Dispatcher.Invoke(_onConnectionStopped, new Object[] { new ConnInfo(IscErrorCode.NoConnect, emsg) });
                    }

                    return; // This is a thread. It won't help returning an error code
                }
                catch (Exception)
                {
                    // Tell the application I'm done
                    if (_onConnectionStopped != null)
                    {
                        Control pj = (Control)_renderObject;
                        pj.Dispatcher.Invoke(_onConnectionStopped, new Object[] { new ConnInfo(IscErrorCode.NoConnect, oper) });
                    }

                    return; // This is a thread. It won't help returning an error code
                }

                int maxbuf = 1024 * 8;

                string sendBuffer = FormatConnect();

                // Deliberately not encoded as UTF-8
                // With XPCO and XPE/WinAuth only the camera GUID and the token are used. These are always 7 bit ASCII
                // With XPE/BasicAuth, the username and password are in clear text. The server expect bytes in it's own current code page.
                // Encoding this as UTF-8 will prevent corrent authentication with other than 7-bit ASCII characters in username and password
                // Encoding this with "Default" will at least make other than 7-bit ASCII work when client's and server's code pages are alike
                // XPE's Image Server Manager has an option of manually selecting a code page.
                // But there is no way in which a client can obtain the XPE server's code page selection.
                Byte[] bytesSent = Encoding.Default.GetBytes(sendBuffer);
                
                lock(_liveSocketSendLock)
                {
                    sock.Send(bytesSent, bytesSent.Length, 0);
                }

                Byte[] bytesReceived = new Byte[maxbuf];

                int bytes = RecvUntil(sock, bytesReceived, 0, maxbuf);
                string page = Encoding.UTF8.GetString(bytesReceived, 0, bytes);

                bool authenticated = false;
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(page);
                XmlNodeList nodes = doc.GetElementsByTagName("connected");
                foreach (XmlNode node in nodes)
                {
                    if (node.InnerText.ToLower() == "yes")
                    {
                        authenticated = true;
                    }
                }

                if (!authenticated)
                {
                    // Tell the application I'm done
                    if (_onConnectionStopped != null)
                    {
                        Control pj = (Control)_renderObject;
                        pj.Dispatcher.Invoke(_onConnectionStopped, new Object[] { new ConnInfo(IscErrorCode.InvalidCredentials, "") });
                    }

                    return; // This is a thread. It won't help returning an error code
                }

                /*
                sendBuffer = FormatGetPresets();
                bytesSent = Encoding.UTF8.GetBytes(sendBuffer);
                sock.Send(bytesSent, bytesSent.Length, 0);
                bytes = RecvUntil(sock, bytesReceived, 0, maxbuf);
                page = Encoding.UTF8.GetString(bytesReceived, 0, bytes);
                */

                sendBuffer = FormatLive();
                bytesSent = Encoding.UTF8.GetBytes(sendBuffer);
                lock (_liveSocketSendLock)
                {
                    sock.Send(bytesSent, bytesSent.Length, 0);
                }
                page = Encoding.UTF8.GetString(bytesSent, 0, bytesSent.Length);

                // Others may now send on this socket, preferrably using DoLiveCmd()
                _reqCounter = 2;
                _sockLive = sock;

                while (_live)
                {
                    // Buffer size housekeeping
                    int curbufsize = maxbuf;

                    bytes = RecvUntil(sock, bytesReceived, 0, maxbuf);
                    if (bytes < 0)
                    {
                        throw new Exception("Receive error A");
                    }

                    if (bytesReceived[0] == '<')
                    {
                        // This is XML status message
                        page = Encoding.UTF8.GetString(bytesReceived, 0, bytes);
                        XmlDocument statdoc = new XmlDocument();
                        statdoc.LoadXml(page);
                        if (_onStatusItemReceived != null)
                        {
                            Control pj = (Control)_renderObject;
                            pj.Dispatcher.Invoke(_onStatusItemReceived, new Object[] { statdoc });
                        }

                        continue;
                    }

                    if (bytesReceived[0] == 'I')
                    {
                        // Image
                        ImageInfo h = ParseHeader(bytesReceived, 0, bytes);

                        // Taste two first bytes
                        bytes = RecvFixed(sock, bytesReceived, 0, 2);
                        if (!(bytes == 2 || bytes == -2))
                        {
                            throw new Exception("Receive error 2");
                        }

                        // if (h.Type.Contains("image/jpeg")) // No, XPCO 3.0a can send jpeg with genericbytedata headers
                        if (bytesReceived[0] == 0xFF && bytesReceived[1] == 0xD8)
                        {
                            int neededbufsize = h.Length + 4;
                            if (neededbufsize > curbufsize)
                            {
                                int newbufsize = RoundUpBufSize(neededbufsize);
                                curbufsize = newbufsize;
                                byte b0 = bytesReceived[0];
                                byte b1 = bytesReceived[1];
                                bytesReceived = new byte[curbufsize];
                                bytesReceived[0] = b0;
                                bytesReceived[1] = b1;
                            }
                            bytes = RecvFixed(sock, bytesReceived, 2, neededbufsize-2);
                            if (bytes < 0)
                            {
                                throw new Exception("Receive error B");
                            }
                        }
                        else
                        {
                            bytes = RecvFixed(sock, bytesReceived, 2, 34);
                            if (!(bytes == 34 || bytes == -34))
                            {
                                throw new Exception("Receive error C");
                            }
                            int neededbufsize = h.Length - 36 + 4;
                            if (neededbufsize > curbufsize)
                            {
                                int newbufsize = RoundUpBufSize(neededbufsize);
                                curbufsize = newbufsize;
                                bytesReceived = new byte[curbufsize];
                            }
                            bytes = RecvFixed(sock, bytesReceived, 0, neededbufsize);
                            if (bytes < 0)
                            {
                                throw new Exception("Receive error D");
                            }
                        }

                        _lastJpeg = bytesReceived;

                        try
                        {
                            byte[] ms = new byte[bytes];
                            Buffer.BlockCopy(bytesReceived, 0, ms, 0, bytes);
                            h.Data = ms;
                            img = h;
                            //if (_camName == "FPL1_45")
                            //    Console.WriteLine(_camName + " " + TimeConverter.FromString(img.Current).ToString("yyyyMMddHHmmss.fff")) ;

                            //Control pi = (Control)_renderObject;
                            //pi.Dispatcher.Invoke(_onImagereceived, new Object[] { h });
                        }
                        catch (OutOfMemoryException)
                        {
                            Control pp = (Control)_renderObject;
                            pp.Dispatcher.Invoke(_onConnectionStopped, new Object[] { new ConnInfo(IscErrorCode.OutOfMemory, "") });
                            StopLive();
                        }
                        catch (Exception e)
                        {
                            Control pp = (Control)_renderObject;
                            pp.Dispatcher.Invoke(_onConnectionStopped, new Object[] { new ConnInfo(IscErrorCode.NotJpegError, e.Message) });
                        }
                    }
                }

                // Tell the application I'm done
                Control ipj = (Control)_renderObject;
                ipj.Dispatcher.Invoke(_onConnectionStopped, new Object[] { new ConnInfo(IscErrorCode.Success, "") });
            }

            catch (OutOfMemoryException)
            {
                //Control ipj = (Control)_renderObject;
                //ipj.Dispatcher.Invoke(_onConnectionStopped, new Object[] { new ConnInfo(IscErrorCode.OutOfMemory, "") });
            }
            catch (System.Net.Sockets.SocketException e)
            {
                //string s = e.Message;
                //Control ipj = (Control)_renderObject;
                //ipj.Dispatcher.Invoke(_onConnectionStopped, new Object[] { new ConnInfo(IscErrorCode.SocketError, e.Message) });
            }
            catch (Exception e)
            {
                //string s = e.Message;
                //Control ipj = (Control)_renderObject;
                //ipj.Dispatcher.Invoke(_onConnectionStopped, new Object[] { new ConnInfo(IscErrorCode.InternalError, e.Message) });
            }

            try
            {
                // If the answer was OK, you may now use the traditional requests like Goto or Live
                // When done with that, you should Disconnect the SOAP Login so as to declare the token obsolete
                sock.Close();
            }
            catch
            {
            }
        }

        private int RoundUpBufSize(int needed)
        {
            int roundup = (needed / 1024) * 1024 / 100 * 130;
            return roundup;
        }

        private static int RecvFixed(Socket sock, byte[] buf, int offset, int size)
        {
            int miss = size;
            int got = 0;
            int bytes = 0;
            int get = 1;
            int maxb = 1024 * 16;

            do
            {
                get = miss > maxb ? maxb : miss;
                bytes = sock.Receive(buf, offset + got, get, SocketFlags.None);
                got += bytes;
                miss -= bytes;
            }
            while (got < size);

            if (got > size)
            {
                throw new Exception("Buffer overflow");
            }

            if (size < 4)
                return -got;

            int i = offset + got - 4;
            if (buf[i] == '\r' && buf[i + 1] == '\n' && buf[i + 2] == '\r' && buf[i + 3] == '\n')
            {
                return got;
            }

            return -got;

        }

        private static int RecvUntil(Socket sock, byte[] buf, int offset, int size)
        {
            int miss = size;
            int got = 0;
            int bytes = 0;
            int retry = 100;
            int ended = 4;
            int i = 0;


            while (got < size && ended > 0 && retry > 0)
            {
                i = offset + got;
                bytes = sock.Receive(buf, i, 1, SocketFlags.None);

                if (bytes == 1)
                {
                    if (buf[i] == '\r' || buf[i] == '\n')
                    {                   
                        ended--;
                    }
                    else
                    {
                        ended = 4;
                    }
                    got += bytes;
                    miss -= bytes;
                } else
                {
                	throw new Exception("Session closed by server");
                }

                if (sock.Available == 0)
                {
                    System.Threading.Thread.Sleep(100);
                    retry--;
                }
            }

            if (got > size)
            {
                throw new Exception("Buffer overflow");
            }

            if (ended == 0)
            {
                return got;
            }
            else
            {
                return -got;
            }
        }

        private static ImageInfo ParseHeader(byte[] buf, int offset, int bytes)
        {
            ImageInfo h = new ImageInfo();
            h.Length = 0;
            h.Type = "";

            string response = Encoding.UTF8.GetString(buf, offset, bytes);
            string[] headers = response.Split('\n');
            foreach (string header in headers)
            {
                string[] keyval = header.Split(':');
                if (keyval[0].ToLower() == "content-length" && keyval.Length > 1)
                {
                    h.Length = int.Parse(keyval[1]);
                }
                if (keyval[0].ToLower() == "content-type" && keyval.Length > 1)
                {
                    h.Type = keyval[1].Trim('\r').ToLower();
                }
                if (keyval[0].ToLower() == "current" && keyval.Length > 1)
                {
                    h.Current = keyval[1].Trim('\r');
                }
                if (keyval[0].ToLower() == "next" && keyval.Length > 1)
                {
                    h.Next = keyval[1].Trim('\r');
                }
                if (keyval[0].ToLower() == "prev" && keyval.Length > 1)
                {
                    h.Prev = keyval[1].Trim('\r');
                }
            }

            return h;
        }
    }

    public class ImageInfo
    {
        public int Length;
        public string Type;
        public string Current;
        public string Next;
        public string Prev;
        public object Data;

        public ImageInfo()
        {
            Length = -1;
            Type = "";
            Current = "";
            Next = "";
            Prev = "";
            Data = null;
        }
    }

    public enum IscErrorCode { Success, NoConnect, InvalidCredentials, OutOfMemory, SocketError, NotJpegError, InternalError } ;
    public enum IscStatusItem { None=0, LiveFeedStarted=1, LiveFeedMotion=2, LiveFeedRecording=3, LiveFeedEventNotification=4, 
        CameraConnectionLost=5, DatabaseFail=6, RunningOutOfDiskSpace=7 } ;

    public class ConnInfo
    {
        public IscErrorCode ErrorCode;
        public string Message;

        public ConnInfo(IscErrorCode errc, string errm)
        {
            ErrorCode = errc;
            Message = errm;
        }

        public static IPAddress ToIpv4(string dns)
        {
            IPAddress ipaddr;
            byte[] nullip = {0, 0, 0, 0};
            try
            {
                ipaddr = IPAddress.Parse(dns);
                return ipaddr;
            }
            catch (Exception)
            {
                try
                {
                    ipaddr = null;
                    IPHostEntry ent = Dns.GetHostEntry(dns);
                    foreach (IPAddress addr in ent.AddressList)
                    {
                        if (addr.AddressFamily == AddressFamily.InterNetwork)
                        {
                            ipaddr = addr;
                            return ipaddr;
                        }
                    }
                    return new IPAddress(nullip);
                }
                catch
                {
                    return new IPAddress(nullip);
                }
            }
        }
    }
}
