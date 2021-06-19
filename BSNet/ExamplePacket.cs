using BSNet.Stream;
using System.Text;

namespace BSNet.Example
{
    public class ExamplePacket : IBSSerializable
    {
        public string TestString { set; get; }

        public ExamplePacket() { }

        public ExamplePacket(string testString)
        {
            TestString = testString;
        }

        public virtual void Serialize(IBSStream stream)
        {
            TestString = stream.SerializeString(Encoding.ASCII, TestString);
        }
    }
}
