using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Fleck;
using KinectMouseClickPrototype.Cursor;
using KinectMouseClickPrototype.Enums;
using KinectMouseClickPrototype.Kinect;
using Microsoft.Kinect;

namespace KinectMouseClickPrototype
{
    public partial class MainWindow :  INotifyPropertyChanged
    {

//        topright:
//X = 347
//Y= 143
//Z = 2372

//bottomleft:
//X = 226
//Y = 227
//Z = 2132

//347 - 226 = 121 cm width
//84 cm height

//widthFactor 1920 / 121 = 15.87
//heightFactor = 1080 / 84 = 12.86

//(276-226) * 16 = 793,5px 
//(measuredx-zerox) * xrealuation - 

        [DllImport("User32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        #region screen configuration

        private double _zLeft;

        private double _zRight;

        private bool _doneCalibrating;

        private bool _calibratingTopLeft;

        private bool _calibratingBottomRight;

        private bool _calibrating;

        public Point ScreenTopLeft{ get; set; }

        public Point ScreenTopRight
        {
            get
            {
                return new Point(ScreenBottomRight.X, ScreenTopLeft.Y);
            }
        }

        public Point ScreenBottomRight { get; set; }

        public Point ScreenBottomLeft
        {
            get
            {
                return new Point(ScreenTopLeft.X, ScreenBottomRight.Y);
            }
        }

        public double WallZ
        {
            get { return (_zLeft + _zRight)/2; }
        }

        public double ScreenWidth
        {
            get { return ScreenTopLeft.X - ScreenBottomRight.X; }
        }

        public double ScreenHeight
        {
            get { return ScreenBottomRight.Y - ScreenTopLeft.Y; }
        }

       

        #endregion

        #region constants

        private const int MapDepthToByte = 8000 / 256; // Map depth range to byte range

        #endregion

        #region private properties

        //Peter
        private bool _isLeftHeldDown;

        private bool _isRightHeldDown;

        //EndPeter

        private DepthFrameReader _depthFrameReader; // Reader for depth frames

        private readonly FrameDescription _depthFrameDescription; // Description of the data contained in the depth frame

        private readonly WriteableBitmap _depthBitmap; // Bitmap to display

        private readonly byte[] _depthPixels; // Intermediate storage for frame data converted to color

        private const double HandSize = 5; // Radius of drawn hand circles

        private const double JointThickness = 3; // Thickness of drawn joint lines

        private const double ClipBoundsThickness = 10; // Thickness of clip edge rectangles
         
        private const float InferredZPositionClamp = 0.1f; // Constant for clamping Z values of camera space points from being negative

        private readonly Brush _handClosedBrush = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0)); // Brush used for drawing hands that are currently tracked as closed

        private readonly Brush _handOpenBrush = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0)); // Brush used for drawing hands that are currently tracked as opened

        private readonly Brush _handLassoBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255)); // Brush used for drawing hands that are currently tracked as in lasso (pointer) position

        private readonly Brush _trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68)); // Brush used for drawing joints that are currently tracked
       
        private readonly Brush _inferredJointBrush = Brushes.Yellow; // Brush used for drawing joints that are currently inferred

        private readonly Pen _inferredBonePen = new Pen(Brushes.Gray, 1); // Pen used for drawing bones that are currently inferred

        private readonly DrawingGroup _drawingGroup; // Drawing group for body rendering output

        private readonly DrawingImage _imageSource; // Drawing image that we will display

        private ImageSource _imageSourceDepth
        {
            get
            {
                return _depthBitmap;
            }
        }

        private KinectSensor _kinectSensor; // Active Kinect sensor

        private readonly CoordinateMapper _coordinateMapper; // Coordinate mapper to map one type of point to another

        private BodyFrameReader _bodyFrameReader; // Reader for body frames

        private Body[] _bodies; // Array for the bodies

        private List<Tuple<JointType, JointType>> _bones; // definition of bones

        private readonly int _displayWidth; // Width of display (depth space)

        private readonly int _displayHeight; // Height of display (depth space)

        private List<Pen> _bodyColors; // List of colors for each body tracked

        private string _statusText; // Current status text to display

        #endregion

        #region public properties

        private string _buttonText = "Start calibration";

        public string ButtonText
        {
            get { return _buttonText; }
            set
            {
                _buttonText = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("ButtonText"));
            }
        }



        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSourceDepth { get { return _imageSourceDepth; } }
        public ImageSource ImageSource { get { return _imageSource; } }

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return _statusText;
            }
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;

                    if (PropertyChanged != null)
                        PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                }
            }
        }

        #endregion

        #region constructor

        public MainWindow()
        {
            LongClicked += (sender, args) => MouseOperations.MouseEvent(MouseOperations.MouseEventFlags.RightDown);

            _kinectSensor = KinectSensor.GetDefault(); // one sensor is currently supported
            _coordinateMapper = _kinectSensor.CoordinateMapper;   // get the coordinate mapper

            _depthFrameReader = _kinectSensor.DepthFrameSource.OpenReader();
            _depthFrameReader.FrameArrived += DepthReader_FrameArrived;  // wire handler for frame arrival
            _depthFrameDescription = _kinectSensor.DepthFrameSource.FrameDescription; // get FrameDescription from DepthFrameSource

            // allocate space to put the pixels being received and converted
            _depthPixels = new byte[_depthFrameDescription.Width * _depthFrameDescription.Height];

            // create the bitmap to display
            _depthBitmap = new WriteableBitmap(_depthFrameDescription.Width, _depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray8, null);

            // get the depth (display) extents
            _displayWidth = _kinectSensor.DepthFrameSource.FrameDescription.Width; // get size of joint space
            _displayHeight = _kinectSensor.DepthFrameSource.FrameDescription.Height; // get size of joint space

            _bodyFrameReader = _kinectSensor.BodyFrameSource.OpenReader();  // open the reader for the body frames

            InitializeSockets();
            InitBones();
            InitBodyColors();

            _kinectSensor.IsAvailableChanged += Sensor_IsAvailableChanged;
            _kinectSensor.Open();

            StatusText = _kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText : Properties.Resources.NoSensorStatusText;

            _drawingGroup = new DrawingGroup();  // Create the drawing group we'll use for drawing
            
            _imageSource = new DrawingImage(_drawingGroup);  // Create an image source that we can use in our image control

            DataContext = this;    // use the window object as the view model in this simple example

            InitializeComponent();
        }

        #endregion

        #region handlers

        private void FinishButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_calibrating)
            {
                ButtonText = "Calibrating topleft";
                var messageBoxResult = MessageBox.Show("Touch top-left", string.Empty, MessageBoxButton.YesNo);
                if (messageBoxResult == MessageBoxResult.Yes)
                    _calibrating = _calibratingTopLeft = true;
                else
                    Application.Current.Shutdown();
            }
            else
            {
                if (_calibratingTopLeft)
                {
                    ButtonText = "Calibrating bottomright";
                    _calibratingTopLeft = false;

                    var messageBoxResult = MessageBox.Show("Touch bottom-right", string.Empty, MessageBoxButton.YesNo);
                    if (messageBoxResult == MessageBoxResult.Yes)
                        _calibratingBottomRight = true;
                    else
                        Application.Current.Shutdown();
                }
                else if (_calibratingBottomRight)
                {
                    _calibratingBottomRight = false;
                    var btn = (Button)sender;
                    btn.Visibility = Visibility.Hidden;
                    _calibrating = false;
                    _doneCalibrating = true;
                }
            }
        }

        /// <summary>
        /// Execute start up tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_bodyFrameReader != null)
                _bodyFrameReader.FrameArrived += Reader_FrameArrived;
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            Dispose();
        }

        /// <summary>
        /// Handles the body frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;
           
            using (var bodyFrame = e.FrameReference.AcquireFrame())
            {
               
                if (bodyFrame != null)
                {
                    if (_bodies == null)
                        _bodies = new Body[bodyFrame.BodyCount];
                    
                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(_bodies);
                    dataReceived = true;
                }
            }

            if (dataReceived)
            {
                using (var dc = _drawingGroup.Open())
                {
                    // Draw a transparent background to set the render size
                    dc.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, _displayWidth, _displayHeight));

                    foreach (var body in _bodies)
                    {
                        if (body.IsTracked)
                        {
                            DrawClippedEdges(body, dc);

                            var joints = body.Joints;

                            // convert the joint points to depth (display) space
                            var jointPoints = new Dictionary<JointType, DepthPoint>();

                            foreach (var jointType in joints.Keys)
                            {
                                // sometimes the depth(Z) of an inferred joint may show as negative
                                // clamp down to 0.1f to prevent coordinatemapper from returning (-Infinity, -Infinity)
                                var position = joints[jointType].Position;
                                if (position.Z < 0)
                                    position.Z = InferredZPositionClamp;
                                
                                var depthSpacePoint = _coordinateMapper.MapCameraPointToDepthSpace(position);
                                jointPoints[jointType] = new DepthPoint(depthSpacePoint.X, depthSpacePoint.Y, (position.Z * 1000));
                            }

                            DrawHand(HandType.Left, jointPoints[JointType.HandTipLeft], dc);
                            DrawHand(HandType.Right, jointPoints[JointType.HandTipRight], dc);
                        }
                    }

                    // prevent drawing outside of our render area
                    _drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, _displayWidth, _displayHeight));
                }
            }
        }

        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            StatusText = _kinectSensor.IsAvailable ? 
                Properties.Resources.RunningStatusText : Properties.Resources.SensorNotAvailableStatusText;
        }


        /// <summary>
        /// Handles the depth frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void DepthReader_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            bool depthFrameProcessed = false;

            using (DepthFrame depthFrame = e.FrameReference.AcquireFrame())
            {
                if (depthFrame != null)
                {
                    // the fastest way to process the body index data is to directly access 
                    // the underlying buffer
                    using (var depthBuffer = depthFrame.LockImageBuffer())
                    {
                        // verify data and write the color data to the display bitmap
                        if (((_depthFrameDescription.Width * _depthFrameDescription.Height) == (depthBuffer.Size / _depthFrameDescription.BytesPerPixel)) &&
                            (_depthFrameDescription.Width == _depthBitmap.PixelWidth) && (_depthFrameDescription.Height == _depthBitmap.PixelHeight))
                        {
                            // Note: In order to see the full range of depth (including the less reliable far field depth)
                            // we are setting maxDepth to the extreme potential depth threshold
                            ushort maxDepth = ushort.MaxValue;

                            // If you wish to filter by reliable depth distance, uncomment the following line:
                            //// maxDepth = depthFrame.DepthMaxReliableDistance

                            ProcessDepthFrameData(depthBuffer.UnderlyingBuffer, depthBuffer.Size, depthFrame.DepthMinReliableDistance, maxDepth);
                            depthFrameProcessed = true;
                        }
                    }
                }
            }

            if (depthFrameProcessed)
            {
                RenderDepthPixels();
            }
        }

        /// <summary>
        /// Renders color pixels into the writeableBitmap.
        /// </summary>
        private void RenderDepthPixels()
        {
            _depthBitmap.WritePixels(
                new Int32Rect(0, 0, _depthBitmap.PixelWidth, _depthBitmap.PixelHeight),
                _depthPixels,
                _depthBitmap.PixelWidth,
                0);
        }

        private unsafe void ProcessDepthFrameData(IntPtr depthFrameData, uint depthFrameDataSize, ushort minDepth, ushort maxDepth)
        {
            // depth frame data is a 16 bit value
            ushort* frameData = (ushort*)depthFrameData;

            // convert depth to a visual representation
            for (int i = 0; i < (int)(depthFrameDataSize / _depthFrameDescription.BytesPerPixel); ++i)
            {
                // Get the depth for this pixel
                ushort depth = frameData[i];

                // To convert to a byte, we're mapping the depth value to the byte range.
                // Values outside the reliable depth range are mapped to 0 (black).
                _depthPixels[i] = (byte)(depth >= minDepth && depth <= maxDepth ? (depth / MapDepthToByte) : 0);
            }
        }

        #endregion

        #region methods

        private readonly List<double> avgX = new List<double>();
        private readonly List<double> avgY = new List<double>();

        private void DrawHand(HandType handType, DepthPoint handPosition, DrawingContext drawingContext)
        {
            var xInMillimeters = handPosition.X;
            var yInMillimeters = handPosition.Y;

            if (_calibrating)
            {
                if (handType == HandType.Right && _calibratingTopLeft)
                {
                    ScreenTopLeft = new Point(xInMillimeters, yInMillimeters);
                    _zLeft = handPosition.Z;
                }
                else if (handType == HandType.Left && _calibratingBottomRight)
                {
                    ScreenBottomRight = new Point(xInMillimeters, yInMillimeters);
                    _zRight = handPosition.Z;
                }
            }

            if (_calibrating)
                drawingContext.DrawEllipse(_handOpenBrush, null, handPosition.GetPoint(), HandSize, HandSize);
            else if (_doneCalibrating)
            {
                if (handType == HandType.Right)
                {
                    var x = PixelHelper.ToPixelsX(xInMillimeters, this);
                    var y = PixelHelper.ToPixelsY(yInMillimeters, this);

                    tbTop.Text = "X: " + Convert.ToInt32(xInMillimeters) + ", Y: " + 
                        Convert.ToInt32(yInMillimeters) + ", Z: " + Convert.ToInt32(handPosition.Z);

                    Task.Run(() =>
                    {
                        avgX.Add(x);
                        avgY.Add(y);

                        if (avgX.Count > 3)
                        {
                           avgX.RemoveAt(0);
                           avgY.RemoveAt(0);
                        }

                        var xFinal = avgX.Average();
                        var yFinal = avgY.Average();

                        Dispatcher.BeginInvoke(new Action(() => SetCursorPos(Convert.ToInt32(xFinal), Convert.ToInt32(yFinal))));
                    });

                    drawingContext.DrawEllipse(handPosition.Z >= WallZ - 25 ? _handClosedBrush : _handOpenBrush, null, handPosition.GetPoint(), HandSize, HandSize);

                    //if (handPosition.Z >= WallZ - 25 && handType == HandType.Right)
                    //    LeftDown();
                    //else if (handType == HandType.Right)
                    //    LeftRelease();
                }
                else
                    drawingContext.DrawEllipse(_handOpenBrush, null, handPosition.GetPoint(), HandSize, HandSize);
            }
            else if(handType == HandType.Right)
                drawingContext.DrawEllipse(_handOpenBrush, null, handPosition.GetPoint(), HandSize, HandSize);
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping body data
        /// </summary>
        /// <param name="body">body to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawClippedEdges(Body body, DrawingContext drawingContext)
        {
            var clippedEdges = body.ClippedEdges;

            if (clippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, _displayHeight - ClipBoundsThickness, _displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, _displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, _displayHeight));
            }

            if (clippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(_displayWidth - ClipBoundsThickness, 0, ClipBoundsThickness, _displayHeight));
            }
        }

        #endregion

        #region mouse controls

        private void LeftDown()
        {
            _isLeftHeldDown = true;

            MouseOperations.MouseEvent(MouseOperations.MouseEventFlags.LeftDown);
        }

        private void LeftRelease()
        {
            _isLeftHeldDown = false;
            MouseOperations.MouseEvent(MouseOperations.MouseEventFlags.LeftUp);
        }

        private void RightDown()
        {
            _isRightHeldDown = true;
            MouseOperations.MouseEvent(MouseOperations.MouseEventFlags.RightDown);
        }

        private void RightRelease()
        {
            _isRightHeldDown = false;
            MouseOperations.MouseEvent(MouseOperations.MouseEventFlags.RightUp);
        }

        private void InitLongPress()
        {
            Task.Run(async() =>
            {
                await Task.Delay(2000);

                if (_isLeftHeldDown)
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        LongClick(new LongClickArgs(MouseOperations.GetCursorPosition().X,
                            MouseOperations.GetCursorPosition().Y));
                    }));
            });
        }

        private EventHandler<LongClickArgs> LongClicked;

        private void LongClick(LongClickArgs args)
        {
            var handler = LongClicked;
            if (handler != null)
                handler(this, args);
        }

      

        #endregion

        #region sockets

        static List<IWebSocketConnection> _sockets;
        static bool _initialized = false;
        private static void InitializeSockets()
        {
            _sockets = new List<IWebSocketConnection>();

            var server = new WebSocketServer("ws://localhost:8181");

            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    Console.WriteLine("Connected to " + socket.ConnectionInfo.ClientIpAddress);
                    _sockets.Add(socket);
                };
                socket.OnClose = () =>
                {
                    Console.WriteLine("Disconnected from " + socket.ConnectionInfo.ClientIpAddress);
                    _sockets.Remove(socket);
                };
                socket.OnMessage = message =>
                {
                    Console.WriteLine(message);
                };
            });

            _initialized = true;

            //Console.ReadLine();
        }

        #endregion
    }
}
