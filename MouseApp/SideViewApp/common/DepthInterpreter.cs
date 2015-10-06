using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SideViewApp.common
{
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;

    using System.Diagnostics;

    class DepthInterpreter : IDisposable
    {
        private readonly PixelFormat format = PixelFormats.Gray8;

        private KinectSensor kinectSensor = null;
        private DepthFrameReader depthFrameReader = null;
        private FrameDescription depthFrameDescription = null;

        public int displayWidth { get { return depthFrameDescription.Width; } }
        public int displayHeight { get { return depthFrameDescription.Height; } }

        private ushort[] _depthData = null;
        public ushort[] depthData {
            get {
                return _depthData;
            }
            set
            {
                _depthData = value;
                if (depthMap != null)
                {
                    depthMap.Update(value);
                }
            }

        }

        public DepthMap depthMap = null;

        private BitmapSource image = null;
        private WriteableBitmap depthBitmap = null;
        private byte[] depthPixelData = null;
        private int stride { get { return (depthBitmap.PixelWidth * format.BitsPerPixel + 7) / 8; } }

        private DrawingGroup drawingGroup;
        private DrawingImage depthOverlay;

        public ImageSource ImageSource
        {
            get
            {
                DrawingGroup dg = new DrawingGroup();
                DrawingImage di = new DrawingImage(dg);
                using (DrawingContext dc = dg.Open())
                {
                    //dc.DrawImage(image, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
                    dc.DrawImage(depthBitmap, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
                    dc.DrawImage(depthOverlay, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
                    
                }
                return di;
                //return this.image;
                //return depthBitmap;
            }
        }

        public DepthInterpreter(KinectSensor sensor)
        {

            kinectSensor = sensor;
            this.depthFrameReader = this.kinectSensor.DepthFrameSource.OpenReader();
            this.depthFrameReader.FrameArrived += this.Reader_DepthFrameArrived;
            this.depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            this.depthBitmap = new WriteableBitmap(displayWidth, displayHeight, 96.0, 96.0, format, null);
            this.depthPixelData = new byte[displayWidth * displayWidth];

            this.drawingGroup = new DrawingGroup();
            this.depthOverlay = new DrawingImage(this.drawingGroup);

            depthData = new ushort[this.depthFrameDescription.Width * this.depthFrameDescription.Height];
            depthMap = new DepthMap(depthData, displayWidth);
            
        }

        private void Reader_DepthFrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            bool depthFrameProcessed = false;

            using (DepthFrame depthFrame = e.FrameReference.AcquireFrame())
            {
                if (depthFrame != null)
                {
                    depthFrame.CopyFrameDataToArray(depthData);
                    depthMap.Update(depthData);
                    // the fastest way to process the body index data is to directly access 
                    // the underlying buffer
                    using (Microsoft.Kinect.KinectBuffer depthBuffer = depthFrame.LockImageBuffer())
                    {
                        // verify data and write the color data to the display bitmap
                        if (((this.depthFrameDescription.Width * this.depthFrameDescription.Height) == (depthBuffer.Size / this.depthFrameDescription.BytesPerPixel)) &&
                            (this.depthFrameDescription.Width == this.depthBitmap.PixelWidth) && (this.depthFrameDescription.Height == this.depthBitmap.PixelHeight))
                        {
                            // Note: In order to see the full range of depth (including the less reliable far field depth)
                            // we are setting maxDepth to the extreme potential depth threshold
                            ushort maxDepth = ushort.MaxValue;
                            ushort minDepth = ushort.MinValue;

                            // If you wish to filter by reliable depth distance, uncomment the following line:
                            //// maxDepth = depthFrame.DepthMaxReliableDistance
                            //// minDepth = depthFrame.DepthMinReliableDistance;

                            this.ProcessDepthFrameData(depthBuffer.UnderlyingBuffer, depthBuffer.Size, minDepth, maxDepth);
                            depthFrameProcessed = true;
                        }
                    }
                }
            }

            if (depthFrameProcessed)
            {
                this.RenderDepthPixels();
            }
        }

        /// <summary>
        /// Directly accesses the underlying image buffer of the DepthFrame to 
        /// create a displayable bitmap.
        /// This function requires the /unsafe compiler option as we make use of direct
        /// access to the native memory pointed to by the depthFrameData pointer.
        /// </summary>
        /// <param name="depthFrameData">Pointer to the DepthFrame image data</param>
        /// <param name="depthFrameDataSize">Size of the DepthFrame image data</param>
        /// <param name="minDepth">The minimum reliable depth value for the frame</param>
        /// <param name="maxDepth">The maximum reliable depth value for the frame</param>
        private unsafe void ProcessDepthFrameData(IntPtr depthFrameData, uint depthFrameDataSize, ushort minDepth, ushort maxDepth)
        {
            // depth frame data is a 16 bit value
            ushort* frameData = (ushort*)depthFrameData;

            // convert depth to a visual representation
            for (int i = 0; i < (int)(depthFrameDataSize / this.depthFrameDescription.BytesPerPixel); ++i)
            {
                if (depthMap[i].IsActive)
                {
                    depthPixelData[i] = byte.MaxValue;
                }
                else
                {
                    // Get the depth for this pixel
                    ushort depth = frameData[i];

                    // To convert to a byte, we're mapping the depth value to the byte range.
                    // Values outside the reliable depth range are mapped to 0 (black).
                    this.depthPixelData[i] = (byte)(depth >= minDepth && depth <= maxDepth ? (depth / (8000 / 256)) : 0);
                }
            }
        }

        /// <summary>
        /// Renders color pixels into the writeableBitmap.
        /// </summary>
        private void RenderDepthPixels()
        {
            this.depthBitmap.WritePixels(
                new Int32Rect(0, 0, this.depthBitmap.PixelWidth, this.depthBitmap.PixelHeight),
                this.depthPixelData,
                (this.depthBitmap.PixelWidth*format.BitsPerPixel+7)/8,
                0);
        }

        private void ProcessFrame(DepthFrame frame)
        {
            int width = frame.FrameDescription.Width;
            int height = frame.FrameDescription.Height;

            //ushort minDepth = 0;
            ushort minDepth = frame.DepthMinReliableDistance;

            ushort maxDepth = ushort.MaxValue;
            //ushort maxDepth = frame.DepthMaxReliableDistance;

            ushort[] depthData = new ushort[width * height];

            int stride = (width * format.BitsPerPixel + 7) / 8;
            byte[] pixelData = new byte[stride * height];

            frame.CopyFrameDataToArray(depthData);
            
            int colorIndex = 0;
            for (int depthIndex = 0; depthIndex < depthData.Length; ++depthIndex)
            {
                ushort depth = depthData[depthIndex];
                byte intensity = (byte)(depth >= minDepth && depth <= maxDepth ? depth : 0);

                pixelData[colorIndex++] = intensity; // Blue
                pixelData[colorIndex++] = intensity; // Green
                pixelData[colorIndex++] = intensity; // Red

                ++colorIndex;
            }

            if (depthBitmap == null)
            {
                this.depthBitmap = new WriteableBitmap(displayWidth, displayHeight, 96.0, 96.0,format, null);
            }

            Random r = new Random();
            r.NextBytes(pixelData);

            depthBitmap.Lock();

            Marshal.Copy(pixelData, 0, depthBitmap.BackBuffer, pixelData.Length);
            depthBitmap.AddDirtyRect(new Int32Rect(0, 0, displayWidth, displayHeight));

            depthBitmap.Unlock();

            this.depthData = depthData;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (this.depthFrameReader != null)
                    {
                        // DepthFrameReader is IDisposable
                        this.depthFrameReader.Dispose();
                        this.depthFrameReader = null;
                    }

                    if (this.kinectSensor != null)
                    {
                        this.kinectSensor.Close();
                        this.kinectSensor = null;
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~DepthInterpreter() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            //GC.SuppressFinalize(this);
        }
        #endregion
        
    }
}
