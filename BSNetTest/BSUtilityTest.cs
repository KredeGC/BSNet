using BSNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BSNetTest
{
    [TestClass]
    public class BSUtilityTest
    {
        [TestMethod]
        public void SmallTrimTest()
        {
            byte[] originalBytes = new byte[] { 0b11110000, 0b10101010, 0b11110000 };
            byte[] testBytes = BSUtility.Trim(originalBytes, 4, 16);

            byte[] predictedBytes = new byte[] { 0b00001010, 0b10101111 };

            Assert.AreEqual(testBytes.Length, predictedBytes.Length);

            for (int i = 0; i < testBytes.Length; i++)
                Assert.AreEqual(testBytes[i], predictedBytes[i]);
        }

        [TestMethod]
        public void LargeTrimTest()
        {
            byte[] originalBytes = new byte[] { 0b11110000, 0b10101010, 0b11110000 };
            byte[] testBytes = BSUtility.Trim(originalBytes, 11, 7);

            byte[] predictedBytes = new byte[] { 0b01010111 };

            Assert.AreEqual(testBytes.Length, predictedBytes.Length);

            for (int i = 0; i < testBytes.Length; i++)
                Assert.AreEqual(testBytes[i], predictedBytes[i]);
        }
    }
}
