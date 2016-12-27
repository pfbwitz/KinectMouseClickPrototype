using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace KinectMouseClickPrototype.Cursor
{
    [DataContract]
    public class CursorData
    {
        [DataMember]
        public int X { get; private set; }

        [DataMember]
        public int Y { get; private set; }

        [DataMember]
        public int Z { get; private set; }

        public CursorData(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public CursorData()
        {
        }

        public string Serialize()
        {
            var strean = new MemoryStream();
            new DataContractJsonSerializer(typeof(CursorData)).WriteObject(strean, this);
            strean.Position = 0;
            return new StreamReader(strean).ReadToEnd();
        }
    }
}
