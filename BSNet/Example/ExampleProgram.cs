using System;
using System.Collections.Generic;
using System.Threading;

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


            // Testing nested writers
            //NewWriter occupy1 = NewWriter.GetWriter(4);
            //NewWriter occupy2 = NewWriter.GetWriter(3);
            //NewWriter occupy3 = NewWriter.GetWriter(2);
            //NewWriter.ReturnWriter(occupy1);
            //NewWriter.ReturnWriter(occupy2);
            //NewWriter.ReturnWriter(occupy3);

            //byte[] fullBytes;
            //using (NewWriter writer1 = NewWriter.GetWriter(1))
            //{
            //    writer1.SerializeUInt(1U, 1);
            //    writer1.SerializeUInt(273U, 11);
            //    writer1.SerializeUInt(273U, 17);
            //    writer1.SerializeUInt(uint.MaxValue, 14);
            //    writer1.SerializeUInt(511U, 9);
            //    byte[] rawBytes = writer1.ToArray();
            //    BSUtility.PrintBits(rawBytes);

            //    using (NewWriter writer2 = NewWriter.GetWriter(2))
            //    {
            //        writer2.SerializeBytes(writer1.TotalBits, rawBytes, true);
            //        BSUtility.PrintBits(writer2.ToArray());

            //        fullBytes = writer2.ToArray();
            //    }
            //}

            //using (NewReader reader = NewReader.GetReader(fullBytes))
            //{
            //    Console.WriteLine(reader.SerializeUInt(0, 1));
            //    Console.WriteLine(reader.SerializeUInt(0, 11));
            //    Console.WriteLine(reader.SerializeUInt(0, 17));
            //    Console.WriteLine(reader.SerializeUInt(0, 14));
            //    Console.WriteLine(reader.SerializeUInt(0, 9));
            //}

            //Console.ReadKey(true);


            // Example server
            int serverPort = 1615;
            ExampleServer server = new ExampleServer(serverPort);
            List<ExampleClient> botClients = new List<ExampleClient>();

            bool quit = false;

            Action stopServer = new Action(() =>
            {
                server.Dispose();

                lock (botClients)
                {
                    foreach (ExampleClient client in botClients)
                        client.Dispose();
                }
            });

            // Run the server + bots in another thread
            Thread thread = new Thread(() =>
            {
                while (!quit)
                {
                    server.Update();

                    lock (botClients)
                    {
                        foreach (ExampleClient client in botClients)
                            client.Update();
                    }
                }
            });
            thread.Start();

            // Catch interrupts
            Console.CancelKeyPress += (sender, e) => {
                e.Cancel = true;

                quit = true;

                // Dispose of server
                stopServer.Invoke();

                Environment.Exit(0);
            };

            // Run commands
            while (!quit)
            {
                Console.Write("> ");
                string msg = Console.ReadLine().ToLower();
                string[] pars = msg.Split(' ');
                string command = pars[0];
                switch (command)
                {
                    case "cls":
                        Console.Clear();
                        break;
                    case "pl":
                        server.PrintConnections();
                        break;
                    case "bot":
                        lock (botClients)
                        {
                            ExampleClient client = new ExampleClient(0, "127.0.0.1", serverPort);
                            botClients.Add(client);
                        }
                        break;
                    case "":

                        break;
                    case "exit":
                        quit = true;
                        break;
                    case "restart":
                        stopServer.Invoke();
                        lock (botClients)
                            botClients.Clear();
                        server = new ExampleServer(serverPort);
                        break;
                }
            }

            // Dispose of server
            stopServer.Invoke();


            // Testing a P2P implementation
            //ExampleClient client1 = new ExampleClient(1609, "127.0.0.1", 1615, true);
            //ExampleClient client2 = new ExampleClient(1615, "127.0.0.1", 1609);

            //while (true)
            //{
            //    client1.Update();
            //    client2.Update();

            //    if (Console.KeyAvailable)
            //    {
            //        ConsoleKey key = Console.ReadKey(true).Key;
            //        if (key == ConsoleKey.Q)
            //        {
            //            break;
            //        }
            //        else if (key == ConsoleKey.R)
            //        {
            //            client2.Dispose();
            //            client2 = new ExampleClient(1615, "127.0.0.1", 1609);
            //        }
            //    }
            //}

            //client1.Dispose();
            //client2.Dispose();
        }
    }
}
