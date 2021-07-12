﻿using BSNet.Stream;
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


            //// Test shift array
            //// 11111111 00110011 11111111
            //// 00011111 11100110 01111111
            ////byte[] byteArray = new byte[] { 0b11111111, 0b00110011, 0b11111111 };
            //byte[] byteArray = new byte[] { 0b11111111, 0b10000000 };
            //byte[] shiftedArray = BSUtility.BitShiftRight(byteArray, byteArray.Length, 4);
            //Console.WriteLine(shiftedArray[0]);
            //Console.WriteLine(shiftedArray[1]);
            ////Console.WriteLine(shiftedArray[2]);

            //Console.ReadKey(true);

            // Testing nested writers
            //byte[] testBytes = new byte[] { 0b11110000, 0b10101010, 0b11110000 };
            //BSUtility.PrintBits(testBytes);
            //byte[] trimmedBytes = BSUtility.Trim(testBytes, 4, 16);
            //BSUtility.PrintBits(trimmedBytes);

            NewWriter occupy1 = NewWriter.GetWriter(4);
            NewWriter occupy2 = NewWriter.GetWriter(3);
            NewWriter occupy3 = NewWriter.GetWriter(2);
            NewWriter.ReturnWriter(occupy1);
            NewWriter.ReturnWriter(occupy2);
            NewWriter.ReturnWriter(occupy3);

            byte[] fullBytes;
            using (NewWriter writer1 = NewWriter.GetWriter(1))
            {
                writer1.SerializeUInt(uint.MaxValue, 10);
                writer1.SerializeUInt(uint.MaxValue, 3);
                writer1.SerializeUInt(uint.MaxValue, 11);
                writer1.SerializeUInt(uint.MaxValue, 31);
                //writer1.SerializeUInt(1, 1);
                //writer1.SerializeUInt(273, 11);
                //writer1.SerializeUInt(273, 17);
                //writer1.SerializeUInt(uint.MaxValue, 14);
                //writer1.SerializeUInt(511, 11);
                //writer1.SerializeUInt(0b10101010111111111010101000001111, 32);
                //writer1.SerializeUInt(273, 11);
                //writer1.SerializeUInt(211, 10);
                //writer1.SerializeUInt(126, 7);
                byte[] rawBytes = writer1.ToArray();
                BSUtility.PrintBits(rawBytes);

                using (NewWriter writer2 = NewWriter.GetWriter(2))
                {
                    writer2.SerializeBytes(writer1.TotalBits, rawBytes, true);
                    BSUtility.PrintBits(writer2.ToArray());

                    fullBytes = writer2.ToArray();
                }
            }

            using (NewReader reader = NewReader.GetReader(fullBytes))
            {
                //Console.WriteLine(reader.SerializeUInt(0, 1));
                Console.WriteLine(reader.SerializeUInt(0, 10));
                Console.WriteLine(reader.SerializeUInt(0, 3));
                Console.WriteLine(reader.SerializeUInt(0, 11));
                Console.WriteLine(reader.SerializeUInt(0, 31));
                //Console.WriteLine(reader.SerializeUInt(0, 32));
                //Console.WriteLine(reader.SerializeUShort(0, 10));
                //Console.WriteLine(reader.SerializeUShort(0, 7));
            }

            Console.ReadKey(true);

            //BSWriter occupy1 = BSWriter.GetWriter(4);
            //BSWriter occupy2 = BSWriter.GetWriter(3);
            //BSWriter occupy3 = BSWriter.GetWriter(2);
            //BSWriter.ReturnWriter(occupy1);
            //BSWriter.ReturnWriter(occupy2);
            //BSWriter.ReturnWriter(occupy3);

            //byte[] fullBytes;
            //using (BSWriter writer1 = BSWriter.GetWriter(1))
            //{
            //    writer1.SerializeUShort(273, 11);
            //    byte[] rawBytes = writer1.ToArray();
            //    Console.WriteLine(rawBytes[0]);
            //    Console.WriteLine(rawBytes[1]);

            //    using (BSWriter writer2 = BSWriter.GetWriter(2))
            //    {
            //        //writer2.SerializeBuffer(bitCount, rawBytes);
            //        writer2.SerializeBytes(writer1.TotalBits, rawBytes);
            //        Console.WriteLine(writer2.ToArray()[0]);
            //        Console.WriteLine(writer2.ToArray()[1]);

            //        fullBytes = writer2.ToArray();
            //    }
            //}

            //using (BSReader reader = BSReader.GetReader(fullBytes))
            //{
            //    Console.WriteLine(reader.SerializeUShort(0, 11));
            //}

            //Console.ReadKey(true);


            // Testing a P2P implementation
            //ExampleClient client1 = new ExampleClient(1609, "127.0.0.1", 1615);
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
            //        else if (key == ConsoleKey.D1)
            //        {
            //            client1.Dispose();
            //            client1 = new ExampleClient(1609, "127.0.0.1", 1615);
            //        }
            //    }
            //}

            //client1.Dispose();
            //client2.Dispose();
        }
    }
}