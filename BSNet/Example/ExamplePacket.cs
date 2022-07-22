using BSNet.Quantization;
using BSNet.Stream;
using System.Text;
using BSNet.Attributes;

namespace BSNet.Example
{
    public class ExamplePacket : IBSSerializable
    {
        [BSSerializable(typeof(UTF8Encoding))]
        public string TestString { set; get; }
        
        [BSSerializable(-10, 10, 0.00001f)]
        public float TestFloat { set; get; }

        private BoundedRange testRange = new BoundedRange(-10, 10, 0.00001f);

        public ExamplePacket() { }

        public ExamplePacket(string testString, float testFloat)
        {
            TestString = testString;
            TestFloat = testFloat;
        }

        public virtual void Serialize(IBSStream stream)
        {
            TestFloat = stream.SerializeFloat(testRange, TestFloat);
            TestString = stream.SerializeString(Encoding.ASCII, TestString);
        }
    }
}
