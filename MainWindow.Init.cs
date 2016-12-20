using System;
using System.Collections.Generic;
using System.Windows.Media;
using Microsoft.Kinect;

namespace KinectMouseClickPrototype
{
    public partial class MainWindow
    {
        private void InitBones()
        {
            // a bone defined as a line between two joints
            _bones = new List<Tuple<JointType, JointType>>();

            // Torso
            _bones.Add(new Tuple<JointType, JointType>(JointType.Head, JointType.Neck));
            _bones.Add(new Tuple<JointType, JointType>(JointType.Neck, JointType.SpineShoulder));
            _bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.SpineMid));
            _bones.Add(new Tuple<JointType, JointType>(JointType.SpineMid, JointType.SpineBase));
            _bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderRight));
            _bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderLeft));
            _bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipRight));
            _bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipLeft));

            // Right Arm
            _bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderRight, JointType.ElbowRight));
            _bones.Add(new Tuple<JointType, JointType>(JointType.ElbowRight, JointType.WristRight));
            _bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.HandRight));
            _bones.Add(new Tuple<JointType, JointType>(JointType.HandRight, JointType.HandTipRight));
            _bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.ThumbRight));

            // Left Arm
            _bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderLeft, JointType.ElbowLeft));
            _bones.Add(new Tuple<JointType, JointType>(JointType.ElbowLeft, JointType.WristLeft));
            _bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.HandLeft));
            _bones.Add(new Tuple<JointType, JointType>(JointType.HandLeft, JointType.HandTipLeft));
            _bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.ThumbLeft));

            // Right Leg
            _bones.Add(new Tuple<JointType, JointType>(JointType.HipRight, JointType.KneeRight));
            _bones.Add(new Tuple<JointType, JointType>(JointType.KneeRight, JointType.AnkleRight));
            _bones.Add(new Tuple<JointType, JointType>(JointType.AnkleRight, JointType.FootRight));

            // Left Leg
            _bones.Add(new Tuple<JointType, JointType>(JointType.HipLeft, JointType.KneeLeft));
            _bones.Add(new Tuple<JointType, JointType>(JointType.KneeLeft, JointType.AnkleLeft));
            _bones.Add(new Tuple<JointType, JointType>(JointType.AnkleLeft, JointType.FootLeft));
        }

        private void InitBodyColors()
        {
            // populate body colors, one for each BodyIndex
            _bodyColors = new List<Pen>
            {
                new Pen(Brushes.Red, 6),
                new Pen(Brushes.Orange, 6),
                new Pen(Brushes.Green, 6),
                new Pen(Brushes.Blue, 6),
                new Pen(Brushes.Indigo, 6),
                new Pen(Brushes.Violet, 6)
            };
        }

        private void Dispose()
        {
            if (_bodyFrameReader != null)
            {
                _bodyFrameReader.Dispose();
                _bodyFrameReader = null;
            }

            if (_depthFrameReader != null)
            {
                // DepthFrameReader is IDisposable
                _depthFrameReader.Dispose();
                _depthFrameReader = null;
            }

            if (_kinectSensor != null)
            {
                _kinectSensor.Close();
                _kinectSensor = null;
            }
        }
    }
}
