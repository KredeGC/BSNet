using BSNet;
using BSNet.Datagram;
using BSNet.Stream;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BSNetTest
{
    [TestClass]
    public class HeaderTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void HeaderSerializeTest()
        {
            byte type = ConnectionType.MESSAGE;
            ushort sequence = 416;
            ushort ack = 412;
            ushort ackBits = 4214;
            ulong token = 79832678;
            uint randomValue = 0xBEEBB00B;

            byte[] rawBytes;
            using (NewWriter writer = NewWriter.GetWriter(Header.HEADER_SIZE))
            {
                using (Header header = Header.GetHeader(type, sequence, ack, ackBits, token))
                {
                    header.Serialize(writer);
                }

                writer.SerializeUInt(randomValue);

                rawBytes = writer.ToArray();
            }

            Assert.AreEqual(rawBytes.Length, Header.HEADER_SIZE + 4);

            using (NewReader reader = NewReader.GetReader(rawBytes))
            {
                using (Header header = Header.GetHeader(reader))
                {
                    Assert.AreEqual(header.Type, type);
                    Assert.AreEqual(header.Sequence, sequence);
                    Assert.AreEqual(header.Ack, ack);
                    Assert.AreEqual(header.AckBits, ackBits);
                    Assert.AreEqual(header.Token, token);
                }

                Assert.AreEqual(reader.SerializeUInt(), randomValue);
            }
        }

        [TestMethod]
        public void HeaderPaddingChecksumTest()
        {
            byte[] version = new byte[] { 0xC0, 0xDE, 0xCA, 0xFE };
            byte type = ConnectionType.CONNECT;
            ushort sequence = 416;
            ushort ack = 412;
            ushort ackBits = 4214;
            ulong token = 0xC0DECAFEBEEBB00B;

            byte[] rawBytes;
            using (NewWriter writer = NewWriter.GetWriter(Header.HEADER_SIZE))
            {
                using (Header header = Header.GetHeader(type, sequence, ack, ackBits, token))
                {
                    header.Serialize(writer);
                }

                writer.PadToEnd();

                writer.SerializeChecksum(version);

                rawBytes = writer.ToArray();
            }

            Assert.AreEqual(rawBytes.Length, BSUtility.RECEIVE_BUFFER_SIZE);

            using (NewReader reader = NewReader.GetReader(rawBytes))
            {
                Assert.IsTrue(reader.SerializeChecksum(version));

                using (Header header = Header.GetHeader(reader))
                {
                    Assert.AreEqual(header.Type, type);
                    Assert.AreEqual(header.Sequence, sequence);
                    Assert.AreEqual(header.Ack, ack);
                    Assert.AreEqual(header.AckBits, ackBits);
                    Assert.AreEqual(header.Token, token);
                }
            }
        }
    }
}
