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
            ConnectionType type = ConnectionType.Message;
            ushort sequence = 416;
            ushort ack = 412;
            ushort ackBits = 4214;
            ulong token = 79832678;
            uint randomValue = 0xBEEBB00B;

            byte[] rawBytes;
            using (BSWriter writer = BSWriter.Get(Header.HEADER_SIZE))
            {
                using (Header header = Header.GetHeader(type, sequence, ack, ackBits, token))
                {
                    header.Serialize(writer);
                }

                writer.SerializeUInt(randomValue);

                rawBytes = writer.ToArray();
            }

            Assert.AreEqual(Header.HEADER_SIZE + 4, rawBytes.Length);

            using (BSReader reader = BSReader.Get(rawBytes))
            {
                using (Header header = Header.GetHeader(reader))
                {
                    Assert.AreEqual(type, header.Type);
                    Assert.AreEqual(sequence, header.Sequence);
                    Assert.AreEqual(ack, header.Ack);
                    Assert.AreEqual(ackBits, header.AckBits, ackBits);
                    Assert.AreEqual(token, header.Token);
                }

                Assert.AreEqual(randomValue, reader.SerializeUInt());
            }
        }

        [TestMethod]
        public void HeaderPaddingChecksumTest()
        {
            byte[] version = new byte[] { 0xC0, 0xDE, 0xCA, 0xFE };
            ConnectionType type = ConnectionType.Connect;
            ushort sequence = 416;
            ushort ack = 412;
            uint ackBits = 0b0011001100110011;
            ulong token = 0xC0DECAFEBEEBB00B;

            byte[] rawBytes;
            using (BSWriter writer = BSWriter.Get(Header.HEADER_SIZE))
            {
                using (Header header = Header.GetHeader(type, sequence, ack, ackBits, token))
                {
                    header.Serialize(writer);
                }

                writer.PadToEnd();

                writer.SerializeChecksum(version);

                rawBytes = writer.ToArray();
            }

            Assert.AreEqual(BSUtility.PACKET_MAX_SIZE, rawBytes.Length);

            using (BSReader reader = BSReader.Get(rawBytes))
            {
                Assert.IsTrue(reader.SerializeChecksum(version));

                using (Header header = Header.GetHeader(reader))
                {
                    Assert.AreEqual(type, header.Type);
                    Assert.AreEqual(sequence, header.Sequence);
                    Assert.AreEqual(ack, header.Ack);
                    Assert.AreEqual(ackBits, header.AckBits, ackBits);
                    Assert.AreEqual(token, header.Token);
                }
            }
        }
    }
}
