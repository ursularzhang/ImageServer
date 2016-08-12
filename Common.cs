using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace ImageService
{
    public enum Format
    {
        JpegLow = 0,
        JpegMedium = 1,
        JpegHigh = 2,
        JpegMaximum = 3,
        Png24 = 4,
    }

    public struct BITMAPINFOHEADER
    {
        public UInt32 biSize;
        public Int32 biWidth;
        public Int32 biHeight;
        public Int16 biPlanes;
        public Int16 biBitCount;
        public UInt32 biCompression;
        public UInt32 biSizeImage;
        public Int32 biXPelsPerMeter;
        public Int32 biYPelsPerMeter;
        public UInt32 biClrUsed;
        public UInt32 biClrImportant;
    }

    public class Frame
    {
        public byte[] img;
        public byte[] viewImg;
        public Bitmap bitmap;
        public DateTime ts;
        public DateTime nextTS;

        public Frame()
        {
        }

        public Frame(int width, int height, int stride)
        {
            img = new byte[height * stride];
        }

        //public Frame Clone()
        //{
        //    Frame copy = new Frame(bitmap.Width, bitmap.Height, bitmap.Width * 3);
        //    copy.bitmap = bitmap;
        //    copy.ts = ts;
        //    copy.nextTS = nextTS;


        //}


    }

    public enum PlayType
    {
        Live = 0,
        Playback = 1,
    }

}
