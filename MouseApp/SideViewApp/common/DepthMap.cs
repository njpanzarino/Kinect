using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SideViewApp.common
{
    using System.Windows;
    /// <summary>
    /// 
    /// </summary>
    class DepthMap : IEnumerable
    {
        const int BASE_BUFFER_SIZE = 50;
        int bufferIteration = 0;

        private DepthNode[] nodes = null;
        private int width;

        List<int>[] baseBuffer = null;

        int Count
        {
            get
            {
                return nodes.Length;
            }
        }

        public DepthNode this[int index]
        {
            get
            {
                return nodes[index];
            }
        }

        public DepthNode this[int x, int y]
        {
            get
            {
                return nodes[width * y + x];
            }
        }

        public int X(DepthNode n)
        {
            return width - (n.depthIndex / width);
        }

        public int Y(DepthNode n)
        {
            return n.depthIndex / width;
        }

        public DepthMap(ushort[] depthData, int width)
        {
            this.width = width;
            nodes = new DepthNode[depthData.Length];

            for (int i = 0; i < depthData.Length; i++)
            {
                nodes[i]=new DepthNode(i);
            }
            baseBuffer = new List<int>[nodes.Length];
        }

        public void acquireBase()
        {
            baseBuffer = new List<int>[nodes.Length];
        }

        public void Update(ushort[] depthData)
        {
            //Base buffer has already been used and cleared
            if (baseBuffer == null)
            {
                for (int i = 0; i < depthData.Length; i++)
                {
                    nodes[i].update(depthData[i]);
                }
            }
            else
            {
                //Add to base buffer
                for (int i = 0; i < depthData.Length; i++)
                {
                    //Dont add zeroes
                    if (depthData[i] != 0)
                    {
                        if (baseBuffer[i] == null)
                        {
                            baseBuffer[i] = new List<int>();
                        }
                        baseBuffer[i].Add(depthData[i]);
                    }
                }
                ++bufferIteration;
                //if buffer is full, set values and clear buffer
                if (bufferIteration >= BASE_BUFFER_SIZE)
                {
                    for (int i = 0; i < baseBuffer.Length; i++)
                    {
                        if (baseBuffer[i] != null && baseBuffer[i].Count > 0)
                        {
                            //Min, Max, or Agerage?
                            nodes[i].baseDepth = Convert.ToUInt16(baseBuffer[i].Min());
                        }
                        else
                            nodes[i].baseDepth = 0;
                        
                    }
                    baseBuffer = null;
                    bufferIteration = 0;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return nodes.GetEnumerator();
        }
    }
}
