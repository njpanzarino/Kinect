using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;

using Microsoft.Kinect;
using Microsoft.Kinect.Input;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;

zzzzz
using WindowsInput;


namespace MouseApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private KinectSensor kinectSensor = null;
        KinectCoreWindow kinectCoreWindow;
        MouseAppSettings settings;

        private ColorFrameReader colorFrameReader = null;
        private WriteableBitmap colorBitmap = null;
        private string statusText = null;

        public event PropertyChangedEventHandler PropertyChanged;

        private BodyFrameReader bodyFrameReader = null;

        private const double HandSize = 75;
        private readonly Brush[] handBrush;
        private readonly Brush handClosedBrush = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));
        private readonly Brush handOpenBrush = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0));
        private readonly Brush handLassoBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255));
        private readonly Brush handUnknownBrush = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255));
        private readonly Brush handNotTrackedBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        private static Body[] bodies = null;
        private const float InferredZPositionClamp = 0.1f;
        private CoordinateMapper coordinateMapper = null;
        private DrawingGroup drawingGroup;
        private DrawingImage colorOverlay;
        private int displayWidth;
        private int displayHeight;
        private int deskWidth;
        private int deskHeight;

        static TimeSpan currentTime;
        static TimeSpan activeTime;
        TimeSpan handStateTime;
        //last time he hand was still
        TimeSpan handStillTime;
        Point3D handStillPoint;

        List<float> handDispBuffer;
        List<TimeSpan> handDispTimes;

        bool drifting;
        TimeSpan driftStartTime;

        private InputSimulator sim;

        List<HandState> handStates;
        List<GestureDetector> detectors;
        PHIZTracker phiz;
        public static Body activeBody;
        bool rightHanded;

        PointF unclampedPoint;
        Point3D phizPoint;
        //Mouse Point on screen normalized to range [0..1]
        PointF mousePoint;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                DrawingGroup dg = new DrawingGroup();
                DrawingImage di = new DrawingImage(dg);
                using (DrawingContext dc = dg.Open())
                {
                    dc.DrawImage(colorBitmap, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
                    dc.DrawImage(colorOverlay, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
                }
                return di;
            }
        }

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        //-----------------------------------
        //Constructor
        //-----------------------------------

        public MainWindow()
        {
            settings = new MouseAppSettings(true);

            //get the kinect
            this.kinectSensor = KinectSensor.GetDefault();

            //open the reader for color frames
            this.colorFrameReader = this.kinectSensor.ColorFrameSource.OpenReader();
            //wire handler for frame arrival
            this.colorFrameReader.FrameArrived += this.Reader_ColorFrameArrived;
            //create the colorFrameDescription from the ColorFrameSource
            FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);
            // create the bitmap to display
            this.colorBitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);

            // open the reader for the body frames
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();
            this.bodyFrameReader.FrameArrived += this.Reader_BodyFrameArrived;
            // get the coordinate mapper
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;
            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();
            // Create an image source that we can use in our image control
            this.colorOverlay = new DrawingImage(this.drawingGroup);

            // get size of color space
            this.displayWidth = colorFrameDescription.Width;
            this.displayHeight = colorFrameDescription.Height;

            this.deskHeight = Screen.PrimaryScreen.Bounds.Height;
            this.deskWidth = Screen.PrimaryScreen.Bounds.Width;

            handDispBuffer = new List<float>();
            handDispTimes = new List<TimeSpan>();

            handStates = new List<HandState>();

            detectors = new List<GestureDetector>();
            int maxBodies = this.kinectSensor.BodyFrameSource.BodyCount;
            for (int i = 0; i < maxBodies; ++i)
            {
                GestureDetector detector = new GestureDetector(this.kinectSensor, "TwistWrist");
                detectors.Add(detector);
            }

            activeTime = TimeSpan.MinValue;
            handStateTime = TimeSpan.MinValue;
            driftStartTime = TimeSpan.MinValue;

            handStillPoint = new Point3D();

            //Mouse cursor and PHIZ attributes
            phiz = new PHIZTracker();

            kinectCoreWindow = KinectCoreWindow.GetForCurrentThread();
            kinectCoreWindow.PointerMoved += kinectCoreWindow_PointerMoved;

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();
            // use the window object as the view model in this simple example
            this.DataContext = this;
            // set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.NoSensorStatusText;
            handBrush = new Brush[5];
            handBrush[(int)(HandState.Closed)] = handClosedBrush;
            handBrush[(int)(HandState.Open)] = handOpenBrush;
            handBrush[(int)(HandState.Lasso)] = handLassoBrush;
            handBrush[(int)(HandState.Unknown)] = handUnknownBrush;
            handBrush[(int)(HandState.NotTracked)] = handNotTrackedBrush;

            InitializeComponent();

            this.OptionsColumn.Width = new System.Windows.GridLength(140);
            this.OptionsRow.Height = new System.Windows.GridLength(35);

            UpdateBoundaries();

            sim = new InputSimulator();
        }

        //-----------------------------------
        //Helper Functions
        //-----------------------------------

        private bool UpdateHandState(HandState state)
        {
            int maxStates = 3;

            if (state == HandState.NotTracked || state == HandState.Unknown)
            {
                handStateTime = currentTime;
                return false;
            }

            if (handStates.Count < maxStates)
            {
                handStateTime = currentTime;
                handStates.Insert(0, state);
                return true;
            }
            else if (handStates[0] != state)
            {
                handStateTime = currentTime;
                handStates.RemoveAt(handStates.Count - 1);
                handStates.Insert(0, state);
                return true;
            }

            return false;

        }

        private void DoMouseAction(List<HandState> list)
        {
            int[] pressLeft = { (int)HandState.Closed, (int)HandState.Open };
            int[] releaseLeft = { -1, (int)HandState.Closed, (int)HandState.Open };
            int[] pressRight = { (int)HandState.Closed, (int)HandState.Lasso };
            int[] releaseRight = { -1, (int)HandState.Closed, (int)HandState.Lasso };

            if (StatesMatch(list, pressLeft))
            {
                //InputSender.PressLeftMouseButton();
                sim.Mouse.LeftButtonDown();
            }
            else if (StatesMatch(list, releaseLeft))
            {
                //InputSender.ReleaseLeftMouseButton();
                sim.Mouse.LeftButtonUp();
            }
            else if (StatesMatch(list, pressRight))
            {
                //InputSender.PressRightMouseButton();
                sim.Mouse.RightButtonDown();
            }
            else if (StatesMatch(list, releaseRight))
            {
                //InputSender.ReleaseRightMouseButton();
                sim.Mouse.RightButtonUp();
            }
        }

        private bool StatesMatch(List<HandState> list, int[] ints)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (i < ints.Length)
                {
                    if ((int)list[i] != ints[i] && ints[i] != -1)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        //Moves mouse to Specifified location on screen
        //input range is [0..1]
        private void MoveMouseTo(float dx, float dy)
        {
            //InputSender.MoveMouseTo(Convert.ToInt32(dx * ushort.MaxValue), Convert.ToInt32(dy * ushort.MaxValue));
            sim.Mouse.MoveMouseTo(dx * ushort.MaxValue, dy * ushort.MaxValue);
        }

        //Moves mouse by specified amount relative to previous position 
        //input range is [0..1]
        private void MoveMouse(float dx, float dy)
        {
            //InputSender.MoveMouse(Convert.ToInt32(dx * ushort.MaxValue), Convert.ToInt32(dy * ushort.MaxValue));
            sim.Mouse.MoveMouseBy(Convert.ToInt32(dx * this.deskWidth), Convert.ToInt32(dy * this.deskHeight));
        }

        public void updateImage()
        {
            using (DrawingContext dc = this.drawingGroup.Open())
            {
                //dc.DrawImage(colorBitmap, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));

                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));

                if (settings.printDebug)
                {
                    //Draw ellipse around PHIZ coordinates
                    dc.DrawEllipse(Brushes.Black, null, new Point(unclampedPoint.X * this.displayWidth, unclampedPoint.Y * this.displayHeight), HandSize, HandSize);
                }

                if ((activeBody != null) && settings.printDebug==true)
                {
                    // dc.DrawEllipse(Brushes.Black, null, new Point(unclampedPoint.X*this.displayWidth/1.5, unclampedPoint.Y*this.displayHeight/2+this.displayHeight/4),HandSize,HandSize);
                    dc.DrawEllipse(Brushes.HotPink, null, new Point(phizPoint.X * displayWidth, phizPoint.Y * displayHeight), HandSize * .75, HandSize * .75);

                    phiz.DrawAxis(coordinateMapper, dc);

                    //Draw square at anchor points
                    ColorSpacePoint p1 = this.coordinateMapper.MapCameraPointToColorSpace(phiz.AnchorHead.anchorPoint);
                    Rect rect1 = new Rect(new Point(p1.X - 15, p1.Y - 15), new Size(30, 30));
                    dc.DrawRectangle(Brushes.Brown, null, rect1);

                    ColorSpacePoint p2 = this.coordinateMapper.MapCameraPointToColorSpace(phiz.AnchorShoulderLeft.anchorPoint);
                    Rect rect2 = new Rect(new Point(p2.X - 15, p2.Y - 15), new Size(30, 30));
                    dc.DrawRectangle(Brushes.Brown, null, rect2);

                    ColorSpacePoint p3 = this.coordinateMapper.MapCameraPointToColorSpace(phiz.AnchorShoulderRight.anchorPoint);
                    Rect rect3 = new Rect(new Point(p3.X - 15, p3.Y - 15), new Size(30, 30));
                    dc.DrawRectangle(Brushes.Brown, null, rect3);
                }
                
                foreach (Body body in bodies)
                {

                    if (body.IsTracked)
                    {

                        IReadOnlyDictionary<JointType, Joint> joints = body.Joints;

                        // convert the joint points to depth (display) space
                        Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();

                        foreach (JointType jointType in joints.Keys)
                        {
                            // sometimes the depth(Z) of an inferred joint may show as negative
                            // clamp down to 0.1f to prevent coordinatemapper from returning (-Infinity, -Infinity)
                            CameraSpacePoint position = joints[jointType].Position;
                            if (position.Z < 0)
                            {
                                position.Z = InferredZPositionClamp;
                            }

                            DepthSpacePoint depthSpacePoint = this.coordinateMapper.MapCameraPointToDepthSpace(position);
                            ColorSpacePoint colorSpacePoint = this.coordinateMapper.MapCameraPointToColorSpace(position);

                            //jointPoints[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);
                            jointPoints[jointType] = new Point(colorSpacePoint.X, colorSpacePoint.Y);
                        }

                        if (settings.printHands)
                        {
                            this.DrawHand(body.HandLeftState, jointPoints[JointType.HandLeft], dc);
                            this.DrawHand(body.HandRightState, jointPoints[JointType.HandRight], dc);
                        }
                    }

                }

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
            }
        }

        public void updateModel()
        {
            int i = 0;
            foreach (Body body in bodies)
            {
                if (activeBody != null && body.TrackingId.Equals(activeBody.TrackingId))
                {
                    activeBody = body;
                    bool handStateChanged = UpdateHandState(body.HandRightState);
                    mousePoint.X = System.Windows.Forms.Cursor.Position.X / ushort.MaxValue;
                    mousePoint.Y = System.Windows.Forms.Cursor.Position.Y / ushort.MaxValue;

                    phiz.updatePHIZ(body);
                    Point3D currentPoint = phiz.RightPHIZPoint(phiz.filter.GetFilteredJoint(JointType.HandRight));
                    Vector3D diffVect = (currentPoint - phizPoint);
                    UpdateHandDispBuffer(Convert.ToSingle(diffVect.Length),currentTime);
                    if (handDispBuffer.Average() < settings.stillThresh)
                    {
                        handStillTime = currentTime;
                        handStillPoint = currentPoint;
                    }

                    if (settings.relativeMode)
                    {
                        Point3D center = new Point3D(.5, .5, currentPoint.Z);
                        Vector3D disp = (currentPoint - center);

                        if (disp.Length > settings.driftRadius)
                        {
                            float driftMult = (Convert.ToSingle(disp.Length) - settings.driftRadius) * .03f;
                            diffVect.X += settings.sensitivity * driftMult * disp.X / disp.Length;
                            diffVect.Y += settings.sensitivity * driftMult * disp.Y / disp.Length;
                            diffVect.Z += settings.sensitivity * driftMult * disp.Z / disp.Length;
                            if(drifting == false)
                            {
                                driftStartTime = currentTime;
                                drifting = true;
                            }
                        }
                        else
                        {
                            drifting = false;
                        }
                            
                        if (((currentTime - handStillTime).TotalMilliseconds > settings.stillLagMillis)||((drifting==true)&&((currentTime - driftStartTime).TotalMilliseconds > settings.stillTimeMillis))||
                            ((currentPoint-handStillPoint).Length>settings.moveThresh))
                        {
                            if (handStates[0] == HandState.Lasso)
                            {
                                float scrollMult = 150;
                                sim.Mouse.HorizontalScroll(Convert.ToInt32(diffVect.X * scrollMult * settings.sensitivity));
                                sim.Mouse.VerticalScroll(Convert.ToInt32(-diffVect.Y * scrollMult * settings.sensitivity));
                            }
                            else
                                MoveMouse(Convert.ToSingle(diffVect.X * settings.sensitivity), Convert.ToSingle(diffVect.Y * settings.sensitivity));
                        }
                    }
                    else
                    {
                        mousePoint = new PointF();
                        mousePoint.X = Convert.ToSingle(phizPoint.X);
                        mousePoint.Y = Convert.ToSingle(phizPoint.Y);

                        if ((currentTime - handStillTime).TotalMilliseconds > settings.stillLagMillis)
                        {
                            if (handStates[0] == HandState.Lasso)
                            {
                                float scrollMult = 100;
                                sim.Mouse.HorizontalScroll(Convert.ToInt32(-diffVect.X * scrollMult));
                                sim.Mouse.VerticalScroll(Convert.ToInt32(diffVect.Y * scrollMult));
                            }
                            MoveMouseTo(mousePoint.X, mousePoint.Y);
                        }
                    }

                    phizPoint = currentPoint;

                    if (body.IsTracked && handStateChanged)
                    {
                        DoMouseAction(handStates);
                    }
                }

                if (body.IsTracked)
                {
                    ulong trackingId = body.TrackingId;

                    // if the current body TrackingId changed, update the corresponding gesture detector with the new value
                    if (trackingId != detectors[i].TrackingId)
                    {
                        this.detectors[i].TrackingId = trackingId;

                        // if the current body is tracked, unpause its detector to get VisualGestureBuilderFrameArrived events
                        // if the current body is not tracked, pause its detector so we don't waste resources trying to get invalid gesture results
                        this.detectors[i].IsPaused = trackingId == 0;
                    }

                    ++i;
                }

            }
        }

        public bool UpdateHandDispBuffer(float f, TimeSpan time)
        {
            handDispBuffer.Add(f);
            handDispTimes.Add(time);
            if ((time - handDispTimes[0]).TotalMilliseconds > settings.stillTimeMillis)
            {
                handDispBuffer.RemoveAt(0);
                handDispTimes.RemoveAt(0);
                return true;
            }
            return false;
        }

        public static void GestureRecieved(string name, ulong bodyTrackingID, bool detected, float confidence)
        {
            //If detected gesture above confidence, set active/inactive
            if (detected == true && confidence > .2)
            {
                //if no body is active, set active body
                if (activeBody == null)
                {
                    foreach (Body b in bodies)
                    {
                        if (b.TrackingId == bodyTrackingID)
                        {
                            //only modify state if no active gesure in past second
                            if ((currentTime - activeTime).TotalMilliseconds > 1000)
                            {
                                SetActiveBody(b);
                            }
                        }
                    }
                }

                //if a body is active, and gesture is found on same body, deactivate body
                else if (bodyTrackingID == activeBody.TrackingId)
                {
                    //only modify state if no active gesure in past second
                    if ((currentTime - activeTime).TotalMilliseconds > 1000)
                    {
                        SetActiveBody(null);
                    }
                }

                //record last time a valid gesture was recieved
                activeTime = currentTime;
            }
        }

        public static void SetActiveBody(Body b)
        {
            if (b == null)
            {
                activeBody = null;
                Debug.WriteLine("inactive");
            }

            else
            {
                activeBody = b;
                Debug.WriteLine("active");
            }
        }

        /// </summary>
        /// <param name="handState">state of the hand</param>
        /// <param name="handPosition">position of the hand</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawHand(HandState handState, Point handPosition, DrawingContext drawingContext)
        {
            if (handState != HandState.NotTracked)
            {
                drawingContext.DrawEllipse(this.handBrush[(int)handState], null, handPosition, HandSize, HandSize);
            }
        }
        
        //-----------------------------------
        //Event Handlers
        //-----------------------------------

        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.SensorNotAvailableStatusText;
        }

        private void slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            settings.sensitivity = Convert.ToSingle(slider.Value);
            textBlock.Text = "Sensitivity: "+settings.sensitivity.ToString("F2");
        }

        private void slider1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            settings.driftRadius = Convert.ToSingle(slider1.Value);
            textBlock1.Text = "Drift Radius: " + settings.driftRadius.ToString("F2");
        }

        /// <summary>
        /// Handles the color frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_ColorFrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            // ColorFrame is IDisposable
            using (ColorFrame colorFrame = e.FrameReference.AcquireFrame())
            {
                if (colorFrame != null)
                {
                    FrameDescription colorFrameDescription = colorFrame.FrameDescription;

                    using (KinectBuffer colorBuffer = colorFrame.LockRawImageBuffer())
                    {
                        this.colorBitmap.Lock();

                        // verify data and write the new color frame data to the display bitmap
                        if ((colorFrameDescription.Width == this.colorBitmap.PixelWidth) && (colorFrameDescription.Height == this.colorBitmap.PixelHeight))
                        {
                            colorFrame.CopyConvertedFrameDataToIntPtr(
                                this.colorBitmap.BackBuffer,
                                (uint)(colorFrameDescription.Width * colorFrameDescription.Height * 4),
                                ColorImageFormat.Bgra);

                            this.colorBitmap.AddDirtyRect(new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight));
                        }

                        // get size of joint space
                        this.displayWidth = colorFrameDescription.Width;
                        this.displayHeight = colorFrameDescription.Height;

                        this.colorBitmap.Unlock();
                    }
                }
            }
        }

        /// <summary>
        /// Handles the body frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_BodyFrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (bodies == null)
                    {
                        bodies = new Body[bodyFrame.BodyCount];
                    }

                    phiz.up = bodyFrame.FloorClipPlane;

                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(bodies);
                    dataReceived = true;

                    currentTime = bodyFrame.RelativeTime;
                    if (activeTime.Equals(TimeSpan.MinValue))
                    {
                        activeTime = currentTime;
                    }
                    if (handStateTime.Equals(TimeSpan.MinValue))
                    {
                        handStateTime = currentTime;
                    }
                    if (driftStartTime.Equals(TimeSpan.MinValue))
                    {
                        driftStartTime = currentTime;
                    }
                }
            }

            if (dataReceived)
            {
                updateModel();
                updateImage();
            }
        }

        public void kinectCoreWindow_PointerMoved(object sender, KinectPointerEventArgs args)
        {
            //TODO: move mouse. Based on previous hand position. changes in hand state should disable movement for a short time?
            KinectPointerPoint kinectPointerPoint = args.CurrentPoint;
            unclampedPoint=kinectPointerPoint.Properties.UnclampedPosition;
        }
        
        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                // BodyFrameReader is IDisposable
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

            if (this.colorFrameReader != null)
            {
                // ColorFrameReder is IDisposable
                this.colorFrameReader.Dispose();
                this.colorFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }
        
        private void checkBox_MotionType_Checked(object sender, RoutedEventArgs e)
        {
            if (checkBox_MotionType.IsChecked.HasValue)
            {
                settings.relativeMode = !(bool)checkBox_MotionType.IsChecked;
            }
            else
                settings.relativeMode = true;
        }

        private void checkBox1_Checked(object sender, RoutedEventArgs e)
        {
            if (checkBox1.IsChecked.HasValue)
            {
                settings.printDebug = (bool)checkBox1.IsChecked;
            }
            else
                settings.printDebug = false;
        }

        //-----------------------------------
        //Debugging
        //-----------------------------------

        private void printPhizPos()
        {
            Debug.Write("X:");
            Debug.Write(phizPoint.X);
            Debug.Write("   Y:");
            Debug.Write(phizPoint.Y);
            Debug.Write("   Z:");
            Debug.WriteLine(phizPoint.Z);
        }

        public void UpdateBoundaries()
        {
            slider.Value = settings.sensitivity;
            slider1.Value = settings.driftRadius;
            checkBox_MotionType.IsChecked = !settings.relativeMode;
            checkBox1.IsChecked = settings.printDebug;
        }

        public struct MouseAppSettings
        {
            public bool relativeMode;
            public bool printDebug;
            public bool printHands;

            public bool pauseWhenStill;
            public int handDispBufferSize;
            public int handDispBufferCounter;
            public int stillTimeMillis;
            public int stillLagMillis;
            public float stillThresh;
            public float moveThresh;

            public float sensitivity;
            public float driftRadius;

            public MouseAppSettings(bool debug = true)
            {
                relativeMode = true;
                printDebug = debug;
                printHands = true;

                pauseWhenStill = true;
                handDispBufferSize = 25;
                handDispBufferCounter = 0;
                stillTimeMillis = 750;
                stillLagMillis = 750;
                stillThresh = .0015f;
                moveThresh = .2f;

                sensitivity = 1f;
                driftRadius = .3f;
            }
        }

        
    }

    
}
