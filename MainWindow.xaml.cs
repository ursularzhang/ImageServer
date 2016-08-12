using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Utility;
using System.Threading;
using System.Windows.Threading;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.ComponentModel;

using ImageService;
using System.IO;

namespace VideoGrid
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        #region ForCommunication
        static int MaxWaitMS = 50;
        static int MaxSocketInst = 30;
        static int Conn_Duration = 10;

        private Thread _updateFrame = null;

        //private List<MilestoneTcpViewer> _cameras = new List<MilestoneTcpViewer>();
        private List<ImageServer> _cameras = new List<ImageServer>(16);
        private int _commPort;
        //private string _dbAddr, _dbName, _dbUser, _dbPW;
        private string _vmsAddr, _vmsUser, _vmsPW, _vmsAuth;
        private DispatcherTimer dt;


        private BitmapImage ToBitmapImage(Bitmap src)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                src.Save(memory, ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
            }
        }

        void UpdateFrame()
        {
            while (true)
            {
                try
                {
                    //Thread.Sleep(30);
                    if (gridNum <= 0) continue;
                    if (_cameras[0].isAvailable())
                    {
                        ImageService.Frame frm = _cameras[0].getLastFrame();
                        if (frm == null) continue;
                        try
                        {
                            Bitmap src = new Bitmap(frm.bitmap);
                            DisplayFrame1 = ToBitmapImage(src);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                    if (gridNum <= 1) continue;
                    if (_cameras[1].isAvailable())
                    {
                        ImageService.Frame frm = _cameras[1].getLastFrame();
                        if (frm == null) continue;
                        try
                        {
                            Bitmap src = new Bitmap(frm.bitmap);
                            DisplayFrame2 = ToBitmapImage(src);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                    if (gridNum <= 2) continue;
                    if (_cameras[2].isAvailable())
                    {
                        ImageService.Frame frm = _cameras[2].getLastFrame();
                        if (frm == null) continue;
                        try
                        {
                            Bitmap src = new Bitmap(frm.bitmap);
                            DisplayFrame3 = ToBitmapImage(src);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                    if (gridNum <= 3) continue;
                    if (_cameras[3].isAvailable())
                    {
                        ImageService.Frame frm = _cameras[3].getLastFrame();
                        if (frm == null) continue;
                        try
                        {
                            Bitmap src = new Bitmap(frm.bitmap);
                            DisplayFrame4 = ToBitmapImage(src);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                    if (gridNum <= 4) continue;
                    if (_cameras[4].isAvailable())
                    {
                        ImageService.Frame frm = _cameras[4].getLastFrame();
                        if (frm == null) continue;
                        try
                        {
                            Bitmap src = new Bitmap(frm.bitmap);
                            DisplayFrame5 = ToBitmapImage(src);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                    if (gridNum <= 5) continue;
                    if (_cameras[5].isAvailable())
                    {
                        ImageService.Frame frm = _cameras[5].getLastFrame();
                        if (frm == null) continue;
                        try
                        {
                            Bitmap src = new Bitmap(frm.bitmap);
                            DisplayFrame6 = ToBitmapImage(src);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                    if (gridNum <= 6) continue;
                    if (_cameras[6].isAvailable())
                    {
                        ImageService.Frame frm = _cameras[6].getLastFrame();
                        if (frm == null) continue;
                        try
                        {
                            Bitmap src = new Bitmap(frm.bitmap);
                            DisplayFrame7 = ToBitmapImage(src);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                    if (gridNum <= 7) continue;
                    if (_cameras[7].isAvailable())
                    {
                        ImageService.Frame frm = _cameras[7].getLastFrame();
                        if (frm == null) continue;
                        try
                        {
                            Bitmap src = new Bitmap(frm.bitmap);
                            DisplayFrame8 = ToBitmapImage(src);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }


                    if (gridNum <= 8) continue;
                    if (_cameras[8].isAvailable())
                    {
                        ImageService.Frame frm = _cameras[8].getLastFrame();
                        if (frm == null) continue;
                        try
                        {
                            Bitmap src = new Bitmap(frm.bitmap);
                            DisplayFrame9 = ToBitmapImage(src);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                    if (gridNum <= 9) continue;
                    if (_cameras[9].isAvailable())
                    {
                        ImageService.Frame frm = _cameras[9].getLastFrame();
                        if (frm == null) continue;
                        try
                        {
                            Bitmap src = new Bitmap(frm.bitmap);
                            DisplayFrame10 = ToBitmapImage(src);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                    if (gridNum <= 10) continue;
                    if (_cameras[10].isAvailable())
                    {
                        ImageService.Frame frm = _cameras[10].getLastFrame();
                        if (frm == null) continue;
                        try
                        {
                            Bitmap src = new Bitmap(frm.bitmap);
                            DisplayFrame11 = ToBitmapImage(src);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                    if (gridNum <= 11) continue;
                    if (_cameras[11].isAvailable())
                    {
                        ImageService.Frame frm = _cameras[11].getLastFrame();
                        if (frm == null) continue;
                        try
                        {
                            Bitmap src = new Bitmap(frm.bitmap);
                            DisplayFrame12 = ToBitmapImage(src);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                    if (gridNum <= 12) continue;
                    if (_cameras[12].isAvailable())
                    {
                        ImageService.Frame frm = _cameras[12].getLastFrame();
                        if (frm == null) continue;
                        try
                        {
                            Bitmap src = new Bitmap(frm.bitmap);
                            DisplayFrame13 = ToBitmapImage(src);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                    if (gridNum <= 13) continue;
                    if (_cameras[13].isAvailable())
                    {
                        ImageService.Frame frm = _cameras[13].getLastFrame();
                        if (frm == null) continue;
                        try
                        {
                            Bitmap src = new Bitmap(frm.bitmap);
                            DisplayFrame14 = ToBitmapImage(src);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                    if (gridNum <= 14) continue;
                    if (_cameras[14].isAvailable())
                    {
                        ImageService.Frame frm = _cameras[14].getLastFrame();
                        if (frm == null) continue;
                        try
                        {
                            Bitmap src = new Bitmap(frm.bitmap);
                            DisplayFrame15 = ToBitmapImage(src);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                    if (gridNum <= 15) continue;
                    if (_cameras[15].isAvailable())
                    {
                        ImageService.Frame frm = _cameras[15].getLastFrame();
                        if (frm == null) continue;
                        try
                        {
                            Bitmap src = new Bitmap(frm.bitmap);
                            DisplayFrame16 = ToBitmapImage(src);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }    
 
        private void LoadConfig()
        {
            SmartXML xml = new SmartXML(Thread.GetDomain().BaseDirectory + @"\config.xml", "/smartinit/");
            _commPort = xml.ReadInteger("comm_config", "port", 1088);

            _vmsAddr = xml.ReadString("vms_config", "url", "192.168.1.82");
            _vmsUser = xml.ReadString("vms_config", "user", "admin");
            _vmsPW = xml.ReadString("vms_config", "password", "admin");
            _vmsAuth = xml.ReadString("vms_config", "auth", "Basic");

            xml = null;
        }

        #endregion

        #region Components

        private const int MaxNumGrids = 16;
        private List<string> camList = new List<string>(MaxNumGrids);

        #endregion

        #region Events
        private int gridNum = 4;
        private BitmapImage _FrameImg1 = null;
        private BitmapImage _FrameImg2 = null;
        private BitmapImage _FrameImg3 = null;
        private BitmapImage _FrameImg4 = null;
        private BitmapImage _FrameImg5 = null;
        private BitmapImage _FrameImg6 = null;
        private BitmapImage _FrameImg7 = null;
        private BitmapImage _FrameImg8 = null;
        private BitmapImage _FrameImg9 = null;
        private BitmapImage _FrameImg10 = null;
        private BitmapImage _FrameImg11 = null;
        private BitmapImage _FrameImg12 = null;
        private BitmapImage _FrameImg13 = null;
        private BitmapImage _FrameImg14 = null;
        private BitmapImage _FrameImg15 = null;
        private BitmapImage _FrameImg16 = null;

        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        protected void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public BitmapImage DisplayFrame1
        {
            get
            {
                return _FrameImg1;
            }
            set
            {
                _FrameImg1 = value;
                NotifyPropertyChanged("DisplayFrame1");
            }
        }

        public BitmapImage DisplayFrame2
        {
            get
            {
                return _FrameImg2;
            }
            set
            {
                _FrameImg2 = value;
                NotifyPropertyChanged("DisplayFrame2");
            }
        }

        public BitmapImage DisplayFrame3
        {
            get
            {
                return _FrameImg3;
            }
            set
            {
                _FrameImg3 = value;
                NotifyPropertyChanged("DisplayFrame3");
            }
        }

        public BitmapImage DisplayFrame4
        {
            get
            {
                return _FrameImg4;
            }
            set
            {
                _FrameImg4 = value;
                NotifyPropertyChanged("DisplayFrame4");
            }
        }

        public BitmapImage DisplayFrame5
        {
            get
            {
                return _FrameImg5;
            }
            set
            {
                _FrameImg5 = value;
                NotifyPropertyChanged("DisplayFrame5");
            }
        }

        public BitmapImage DisplayFrame6
        {
            get
            {
                return _FrameImg6;
            }
            set
            {
                _FrameImg6 = value;
                NotifyPropertyChanged("DisplayFrame6");
            }
        }

        public BitmapImage DisplayFrame7
        {
            get
            {
                return _FrameImg7;
            }
            set
            {
                _FrameImg7 = value;
                NotifyPropertyChanged("DisplayFrame7");
            }
        }

        public BitmapImage DisplayFrame8
        {
            get
            {
                return _FrameImg8;
            }
            set
            {
                _FrameImg8 = value;
                NotifyPropertyChanged("DisplayFrame8");
            }
        }

        public BitmapImage DisplayFrame9
        {
            get
            {
                return _FrameImg9;
            }
            set
            {
                _FrameImg9 = value;
                NotifyPropertyChanged("DisplayFrame9");
            }
        }

        public BitmapImage DisplayFrame10
        {
            get
            {
                return _FrameImg10;
            }
            set
            {
                _FrameImg10 = value;
                NotifyPropertyChanged("DisplayFrame10");
            }
        }

        public BitmapImage DisplayFrame11
        {
            get
            {
                return _FrameImg11;
            }
            set
            {
                _FrameImg11 = value;
                NotifyPropertyChanged("DisplayFrame11");
            }
        }


        public BitmapImage DisplayFrame12
        {
            get
            {
                return _FrameImg12;
            }
            set
            {
                _FrameImg12 = value;
                NotifyPropertyChanged("DisplayFrame12");
            }
        }

        public BitmapImage DisplayFrame13
        {
            get
            {
                return _FrameImg13;
            }
            set
            {
                _FrameImg13 = value;
                NotifyPropertyChanged("DisplayFrame13");
            }
        }

        public BitmapImage DisplayFrame14
        {
            get
            {
                return _FrameImg14;
            }
            set
            {
                _FrameImg14 = value;
                NotifyPropertyChanged("DisplayFrame14");
            }
        }

        public BitmapImage DisplayFrame15
        {
            get
            {
                return _FrameImg15;
            }
            set
            {
                _FrameImg15 = value;
                NotifyPropertyChanged("DisplayFrame15");
            }
        }   

        public BitmapImage DisplayFrame16
        {
            get
            {
                return _FrameImg16;
            }
            set
            {
                _FrameImg16 = value;
                NotifyPropertyChanged("DisplayFrame16");
            }
        }  
        #endregion

        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = this;

            //load the config of Comm
            LoadConfig();
            gridNum = 16;

            camList.Add("FPL1_10"); camList.Add("FPL1_11"); camList.Add("FPL1_38"); camList.Add("FPL1_39");
            camList.Add("FPL1_40"); camList.Add("FPL1_44"); camList.Add("FPL1_45"); camList.Add("FPL1_46");
            camList.Add("FPL1_48"); camList.Add("FPL1_49"); camList.Add("FPB1_1"); camList.Add("FPB1_2");
            camList.Add("FPB2_3"); camList.Add("FPB2_4"); camList.Add("FPEX_5"); camList.Add("FPEX_6");
            camList.Add("FPEX_7"); camList.Add("FPEX_8");
            //camList.Add("Camera_1"); camList.Add("Camera_2"); camList.Add("Camera_3"); camList.Add("Camera_4");

            int width = 640, height = 360;

            for (int i = 0; i < gridNum; i++)
            {
                MilestoneMedia cam = new MilestoneMedia();
                _cameras.Add((ImageServer)cam);

                string str = String.Format("{0};{1};{2};{3};{4};{5};{6};{7};;;20;{8}", _vmsAddr, _vmsUser, _vmsPW, _vmsAuth, camList[i], 12, width, height, 0);
                Console.WriteLine(str);
                _cameras[i].ConnectServer(str);
                _cameras[i].StartStream();
            }

            _updateFrame = new Thread(new ThreadStart(UpdateFrame));
            _updateFrame.Start(); 
            
       }

        private void WindowClosed(object sender, EventArgs e)
        {
            for (int i = 0; i < _cameras.Count; i++)
            {
                if (_cameras[i].isAvailable())
                    _cameras[i].StopStream();
            }
            _updateFrame.Abort();

            this.Close();
        }

        private void OnGridLoaded(object sender, RoutedEventArgs e)
        {
        }

    }
}
