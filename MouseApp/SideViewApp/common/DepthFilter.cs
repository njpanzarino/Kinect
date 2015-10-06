using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SideViewApp.common
{
    class DepthFilter
    {
        ushort _bufferIndex=0;
        private int _bufferSize = 5;
        public int[] buffer;

        ushort median
        {
            get
            {
                int[] copy = new int[_bufferSize];
                Array.Copy(buffer, copy, _bufferSize);
                Array.Sort(copy);
                return (ushort)copy[_bufferIndex];
            }
        }
        float average { get { return (ushort)buffer.Average(); } }

        //ushort max=0;
        
        int BufferSize {
            get { return _bufferSize;}
           
            set
            {
                buffer = new int[value];
                _bufferIndex = 0;
                _bufferSize = value;
            }
        }

        int BufferIndex
        {
            get
            {
                return _bufferIndex;
            }
            set
            {
                ++_bufferIndex;
                if (_bufferIndex >= _bufferSize)
                {
                    _bufferIndex = 0;
                }
            }
        }

        public DepthFilter()
        {
            BufferSize = _bufferSize;
        }

        public DepthFilter(int bufferSize)
        {
            BufferSize = bufferSize;
        }

        public ushort update(ushort val)
        {
            buffer[_bufferIndex] = val;
            ++BufferIndex;
            return val;
        }

    }
}
