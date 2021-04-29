using System;

namespace BSNet.Example
{
    public class ExampleProgram
    {
        public static void Main(string[] args)
        {
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
