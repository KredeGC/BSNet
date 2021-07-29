using BSNet.Stream;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace BSNet.Example
{
    public class ExampleProgram
    {
        public static void Main(string[] _)
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
            //BSWriter occupy1 = BSWriter.Get(4);
            //BSWriter occupy2 = BSWriter.Get(3);
            //BSWriter occupy3 = BSWriter.Get(2);
            //BSWriter.Return(occupy1);
            //BSWriter.Return(occupy2);
            //BSWriter.Return(occupy3);

            //byte[] fullBytes;
            //using (BSWriter writer1 = BSWriter.Get(1))
            //{
            //    writer1.SerializeUInt(1U, 1);
            //    writer1.SerializeUInt(273U, 11);
            //    writer1.SerializeUInt(273U, 17);
            //    writer1.SerializeUInt(uint.MaxValue, 14);
            //    writer1.SerializeUInt(511U, 9);
            //    writer1.SerializeUInt(511U, 9);
            //    writer1.SerializeUInt(511U, 9);
            //    writer1.SerializeUInt(511U, 9);
            //    writer1.SerializeUInt(511U, 9);
            //    writer1.SerializeUInt(511U, 9);
            //    byte[] rawBytes = writer1.ToArray();
            //    BSUtility.PrintBits(rawBytes);

            //    using (BSWriter writer2 = BSWriter.Get(2))
            //    {
            //        writer2.SerializeBytes(writer1.TotalBits, rawBytes, true);
            //        BSUtility.PrintBits(writer2.ToArray());

            //        fullBytes = writer2.ToArray();
            //    }
            //}

            //using (BSReader reader = BSReader.Get(fullBytes))
            //{
            //    Console.WriteLine(reader.SerializeUInt(0, 1));
            //    Console.WriteLine(reader.SerializeUInt(0, 11));
            //    Console.WriteLine(reader.SerializeUInt(0, 17));
            //    Console.WriteLine(reader.SerializeUInt(0, 14));
            //    Console.WriteLine(reader.SerializeUInt(0, 9));
            //    Console.WriteLine(reader.SerializeUInt(0, 9));
            //    Console.WriteLine(reader.SerializeUInt(0, 9));
            //    Console.WriteLine(reader.SerializeUInt(0, 9));
            //    Console.WriteLine(reader.SerializeUInt(0, 9));
            //    Console.WriteLine(reader.SerializeUInt(0, 9));
            //}

            //Console.ReadKey(true);


            MainServer();
        }

        private static void MainP2P()
        {
            // Testing a P2P implementation
            ExampleClient client1 = new ExampleClient(1609, "127.0.0.1", 1615, true);
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
                    else if (key == ConsoleKey.R)
                    {
                        client2.Dispose();
                        client2 = new ExampleClient(1615, "127.0.0.1", 1609);
                    }
                }
            }

            client1.Dispose();
            client2.Dispose();
        }

        private static ExampleServer Server { get; set; }

        private static void MainServer()
        {
            int serverPort = 1615;
            Server = new ExampleServer(serverPort);
            List<ExampleClient> botClients = new List<ExampleClient>();

            bool quit = false;

            // Lambda for stopping the server
            Action StopServer = new Action(() =>
            {
                Server.Dispose();

                lock (botClients)
                {
                    foreach (ExampleClient client in botClients)
                        client.Dispose();
                    botClients.Clear();
                }
            });

            // Run the server + bots in another thread
            Thread thread = new Thread(() =>
            {
                while (!quit)
                {
                    Server.Update();

                    lock (botClients)
                    {
                        foreach (ExampleClient client in botClients)
                            client.Update();
                    }
                }
            });
            thread.Start();

            // Catch interrupts
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;

                quit = true;

                // Dispose of server
                StopServer();

                Environment.Exit(0);
            };

            // Run commands
            while (!quit)
            {
                Console.Write("> ");
                string msg = Console.ReadLine().ToLower();
                string[] args = msg.Split(' ');
                string command = args[0];
                switch (command)
                {
                    case "cls":
                        Console.Clear();
                        break;
                    case "pl":
                        Server.PrintConnections(20);
                        break;
                    case "bot":
                        AddBot(args, botClients);
                        break;
                    case "stats":
                        Server.PrintNetworkStats();
                        break;
                    case "version":
                        VersionCommands(args);
                        break;
                    case "hide":
                        string logLevel = args[1];
                        switch (logLevel)
                        {
                            case "info":

                                break;
                            case "warning":

                                break;
                            case "error":

                                break;
                        }
                        break;
                    case "show":

                        break;
                    case "exit":
                        quit = true;
                        break;
                    case "restart":
                        Restart(StopServer, botClients);
                        break;
                    default:
                        Console.WriteLine($"Unknown command: {command}");
                        break;
                }
            }

            // Dispose of server
            StopServer();
        }

        private static void PrintBytes(byte[] bytes)
        {
            string alphabet = "0123456789ABCDEF";

            for (int i = 0; i < bytes.Length; i++)
            {
                byte value = bytes[i];
                char first = alphabet[(int)(value >> 4)];
                char second = alphabet[(int)(value & 0xF)];

                Console.Write($"0x{first}{second}");

                if (i < bytes.Length - 1)
                    Console.Write(", ");
            }

            Console.Write("\r\n");
        }

        private static void AddBot(string[] args, IList<ExampleClient> botClients)
        {
            int amount = 1;
            if (args.Length > 1)
                int.TryParse(args[1], out amount);

            lock (botClients)
            {
                for (int i = 0; i < amount; i++)
                {
                    ExampleClient client = new ExampleClient(0, "127.0.0.1", Server.Port);
                    botClients.Add(client);
                }
            }
        }

        private static void VersionCommands(string[] args)
        {
            if (args.Length < 2) return;

            switch (args[1])
            {
                case "get":
                    PrintBytes(Server.ProtocolVersion);
                    break;
                case "generate":
                    int length = 8;
                    if (args.Length > 2)
                        int.TryParse(args[2], out length);

                    byte[] protocolVersion = new byte[length];
                    Cryptography.GetBytes(protocolVersion);

                    PrintBytes(protocolVersion);
                    break;
                default:
                    Console.WriteLine($"Unknown command: {args[0]} {args[1]}");
                    break;
            }
        }

        private static void Restart(Action StopServer, IList<ExampleClient> botClients)
        {
            int serverPort = Server.Port;
            StopServer();
            Server = new ExampleServer(serverPort);
        }
    }
}
