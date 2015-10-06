using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MouseApp.common
{
    using Microsoft.Kinect;

    class AnchorPointFilter
    {
        float resetThreshold;
        ulong count;
        JointType jointType;

        public bool wasReset;

        public CameraSpacePoint anchorPoint;

        public AnchorPointFilter()
        {
            Init();
        }

        public AnchorPointFilter(JointType jt)
        {
            this.jointType = jt;
            Init();
        }

        public void Init(float thresh = .1f) {
            this.resetThreshold = thresh;
            count = 0;
        }

        public void Reset(CameraSpacePoint newPoint)
        {
            anchorPoint = newPoint;
            count = 1; 
        }

        public void Reset(Body body)
        {
            CameraSpacePoint p = body.Joints[this.jointType].Position;
            Reset(p);
        }

        public void Update(CameraSpacePoint currentPoint)
        {
            wasReset = false;

            anchorPoint.X = CumulativeAverage(anchorPoint.X, currentPoint.X, this.count);
            anchorPoint.Y = CumulativeAverage(anchorPoint.Y, currentPoint.Y, this.count);
            anchorPoint.Z = CumulativeAverage(anchorPoint.Z, currentPoint.Z, this.count);

            ++count;

            if (CSVectorLength(CSVectorSubtract(anchorPoint, currentPoint)) > resetThreshold)
            {
                Reset(currentPoint);
                wasReset = true;
            }

        }

        public void Update(Body body)
        {
            CameraSpacePoint p = body.Joints[this.jointType].Position;
            Update(p);
        }

        private float CumulativeAverage(float currentAvg, float currentVal, ulong count)
        {
            float output;
            output = currentAvg * count / (count + 1f) + currentVal * (1f / (count + 1f));
            return output;
        }

        CameraSpacePoint CSVectorSubtract(CameraSpacePoint p1, CameraSpacePoint p2)
        {
            CameraSpacePoint diff = new CameraSpacePoint();

            diff.X = p1.X - p2.X;
            diff.Y = p1.Y - p2.Y;
            diff.Z = p1.Z - p2.Z;

            return diff;
        }

        float CSVectorLength(CameraSpacePoint p)
        {
            return Convert.ToSingle(Math.Sqrt(p.X * p.X + p.Y * p.Y + p.Z * p.Z));
        }
    }
}
