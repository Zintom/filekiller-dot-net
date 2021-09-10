using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Zintom.FileKiller.Helpers
{
    internal class SecureEraser
    {

        [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetDiskFreeSpaceW([In, MarshalAs(UnmanagedType.LPWStr)]string lpRootPathName, out uint lpSectorsPerCluster, out uint lpBytesPerSector, out uint lpNumberOfFreeClusters, out uint lpTotalNumberOfClusters);

        /// <summary>
        /// Gets the cluster size for the drive that the given file resides on.
        /// </summary>
        internal static int GetClusterSize(FileInfo fileInfo)
        {
            GetDiskFreeSpaceW(fileInfo.GetDriveInfo().Name, out uint sectorsPerCluster, out uint bytesPerSector, out _, out _);
            return (int)(bytesPerSector * sectorsPerCluster);
        }

        /// <summary>
        /// Overwrites the given stream with zeros.
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
        /// 
        /// </summary>
        /// <param name="fileStream"></param>
        /// <param name="clusterSize">The cluster size for the given file.</param>
        /// <returns><see langword="true"/> if a cluster tip was cleared, or <see langword="false"/> if the stream length is a multiple of the cluster size (therefore no cluster tip exists).</returns>
        internal static bool ZeroClusterTips(Stream fileStream, int clusterSize)
        {
            int clusterTipSize = (int)(clusterSize - (fileStream.Length % clusterSize));

            if (clusterTipSize == 0)
            {
                Debug.WriteLine("The given stream length is a multiple of the cluster size therefore there is no cluster tip.");
                return false;
            }

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

    }
}
