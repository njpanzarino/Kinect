using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SideViewApp.common
{
    using Microsoft.Kinect;

    class DepthNode
    {
        
        const float lowThreshSLope = .005f;
        const float highThreshSlope = .03f;

        const ushort lowThreshOffset = 0;
        const ushort highThreshOffset = 0;

        private ushort _baseDepth;
        private ushort lowThresh;
        private ushort highThresh;

        public int depthIndex { get; private set; } = -1;
        public ushort baseDepth
        {
            get { return _baseDepth; }
            set
            {
                _baseDepth = value;
                lowThresh = (ushort)((_baseDepth * lowThreshSLope)+ lowThreshOffset);
                highThresh = (ushort)((_baseDepth * highThreshSlope)+ highThreshOffset);
            }
        }
        public ushort currentDepth;

        public bool onScreen;
        public bool onScreenEdge;

        public bool onEdge;

        DepthFilter filter=new DepthFilter();

        public bool IsActive
        {
            get
            {
                int d = baseDepth - filter.buffer.Max();

                if (d > lowThresh)
                {
                    if (onEdge)
                    {
                        return true;
                    }
                    else if (d < highThresh)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public DepthNode(int depthIndex)
        {
            Initialize(depthIndex);
        }

        public DepthNode(int depthIndex, ushort calDepth)
        {
            Initialize(depthIndex);
            this.baseDepth = calDepth;
        }

        public void Initialize(int depthIndex)
        {
            this.depthIndex = depthIndex;
            baseDepth = 0;
            currentDepth = 0;
            onEdge = false;
            onScreen = false;
            onScreenEdge = false;
        }

        public void update(ushort current)
        {
            currentDepth = filter.update(current);
        }

        public void Clear()
        {
            Initialize(depthIndex);
        }
    }
}
