using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

namespace BSNet.Example
{
    public class ExampleProgram
    {
        public static void Main(string[] commands)
        {
            MainP2P();
            //MainServer(commands);
        }

        private static void MainP2P()
        {
            // Testing a P2P implementation
            ExamplePeer peer1 = new ExamplePeer(0, 250d, 0.25d, 0.001d);
            ExamplePeer peer2 = new ExamplePeer(0, 250d, 0.25d, 0.001d);

            // Construct peer endpoint
            IPAddress peer2Address = IPAddress.Parse("127.0.0.1");
            IPEndPoint peer2EndPoint = new IPEndPoint(peer2Address, peer2.Port);

            // Send a request to connect
            peer1.Connect(peer2EndPoint);

            while (true)
            {
                peer1.Update();
                peer2.Update();

                if (Console.KeyAvailable)
                {
                    ConsoleKey key = Console.ReadKey(true).Key;
                    if (key == ConsoleKey.Q)
                    {
                        break;
                    }
                    else if (key == ConsoleKey.R)
                    {
                        int port = peer2.Port;
                        peer2.Dispose();
                        peer2 = new ExamplePeer(port, 250d, 0.25d, 0.001d);
                    }
                }
            }

            peer1.Dispose();
            peer2.Dispose();
        }

        private static ExampleServer Server { get; set; }
        private static List<ExampleClient> botClients = new List<ExampleClient>();
        private static bool quitting = false;

        private static void StopServer()
        {
            Server.Dispose();

            lock (botClients)
            {
                foreach (ExampleClient client in botClients)
                    client.Dispose();
                botClients.Clear();
            }
        }

        private static void MainServer(string[] commands)
        {
            int serverPort = 1615;
            Server = new ExampleServer(serverPort);

            // Run the server + bots in another thread
            Thread thread = new Thread(() =>
            {
                while (!quitting)
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

                quitting = true;

                // Dispose of server
                StopServer();

                Environment.Exit(0);
            };

            // Run initial commands
            foreach (string command in commands)
                HandleCommand(command);

            // Run commands
            while (!quitting)
            {
                Console.Write("> ");
                HandleCommand(Console.ReadLine());
            }

            // Dispose of server
            StopServer();
        }

        private static void HandleCommand(string line)
        {
            string msg = line.ToLower();
            string[] args = msg.Split(' ');
            string command = args[0];
            switch (command)
            {
                case "cls":
                    Console.Clear();
                    break;
                case "pl":
                    PrintConnections(args);
                    break;
                case "bot":
                    AddBots(args);
                    break;
                case "stats":
                    Server.PrintNetworkStats();
                    break;
                case "version":
                    VersionCommands(args);
                    break;
                case "simulate":
                    SimulateNetwork(args);
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
                    quitting = true;
                    break;
                case "restart":
                    Restart(StopServer, botClients);
                    break;
                default:
                    Console.WriteLine($"Unknown command: {command}");
                    break;
            }
        }

        private static void PrintBytes(byte[] bytes)
        {
            string alphabet = "0123456789ABCDEF";

            for (int i = 0; i < bytes.Length; i++)
            {
                byte value = bytes[i];
                char first = alphabet[value >> 4];
                char second = alphabet[value & 0xF];

                Console.Write($"0x{first}{second}");

                if (i < bytes.Length - 1)
                    Console.Write(", ");
            }

            Console.Write("\r\n");
        }

        private static void PrintConnections(string[] args)
        {
            int amount = 20;
            if (args.Length > 1)
                int.TryParse(args[1], out amount);

            Server.PrintConnections(amount);
        }

        private static void AddBots(string[] args)
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

        private static void SimulateNetwork(string[] args)
        {
#if NETWORK_DEBUG
            if (args.Length < 2) return;

            double amount = 0;
            if (args.Length > 2)
                double.TryParse(args[2], out amount);

            switch (args[1])
            {
                case "latency":
                    Server.SimulatedPacketLatency = amount;
                    break;
                case "loss":
                    Server.SimulatedPacketLoss = amount / 100d;
                    break;
                case "corruption":
                    Server.SimulatedPacketCorruption = amount / 100d;
                    break;
                default:
                    Console.WriteLine($"Unknown command: {args[0]} {args[1]}");
                    break;
            }
#else
            Console.WriteLine("Compile the program with NETWORK_DEBUG to use this feature");
#endif
        }

        private static void Restart(Action StopServer, IList<ExampleClient> botClients)
        {
            int serverPort = Server.Port;
            StopServer();
            Server = new ExampleServer(serverPort);
        }
    }
}
