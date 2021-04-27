using BSNet.Quantization;
using BSNet.Stream;
using System;

namespace BSNet.Example
{
    public class ExampleProgram
    {
        public static void Main(string[] args)
        {
            //Console.WriteLine(BSUtility.GetExternalIP());


            byte[] protocolVersion = new byte[] { 0x00, 0x00, 0x00, 0x01 };

            BoundedRange range = new BoundedRange(-1f, 1f, 0.01f);

            // Bit writer
            BSWriter writer = new BSWriter();

            writer.SerializeFloat(range, -0.12345f);
            writer.SerializeChecksum(protocolVersion);

            byte[] data = writer.ToArray();

            // Bit reader
            BSReader reader = new BSReader(data);

            bool checksum = reader.SerializeChecksum(protocolVersion);
            float number = reader.SerializeFloat(range);

            Console.WriteLine(checksum);
            Console.WriteLine(number);


            Console.ReadKey();
        }
    }
}
