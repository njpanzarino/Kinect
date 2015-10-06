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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SideViewApp
{
    using Microsoft.Kinect;
    using SideViewApp.common;
    using System.Diagnostics;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        KinectSensor kinectSensor = KinectSensor.GetDefault();
        private DepthFrameReader depthFrameReader = null;
        private BodyFrameReader bodyFrameReader = null;

        DepthInterpreter depth = null;

        Point MousePointOnImage;
        private Vector4 up;

        private bool _sensorMoved = true;
        bool needsCal {
            get
            {
                return _sensorMoved;
            }
            set
            {
                _sensorMoved = value;
            }
        }
        public string CalStatus {
            get
            {
                if (needsCal)
                {
                    return "Needs Calibration";
                }
                else
                    return "Calibrated";
            }
        }

        public MainWindow()
        {
            this.depthFrameReader = this.kinectSensor.DepthFrameSource.OpenReader();
            this.depthFrameReader.FrameArrived += this.Reader_FrameArrived;
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();

            this.bodyFrameReader.FrameArrived += this.Reader_BodyFrameArrived;

            depth = new DepthInterpreter(kinectSensor);

            InitializeComponent();

            kinectSensor.Open();

        }

        private void Reader_BodyFrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            using(BodyFrame frame = e.FrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    Vector4 temp = frame.FloorClipPlane;

                    if (((temp.X - up.X) * (temp.X - up.X) + (temp.Y - up.Y) * (temp.Y - up.Y)
                        + (temp.Z - up.Z) * (temp.Z - up.Z)) > .1)
                    {
                        this.needsCal = true;
                    }

                    up = temp;
                }
            }
        }

        private void Reader_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            if(MouseInfoText.Text!="No Mouse")
            {
                updateMouseInfo(MousePointOnImage);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            depth.Dispose();

            if (this.depthFrameReader != null)
            {
                // DepthFrameReader is IDisposable
                this.depthFrameReader.Dispose();
                this.depthFrameReader = null;
            }

            if (this.bodyFrameReader != null)
            {
                // DepthFrameReader is IDisposable
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        public ImageSource ImageSource
        {
            get
            {
                DrawingGroup dg = new DrawingGroup();
                DrawingImage di = new DrawingImage(dg);
                using (DrawingContext dc = dg.Open())
                {
                    dc.DrawImage(depth.ImageSource, new Rect(0.0, 0.0, depth.displayWidth, depth.displayHeight));
                    //dc.DrawEllipse(Brushes.Aqua, null, new Point(50, 50), 80, 80);
                }
                return di;
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            MousePointOnImage = e.GetPosition(screenImage);
            updateMouseInfo(MousePointOnImage);
        }

        private void updateMouseInfo(Point p)
        {
            MouseInfoText.Text = p.X.ToString("F2") + " , " + p.Y.ToString("F2") + " : ";
            if ((p.X < +depth.displayWidth) && p.X > 0 && 
                (p.Y < depth.displayHeight) && p.Y > 0)
            {
                MouseInfoText.Text += depth.depthMap[(int)p.X, (int)p.Y].currentDepth.ToString("F2") +"\n" +
                    depth.depthMap[(int)p.X,(int)p.Y].baseDepth.ToString("F2");
            }
            else
            {
                MouseInfoText.Text = "No Mouse";
            }
        }

        private void calButton_Click(object sender, RoutedEventArgs e)
        {
            depth.depthMap.acquireBase();
            
            //Calibrate
            if (needsCal)
            {
                //TODO: Open and display window in new thread?
                //expose interpreter and/or color image?
                //
                AutoCalWindow calWindow;
                calWindow = new AutoCalWindow();
                calWindow.BeginInit();
                while (!calWindow.IsInitialized) { };

                calWindow.Visibility = Visibility.Visible;

                calWindow.runAutoCal();

                while (calWindow.IsActive)
                {

                }
            }
            
            needsCal = !needsCal;
            calButton.Content = CalStatus;
        }
    }
}
