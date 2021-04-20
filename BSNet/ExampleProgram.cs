using BSNet.Quantization;
using BSNet.Stream;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace BSNet.Example
{
    public class ExampleProgram
    {
        private static string Yeet()
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
                return "Not connected.";

            var networkDevices = NetworkInterface.GetAllNetworkInterfaces();
            if (networkDevices.Length == 0)
                return "No network devices.";

            networkDevices = networkDevices
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                    || n.OperationalStatus == OperationalStatus.Dormant)
                .ToArray();
            if (networkDevices.Length == 0)
                return "Network not connected.";

            networkDevices = networkDevices
                .Where(n => n.Supports(NetworkInterfaceComponent.IPv4))
                .ToArray();
            if (networkDevices.Length == 0)
                return "No IPv4 network found.";

            var router = networkDevices.First().GetIPProperties()
                .GatewayAddresses.First();

            string routerIp = router.Address.ToString();

            return routerIp;
        }

        public static void Main(string[] args)
        {
            Console.WriteLine(BSUtility.GetExternalIP());

            
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
