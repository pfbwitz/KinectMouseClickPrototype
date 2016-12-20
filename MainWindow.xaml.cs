using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using KinectMouseClickPrototype.Enums;
using Microsoft.Kinect;

namespace KinectMouseClickPrototype
{
    public partial class MainWindow :  INotifyPropertyChanged
    {
        [DllImport("User32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        #region private properties

        //Peter
        private bool _isLeftHeldDown;

        private bool _isRightDown;
        //EndPeter

        private const double HandSize = 30; // Radius of drawn hand circles

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

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
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
            _kinectSensor = KinectSensor.GetDefault(); // one sensor is currently supported
            _coordinateMapper = _kinectSensor.CoordinateMapper;   // get the coordinate mapper

            // get the depth (display) extents
            _displayWidth = _kinectSensor.DepthFrameSource.FrameDescription.Width; // get size of joint space
            _displayHeight = _kinectSensor.DepthFrameSource.FrameDescription.Height; // get size of joint space

            _bodyFrameReader = _kinectSensor.BodyFrameSource.OpenReader();  // open the reader for the body frames

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
                    dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, _displayWidth, _displayHeight));

                    var penIndex = 0;
                    foreach (var body in _bodies)
                    {
                        Pen drawPen = _bodyColors[penIndex++];

                        if (body.IsTracked)
                        {
                            DrawClippedEdges(body, dc);

                            var joints = body.Joints;

                            // convert the joint points to depth (display) space
                            var jointPoints = new Dictionary<JointType, Point>();

                            foreach (var jointType in joints.Keys)
                            {
                                // sometimes the depth(Z) of an inferred joint may show as negative
                                // clamp down to 0.1f to prevent coordinatemapper from returning (-Infinity, -Infinity)
                                var position = joints[jointType].Position;
                                if (position.Z < 0)
                                    position.Z = InferredZPositionClamp;
                                
                                var depthSpacePoint = _coordinateMapper.MapCameraPointToDepthSpace(position);
                                jointPoints[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);
                            }

                            DrawBody(joints, jointPoints, dc, drawPen);

                            DrawHand(HandType.Left, body.HandLeftState, jointPoints[JointType.HandLeft], dc);
                            DrawHand(HandType.Right, body.HandRightState, jointPoints[JointType.HandRight], dc);
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

        #endregion

        #region methods

        /// <summary>
        /// Draws a body
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="drawingPen">specifies color to draw a specific body</param>
        private void DrawBody(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {
            // Draw the bones
            foreach (var bone in _bones)
                DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, drawingPen);


            // Draw the joints
            foreach (var jointType in joints.Keys)
            {
                Brush drawBrush = null;

                var trackingState = joints[jointType].TrackingState;

                if (trackingState == TrackingState.Tracked)
                    drawBrush = _trackedJointBrush;
                else if (trackingState == TrackingState.Inferred)
                    drawBrush = _inferredJointBrush;

                if (drawBrush != null)
                    drawingContext.DrawEllipse(drawBrush, null, jointPoints[jointType], JointThickness, JointThickness);
            }
        }

        /// <summary>
        /// Draws one bone of a body (joint to joint)
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="jointType0">first joint of bone to draw</param>
        /// <param name="jointType1">second joint of bone to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// /// <param name="drawingPen">specifies color to draw a specific bone</param>
        private void DrawBone(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, JointType jointType0, JointType jointType1, DrawingContext drawingContext, Pen drawingPen)
        {
            var joint0 = joints[jointType0];
            var joint1 = joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == TrackingState.NotTracked ||
                joint1.TrackingState == TrackingState.NotTracked)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            var drawPen = _inferredBonePen;
            if ((joint0.TrackingState == TrackingState.Tracked) && (joint1.TrackingState == TrackingState.Tracked))
                drawPen = drawingPen;
            
            drawingContext.DrawLine(drawPen, jointPoints[jointType0], jointPoints[jointType1]);
        }

        /// <summary>
        /// Draws a hand symbol if the hand is tracked: red circle = closed, green circle = opened; blue circle = lasso
        /// </summary>
        /// <param name="handState">state of the hand</param>
        /// <param name="handPosition">position of the hand</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawHand(HandType handType, HandState handState, Point handPosition, DrawingContext drawingContext)
        {
           if(handType == HandType.Left)
               tbTop.Text = "X: " + handPosition.X + ", Y: " + handPosition.Y;

            switch (handState)
            {
                case HandState.Closed:
                    if (handType == HandType.Left)
                    {
                        Mouse.OverrideCursor = Cursors.Hand;
                        SetCursor(Convert.ToInt32(handPosition.X), Convert.ToInt32(handPosition.Y));
                        drawingContext.DrawEllipse(_handClosedBrush, null, handPosition, HandSize, HandSize);
                    }

                    if (handType == HandType.Right)
                    {
                        if(!_isLeftHeldDown)
                            LeftDown();
                    }
                    break;
                case HandState.Open:
                    Mouse.OverrideCursor = Cursors.Arrow;

                    if (handType == HandType.Right)
                    {
                        if (_isLeftHeldDown)
                            LeftRelease();
                    }

                    drawingContext.DrawEllipse(_handOpenBrush, null, handPosition, HandSize, HandSize);
                    break;

                case HandState.Lasso:
                    drawingContext.DrawEllipse(_handLassoBrush, null, handPosition, HandSize, HandSize);
                    break;
            }
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
            _isRightDown = true;
            MouseOperations.MouseEvent(MouseOperations.MouseEventFlags.RightDown);
        }

        private void RightRelease()
        {
            _isRightDown = false;
            MouseOperations.MouseEvent(MouseOperations.MouseEventFlags.RightUp);
        }

        private static void SetCursor(int x, int y)
        {
            //var screenWidth = System.Windows.SystemParameters.PrimaryScreenWidth;
            //var screenHeight = System.Windows.SystemParameters.PrimaryScreenHeight;

            var factor = 4;

            //var xL = (int)App.Current.MainWindow.Left;
            //var yT = (int)App.Current.MainWindow.Top;
            SetCursorPos(x * factor, y * factor);
            //SetCursorPos(x + xL, y + yT);
        }



        #endregion
    }
}
