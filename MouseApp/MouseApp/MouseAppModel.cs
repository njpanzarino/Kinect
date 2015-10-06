using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics;

namespace MouseApp
{
    using Microsoft.Kinect;
    using System.Windows.Media.Media3D;

    class MouseAppModel
    {
        public MouseAppSettings settings;

        public MouseAppModel()
        {
            settings = new MouseAppSettings();
        }

        public struct MouseAppSettings
        {
            public bool relativeMode;
            public bool printDebug;
            public bool printHands;

            public bool pauseWhenStill;
            public int handDispBufferSize;
            public int handDispBufferCounter;
            public int stillLagMillis;
            public float stillThresh;

            public float sensitivity;

            public MouseAppSettings(bool debug = false)
            {
                relativeMode            = false;
                printDebug              = debug;
                printHands              = true;

                pauseWhenStill          = true;
                handDispBufferSize      = 25;
                handDispBufferCounter   = 0;
                stillLagMillis          = 750;
                stillThresh             = .001f;

                sensitivity             = 1f;
            }
        }
    }

    
}
