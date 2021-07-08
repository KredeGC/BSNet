using BSNet.Stream;
using System;

namespace BSNet.Example
{
    public class ExampleProgram
    {
        public static void Main(string[] args)
        {
            //BitWriter writer = BitWriter.GetWriter(4);
            //writer.SerializeUInt(15, 32);
            //writer.SerializeUInt(15, 16);
            //writer.SerializeUInt(15, 5);
            //writer.SerializeUInt(15, 9);
            //Console.WriteLine("BitWriter");
            //foreach (var item in writer.ToArray())
            //    Console.WriteLine(item);

            //BSWriter writer2 = BSWriter.GetWriter(4 * 2);
            //writer2.SerializeUInt(15, 32);
            //writer2.SerializeUInt(15, 16);
            //writer2.SerializeUInt(15, 5);
            //writer2.SerializeUInt(15, 9);
            //Console.WriteLine("BSWriter");
            //foreach (var item in writer2.ToArray())
            //    Console.WriteLine(item);

            //Console.ReadKey(true);

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
