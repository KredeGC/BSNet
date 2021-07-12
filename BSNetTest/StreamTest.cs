using BSNet.Stream;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BSNetTest
{
    [TestClass]
    public class StreamTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void WriterPoolTest()
        {
            NewWriter occupy1 = NewWriter.GetWriter(4);
            NewWriter occupy2 = NewWriter.GetWriter(4);

            Assert.AreNotEqual(occupy1, occupy2);

            NewWriter.ReturnWriter(occupy1);
            NewWriter.ReturnWriter(occupy2);
        }

        [TestMethod]
        public void ReaderPoolTest()
        {
            byte[] rawBytes = new byte[1];

            NewReader occupy1 = NewReader.GetReader(rawBytes);
            NewReader occupy2 = NewReader.GetReader(rawBytes);

            Assert.AreNotEqual(occupy1, occupy2);

            NewReader.ReturnReader(occupy1);
            NewReader.ReturnReader(occupy2);
        }

        [TestMethod]
        public void NestedWriterTest()
        {
            using (NewWriter writer1 = NewWriter.GetWriter())
            {
                writer1.SerializeUInt(273U, 11);
                writer1.SerializeUInt(273U, 11);
                writer1.SerializeUInt(273U, 11);
                byte[] writer1Bytes = writer1.ToArray();

                using (NewWriter writer2 = NewWriter.GetWriter())
                {
                    writer2.SerializeBytes(writer1.TotalBits, writer1Bytes, true);

                    byte[] writer2Bytes = writer2.ToArray();

                    Assert.AreEqual(writer1.TotalBits, writer2.TotalBits);
                    Assert.AreEqual(writer1Bytes.Length, writer2Bytes.Length);

                    for (int i = 0; i < writer1Bytes.Length; i++)
                        Assert.AreEqual(writer1Bytes[i], writer2Bytes[i]);
                }
            }
        }

        [TestMethod]
        public void PrecisionReadWriteTest()
        {
            byte[] rawBytes;
            using (NewWriter writer = NewWriter.GetWriter())
            {
                writer.SerializeUInt(1U, 1);
                writer.SerializeUInt(273U, 11);
                writer.SerializeUInt(273U, 17);
                writer.SerializeUInt(16383U, 14);
                writer.SerializeUInt(511U, 11);
                writer.SerializeUInt(0b10101010111111111010101000001111, 32);
                writer.SerializeUInt(211U, 10);
                writer.SerializeUInt(126U, 7);
                rawBytes = writer.ToArray();
            }

            using (NewReader reader = NewReader.GetReader(rawBytes))
            {
                Assert.AreEqual(reader.SerializeUInt(0, 1), 1U);
                Assert.AreEqual(reader.SerializeUInt(0, 11), 273U);
                Assert.AreEqual(reader.SerializeUInt(0, 17), 273U);
                Assert.AreEqual(reader.SerializeUInt(0, 14), 16383U);
                Assert.AreEqual(reader.SerializeUInt(0, 11), 511U);
                Assert.AreEqual(reader.SerializeUInt(0, 32), 0b10101010111111111010101000001111);
                Assert.AreEqual(reader.SerializeUShort(0, 10), 211U);
                Assert.AreEqual(reader.SerializeUShort(0, 7), 126U);
            }
        }

        [TestMethod]
        public void OverflowReadWriteTest()
        {
            byte[] rawBytes;
            using (NewWriter writer1 = NewWriter.GetWriter(1))
            {
                writer1.SerializeUInt(uint.MaxValue, 10);
                writer1.SerializeUInt(uint.MaxValue, 3);
                writer1.SerializeUInt(uint.MaxValue, 11);
                writer1.SerializeUInt(uint.MaxValue, 31);
                rawBytes = writer1.ToArray();
            }

            using (NewReader reader = NewReader.GetReader(rawBytes))
            {
                Assert.AreEqual(reader.SerializeUInt(0, 10), 1023U);
                Assert.AreEqual(reader.SerializeUInt(0, 3), 7U);
                Assert.AreEqual(reader.SerializeUInt(0, 11), 2047U);
                Assert.AreEqual(reader.SerializeUInt(0, 31), 2147483647U);
            }
        }
    }
}
