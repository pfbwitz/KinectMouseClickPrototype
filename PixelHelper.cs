using System;
using System.Windows;

namespace KinectMouseClickPrototype
{
    public static class PixelHelper
    {
        public static int ToPixelsX(double x, MainWindow mainWindow)
        {
            var topleft = new Point(367, 112);
            var topRight = new Point(144, 112);

            var width = mainWindow.ScreenTopLeft.X - mainWindow.ScreenBottomRight.X;
            var diffFromZero = x - mainWindow.ScreenBottomRight.X;
            var factor = diffFromZero / width;
            var pixelDistance = factor * SystemParameters.PrimaryScreenWidth;
            return Convert.ToInt32(SystemParameters.PrimaryScreenWidth - pixelDistance);
        }

        public static int ToPixelsY(float y, MainWindow mainWindow)
        {
            var height = mainWindow.ScreenBottomRight.Y - mainWindow.ScreenTopLeft.Y;

            var diffFromZero = y - mainWindow.ScreenTopLeft.Y;

            var factor = diffFromZero / height;

            var pixelDistance = factor * SystemParameters.PrimaryScreenHeight;

            return Convert.ToInt32(pixelDistance);
        }
    }
}
