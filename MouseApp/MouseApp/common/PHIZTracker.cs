using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

using System.Diagnostics;


namespace MouseApp.common
{
    using Microsoft.Kinect;
    using System.Windows.Media.Media3D;

    //--------------------------------------------------------------------------------------
    // PHIZTracker.cs
    //
    // This file is used to create a stable PHIZ(s) around a single body and to filter and track
    // hand postion within the appropriate PHIZ.
    //--------------------------------------------------------------------------------------
    class PHIZTracker
    {
        Body body;
        public Vector4 up;
        public KinectJointFilter filter { get; private set; }

        public AnchorPointFilter AnchorShoulderLeft { get; private set; }
        public AnchorPointFilter AnchorShoulderRight { get; private set; }
        public AnchorPointFilter AnchorHead { get; private set; }

        private float HEIGHT;
        private float WIDTH;

        private float X_SHOULDER_OFFSET;
        private float Y_SHOULDER_OFFSET;
        private float Z_SHOULDER_OFFSET;

        public PHIZTracker()
        {
            filter = new KinectJointFilter();

            AnchorShoulderLeft = new AnchorPointFilter(JointType.ShoulderLeft);
            AnchorShoulderRight = new AnchorPointFilter(JointType.ShoulderRight);
            AnchorHead = new AnchorPointFilter(JointType.Head);

            body = null;

        }

        public void updatePHIZ(Body newBody)
        {
            if (newBody == null)
            {
                return;
            }

            if (this.body==null || newBody.TrackingId != body.TrackingId)
            {
                this.body = newBody;
                Reset();
            }

            filter.UpdateFilter(this.body);

            AnchorShoulderLeft.Update(this.body);
            AnchorShoulderRight.Update(this.body);
            AnchorHead.Update(this.body);

            if (AnchorShoulderLeft.wasReset || AnchorShoulderRight.wasReset)
            {
                AnchorShoulderLeft.Reset(this.body);
                AnchorShoulderRight.Reset(this.body);
                AnchorHead.Reset(this.body);
            }

            Vector3D right = ToVector3D(AnchorShoulderRight.anchorPoint);
            Vector3D left = ToVector3D(AnchorShoulderLeft.anchorPoint);
            Vector3D shoulders = left - right;

            this.WIDTH = Convert.ToSingle(shoulders.Length);

            Vector3D center = new Vector3D();
            center.X = (left.X + right.X) / 2;
            center.Y = (left.Y + right.Y) / 2;
            center.Z = (left.Z + right.Z) / 2;

            this.HEIGHT = Convert.ToSingle((ToVector3D(AnchorHead.anchorPoint) - center).Length)*2;

            X_SHOULDER_OFFSET = WIDTH*.5f;
            Y_SHOULDER_OFFSET = HEIGHT*.25f;
            Z_SHOULDER_OFFSET = WIDTH*.25f;
            
        }

        private void Reset()
        {
            filter.Reset();
        }

        public Point3D MapCameraSpacePointToPHIZSpace(CameraSpacePoint p, Matrix3D phizTrans)
        {
            Point3D input = new Point3D();
            input.X = p.X;
            input.Y = p.Y;
            input.Z = p.Z;

            Point3D output = phizTrans.Transform(input);

            output.X = (output.X / (WIDTH));
            output.Y = (output.Y / (HEIGHT));

            return output;
        }

        public Vector3D RightPHIZCenter()
        {
            return GetOffset(RightPHIZTransform());
        }

        public Point3D RightPHIZPoint(CameraSpacePoint p)
        {
            Matrix3D trans = RightPHIZTransform();
            trans.Invert();
            return MapCameraSpacePointToPHIZSpace(p, trans);
        }

        //--------------------------------------------------------------------------------------
        //Helper functions for managing 3D Objects used to perform transformations
        //--------------------------------------------------------------------------------------
        public static Vector3D ToVector3D(CameraSpacePoint point)
        {
            return new Vector3D(point.X, point.Y, point.Z);
        }

        public static Vector3D ToVector3D(Vector4 vect)
        {
            return new Vector3D(vect.X, vect.Y, vect.Z);
        }

        public static CameraSpacePoint ToVectorCS(Vector3D vect)
        {
            CameraSpacePoint point = new CameraSpacePoint();
            point.X = Convert.ToSingle(vect.X);
            point.Y = Convert.ToSingle(vect.Y);
            point.Z = Convert.ToSingle(vect.Z);

            return point;
        }

        private Vector3D Translation(Matrix3D matrix, Vector3D vect)
        {
            Vector3D output = new Vector3D();

            output = GetAxisDir(vect, matrix);
            output = Vector3D.Multiply(vect.Length, output);

            return output;
        }

        private Quaternion AlignRotation(Vector3D from, Vector3D to)
        {
            Vector3D cross = Vector3D.CrossProduct(from, to);
            double ang = Vector3D.AngleBetween(from, to);
            Quaternion rot = new Quaternion(cross, ang);

            return rot;
        }

        private Matrix3D AlignRotationMatrix(Vector3D from, Vector3D to)
        {
            Matrix3D output = Matrix3D.Identity;

            output.Rotate(AlignRotation(from,to));

            return output;
        }

        private Quaternion AlignAxisRotation(Vector3D axis, Vector3D from, Vector3D to)
        {
            double f1, f2, t1, t2;

            Vector3D v1 = Vector3D.CrossProduct(axis, Vector3D.Add(new Vector3D(1, 1, 1), axis));
            Vector3D v2 = Vector3D.CrossProduct(axis, v1);

            f1 = Vector3D.DotProduct(from, v1) / from.Length;
            f2 = Vector3D.DotProduct(from, v2) / from.Length;
            t1 = Vector3D.DotProduct(to, v1) / to.Length;
            t2 = Vector3D.DotProduct(to, v2) / to.Length;

            double a1 = Math.Atan2(f1, f2);
            double a2 = Math.Atan2(t1, t2);

            double ang = a2 - a1;
            ang = 180 * ang / Math.PI;
            Quaternion rot = new Quaternion(axis, ang);

            return rot;
        }

        private Matrix3D AlignAxisRotationMatrix(Vector3D axis, Vector3D from, Vector3D to)
        {
            Matrix3D output = Matrix3D.Identity;

            output.Rotate(AlignAxisRotation(axis,from,to));

            return output;
        }

        private double AngleToPlane(Vector3D plane, Vector3D vect)
        {
            return 90 - Vector3D.AngleBetween(plane, vect);
        }

        private Vector3D GetAxisDir(Vector3D axis, Matrix3D matrix)
        {
            Matrix3D inv = matrix;
            inv.Invert();
            Vector3D output=inv.Transform(axis);
            output.Normalize();
            return output;
        }

        private Vector3D GetOffset(Matrix3D matrix)
        {
            return new Vector3D(matrix.OffsetX, matrix.OffsetY, matrix.OffsetZ);
        }

        private Point3D GetPosition(Matrix3D matrix)
        {
            return new Point3D(matrix.OffsetX, matrix.OffsetY, matrix.OffsetZ);
        }

        private Vector3D BodyFacing()
        {
            Vector3D output;

            Vector3D right = ToVector3D(AnchorShoulderRight.anchorPoint);
            Vector3D left = ToVector3D(AnchorShoulderLeft.anchorPoint);
            Vector3D shoulders = left - right;

            output = Vector3D.CrossProduct(ToVector3D(up), shoulders);
            output.Normalize();

            return output;
        }

        private Matrix3D RightPHIZTransform()
        {
            Matrix3D output = Matrix3D.Identity;

            //Debug.WriteLine(output);

            //Translate Coordinate frame from Camera origin to right shoulder
            output.Translate(ToVector3D(AnchorShoulderRight.anchorPoint));

            //Debug.WriteLine(output);

            //Rotate Coordinate Frame so that Z axis points away from body and Y axis points straight up
            Vector3D outZAxis = GetAxisDir(new Vector3D(0, 0, 1), output);
            output.RotateAt(AlignRotation(outZAxis, BodyFacing()), GetPosition(output));
            Vector3D to = output.Transform(ToVector3D(up));
            to.Negate();
            to.Normalize();
            output.RotateAt(AlignAxisRotation(new Vector3D(1, 0, 0), new Vector3D(0, 1, 0), to),GetPosition(output));

            //Debug.WriteLine(output);

            //Translate Coordinate frame away from body, to center point of PHIZ
            output.Translate(Translation(output,new Vector3D(X_SHOULDER_OFFSET, -Y_SHOULDER_OFFSET, Z_SHOULDER_OFFSET)));
            
            //Debug.WriteLine(output);

            //Point Coordinate frame in direction of camera, rotating only on Y Axis
            to = output.Transform(GetOffset(output));
            to.Negate();

            output.RotateAt(AlignAxisRotation(new Vector3D(0, 1, 0), new Vector3D(0, 0, 1), to),GetPosition(output));

            //Debug.WriteLine(output);

            output.Translate(Translation(output,new Vector3D(-WIDTH/2, -HEIGHT/2, 0)));

            return output;
        }

        //--------------------------------------------------------------------------------------
        //Helper functions for imaging and debuging
        //--------------------------------------------------------------------------------------

        public void DrawAxis(CoordinateMapper coordinateMapper, DrawingContext drawingContext)
        {
            Matrix3D t = RightPHIZTransform();
            ColorSpacePoint p = coordinateMapper.MapCameraPointToColorSpace(PHIZTracker.ToVectorCS(GetOffset(t)));
            ColorSpacePoint px = coordinateMapper.MapCameraPointToColorSpace(ToVectorCS(Vector3D.Add(GetOffset(t),
                Vector3D.Multiply(.5,GetAxisDir(new Vector3D(1, 0, 0), t)))));
            ColorSpacePoint py = coordinateMapper.MapCameraPointToColorSpace(ToVectorCS(Vector3D.Add(GetOffset(t),
                Vector3D.Multiply(.5, GetAxisDir(new Vector3D(0, 1, 0), t)))));
            ColorSpacePoint pz = coordinateMapper.MapCameraPointToColorSpace(ToVectorCS(Vector3D.Add(GetOffset(t),
                Vector3D.Multiply(.5, GetAxisDir(new Vector3D(0, 0, 1), t)))));

            drawingContext.DrawEllipse(Brushes.Aqua, null, new Point(p.X, p.Y), 30, 30);
            drawingContext.DrawLine(new Pen(Brushes.Red, 15), new Point(p.X, p.Y), new Point(px.X, px.Y));
            drawingContext.DrawLine(new Pen(Brushes.Green, 15), new Point(p.X, p.Y), new Point(py.X, py.Y));
            drawingContext.DrawLine(new Pen(Brushes.Blue, 15), new Point(p.X, p.Y), new Point(pz.X, pz.Y));
        }

    }
}
