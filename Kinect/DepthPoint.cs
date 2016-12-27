using System.Windows;

namespace KinectMouseClickPrototype.Kinect
{
    public class DepthPoint
    {
        public float X { get; set; }

        public float Y { get; set; }

        public float Z { get; set; }

        public DepthPoint(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Point GetPoint()
        {
            return new Point(X, Y);
        }
    }
}
