using BSNet.Datagram;
using BSNet.Stream;
using System;

namespace BSNet.Example
{
    public class ExampleProgram
    {
        public static void Main(string[] args)
        {
            // Testing on a header
            //using (BSWriter writer = BSWriter.GetWriter(21))
            //{
            //    // Write header data
            //    using (Header header = Header.GetHeader(ConnectionType.HEARTBEAT, 0,
            //        0, 4214, 79832678))
            //    {
            //        header.Serialize(writer);
            //    }

            //    writer.SerializeUShort(0);

            //    writer.PadToEnd();

            //    // Write message data
            //    writer.SerializeChecksum(new byte[] { 0x00, 0x00, 0x00, 0x01 });

            //    byte[] rawBytes = writer.ToArray();
            //}

            //Console.ReadKey(true);


            // Comparing different writers
            //using (BSWriter writer = BSWriter.GetWriter(4))
            //{
            //    writer.SerializeUInt(15, 32);
            //    writer.SerializeUInt(15, 16);
            //    writer.SerializeUInt(15, 5);
            //    writer.SerializeUInt(15, 9);
            //    writer.SerializeChecksum(new byte[] { 0x00 });
            //    Console.WriteLine("BSWriter");
            //    foreach (var item in writer.ToArray())
            //      Console.WriteLine(item);
            //}

            //OldWriter writer2 = OldWriter.GetWriter(4 * 2);
            //writer2.SerializeUInt(15, 32);
            //writer2.SerializeUInt(15, 16);
            //writer2.SerializeUInt(15, 5);
            //writer2.SerializeUInt(15, 9);
            //Console.WriteLine("OldWriter");
            //foreach (var item in writer2.ToArray())
            //  Console.WriteLine(item);

            //Console.ReadKey(true);


            // Testing a P2P implementation
            ExampleClient client1 = new ExampleClient(1609, "127.0.0.1", 1615);
            ExampleClient client2 = new ExampleClient(1615, "127.0.0.1", 1609);

            while (true)
            {
                client1.Update();
                client2.Update();

                if (Console.KeyAvailable)
                {
                    ConsoleKey key = Console.ReadKey(true).Key;
                    if (key == ConsoleKey.Q)
                    {
                        break;
                    }
                    else if (key == ConsoleKey.D1)
                    {
                        client1.Dispose();
                        client1 = new ExampleClient(1609, "127.0.0.1", 1615);
                    }
                }
            }

            client1.Dispose();
            client2.Dispose();
        }
    }
}
