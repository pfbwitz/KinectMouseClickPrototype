using System.Windows;

namespace KinectMouseClickPrototype
{
    public class DepthPoint
    {
        public int X { get; set; }

        public int Y { get; set; }

        public int Z { get; set; }

        public DepthPoint(int x, int y, int z)
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
