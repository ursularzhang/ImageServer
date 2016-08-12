using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImageService
{
    public interface ImageServer
    {
        // interface members
        bool isAvailable();

        Frame getFirstFrame();
        Frame getLastFrame();
        void clearBuffer();
        void getFrame(DateTime ts);
        void removeFrame();

        int width();
        int height();
        int channel();
        string status();

        void ConnectServer(string str);
        void SetPlaybackSpeed(double speed);
        void SetLoop(bool toLoop);
        void StartStream();
        void PauseStream();
        void ResumeStream();
        void StopStream();
    }
}
