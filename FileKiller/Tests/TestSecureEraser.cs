using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Zintom.FileKiller.Helpers;

namespace Tests
{
    [TestClass]
    public class TestSecureEraser
    {
        [TestMethod]
        public void SecureEraserZeroStream_AllZeros()
        {
            // Create a nice piece of ordered data, each array item value increments by one, modulo 255.
            byte[] orderedData = new byte[ushort.MaxValue];
            for (int i = 1; i < orderedData.Length; i++)
            {
                orderedData[i] = (byte)(i % 255);
            }

            // Wrap our data in a stream.
            var stream = new MemoryStream(orderedData);

            // Zero the stream.
            SecureEraser.ZeroStream(stream);

            // Check that all bytes are zero.
            byte[] streamAsBytes = stream.ToArray();
            for (int i = 0; i < streamAsBytes.Length; i++)
            {
                Assert.IsTrue(streamAsBytes[i] == 0);
            }
        }

        [TestMethod]
        public void SecureEraseClusterTip_AllZeros()
        {
            var stream = new MemoryStream();
            stream.Write(stackalloc byte[8190]);
            stream.Write(stackalloc byte[2] { 6, 9});
            // Total stream size at this point is 4096

            // Re-size the stream down to leave two bytes at the end of the "cluster".
            stream.SetLength(8190);
            stream.Position = 8190;

            /// For this test we use the NTFS default cluster size of 4096.
            SecureEraser.ZeroClusterTips(stream, 4096);

            // The zero cluster tips method should have written out zeros to the end of the cluster size,
            // meaning the length should have increased by two.
            Assert.IsTrue(stream.Length == 8192);

            // Here we ensure that the bytes we previous set as '6' and '9' have been written over.
            stream.Position = 8190;
            int was6 = stream.ReadByte();
            int was9 = stream.ReadByte();

            Assert.IsTrue(was6 == 0 && was9 == 0);
        }
    }
}
