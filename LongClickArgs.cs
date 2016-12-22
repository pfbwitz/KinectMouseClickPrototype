using System;

namespace KinectMouseClickPrototype
{
    public class LongClickArgs : EventArgs
    {
        public int X { get; private set; }

        public int Y { get; private set; }

        public LongClickArgs(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}
