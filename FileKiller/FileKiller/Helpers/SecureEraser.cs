using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Zintom.FileKiller.Helpers
{
    internal class SecureEraser
    {

        private static readonly RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetDiskFreeSpaceW([In, MarshalAs(UnmanagedType.LPWStr)] string lpRootPathName, out uint lpSectorsPerCluster, out uint lpBytesPerSector, out uint lpNumberOfFreeClusters, out uint lpTotalNumberOfClusters);

        /// <summary>
        /// Gets the cluster size for the drive that the given file resides on.
        /// </summary>
        internal static int GetClusterSize(FileInfo fileInfo)
        {
            GetDiskFreeSpaceW(fileInfo.GetDriveInfo().Name, out uint sectorsPerCluster, out uint bytesPerSector, out _, out _);
            return (int)(bytesPerSector * sectorsPerCluster);
        }

        /// <summary>
        /// Overwrites all the bytes in the given <paramref name="stream"/> to 0.
        /// <para>
        /// <b>Warning: </b> This is a destructive method.
        /// </para>
        /// </summary>
        internal static void ZeroStream(Stream stream)
        {
            long remaining = stream.Length;

            Span<byte> zeroData = stackalloc byte[4096];
            zeroData.Clear();

            stream.Position = 0;
            while (remaining > 0)
            {
                if (remaining > 4096)
                {
                    stream.Write(zeroData);
                    remaining -= zeroData.Length;
                }
                else
                {
                    stream.Write(zeroData.Slice(0, (int)remaining));
                    break;
                }
            }

            stream.Flush();
        }

        /// <summary>
        /// Zero's the bytes in the cluster tip of the given file if a cluster tip is present.
        /// <para>
        /// <b>Warning: </b> This is a destructive method. The file is not shrunk back down once the bytes are written.
        /// </para>
        /// </summary>
        /// <param name="fileStream"></param>
        /// <param name="clusterSize">The cluster size for the given file.</param>
        /// <returns><see langword="true"/> if a cluster tip was cleared, or <see langword="false"/> if the stream length is a multiple of the cluster size (therefore no cluster tip exists).</returns>
        internal static bool ZeroClusterTip(Stream fileStream, int clusterSize)
        {
            if (fileStream.Length == 0) { return false; }

            int remainder = (int)(fileStream.Length % clusterSize);

            if (remainder == 0)
            {
                Debug.WriteLine("The given stream length is a multiple of the cluster size therefore there is no cluster tip.");
                return false;
            }

            int clusterTipSize = (int)(clusterSize - remainder);

            // Create a block of zeros which we will use to fill the cluster tip.
            Span<byte> zeros = clusterTipSize <= 8192 ?
                               stackalloc byte[clusterTipSize] :
                               new byte[clusterTipSize];
            zeros.Clear();

            fileStream.Position = fileStream.Length;
            fileStream.Write(zeros);
            fileStream.Flush();

            return true;
        }

        internal static bool ClearFileMetaData(FileInfo fileInfo)
        {
            var year2000 = new DateTime(2000, 1, 1);
            fileInfo.CreationTime = year2000;
            fileInfo.CreationTimeUtc = year2000;
            fileInfo.LastAccessTime = year2000;
            fileInfo.LastAccessTimeUtc = year2000;
            fileInfo.LastWriteTime = year2000;
            fileInfo.LastWriteTimeUtc = year2000;

            // Get the original file name and extension
            ReadOnlySpan<char> oldFileName = Path.GetFileNameWithoutExtension(fileInfo.FullName.AsSpan());
            ReadOnlySpan<char> oldExt = Path.GetExtension(fileInfo.FullName.AsSpan());

            // Allocate new memory which is the same size as the old file name and extension.
            Span<char> newFileName = stackalloc char[oldFileName.Length];
            Span<char> newFileExt = stackalloc char[oldExt.Length];

            // Generate a random file name.
            for (int i = 0; i < newFileName.Length; i++)
            {
                Span<char> character = newFileName.Slice(i, 1);
                FillWithRandomValidAlphaNumeric(character);
            }

            // Generate a random extension.
            for (int i = 0; i < newFileExt.Length; i++)
            {
                Span<char> character = newFileExt.Slice(i, 1);
                FillWithRandomValidAlphaNumeric(character);
            }

            fileInfo.MoveTo(fileInfo.DirectoryName + "\\" +
                            newFileName.ToString() + "\\" +
                            newFileExt.ToString());

            return true;
        }

        /// <summary>
        /// Fills the given two-byte <see cref="Span{T}"/> with an random alphanumeric character.
        /// </summary>
        /// <param name="character"></param>
        private static void FillWithRandomValidAlphaNumeric(Span<char> character)
        {
            if (character.Length != 1) throw new ArgumentException("The given Span must 1 character wide.");

            // This is our temporary storage location for our random data.
            Span<byte> tmpRndStore = stackalloc byte[2];

            // Fill the temporary storage location with random bytes.
            rng.GetBytes(tmpRndStore);

            // Clamp the value of the character between 65 and 90.
            short newValue = (short)ClampModulo((uint)BinaryPrimitives.ReadInt16LittleEndian(tmpRndStore), 65, 90);

            character[0] = BitConverter.ToChar(tmpRndStore);
        }

        private static uint ClampModulo(uint value, uint min, uint max)
        {
            // To retain entropy during the clamping process
            // We move our range down to zero (by taking away `min` from each operand in the formula), modulo the `value`, then move it back to the correct range by adding `min` back.
            return ((value - min) % (max - min + 1)) + min;
        }

    }
}
