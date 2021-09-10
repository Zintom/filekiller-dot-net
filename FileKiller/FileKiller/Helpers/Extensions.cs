using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;

namespace Zintom.FileKiller.Helpers
{
    internal static class Extensions
    {

        /// <summary>
        /// Gets the <see cref="DriveInfo"/> for the given <paramref name="fileInfo"/>
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <exception cref="FormatException"/>
        /// <returns></returns>
        internal static DriveInfo GetDriveInfo(this FileInfo fileInfo)
        {
            string? root = Path.GetPathRoot(fileInfo.FullName);

            if (string.IsNullOrEmpty(root))
            {
                throw new FormatException("The given file path was not absolute (does not have a root) and therefore the drive info could not be identified.");
            }

            return new DriveInfo(root);
        }

        private struct HarddriveIdentifier
        {
            public readonly string DriveNumber;

            /// <summary>
            /// Usually 'SSD' or 'HDD'
            /// </summary>
            public readonly string MediaType;

            public HarddriveIdentifier(string driveNumber, string mediaType) : this()
            {
                DriveNumber = driveNumber;
                MediaType = mediaType;
            }
        }

        private static IEnumerable<HarddriveIdentifier> GetPhysicalDisksInfo()
        {
            Process powerShell = new Process();
            powerShell.StartInfo.FileName = "PowerShell";
            powerShell.StartInfo.Arguments = "-Command \"${result}=Get-PhysicalDisk; Foreach ($d in $result) { if ($d.Number -And $d.MediaType) { echo $d.Number; echo $d.MediaType; } }\"";
            powerShell.StartInfo.UseShellExecute = false;
            powerShell.StartInfo.RedirectStandardOutput = true;
            powerShell.Start();

            powerShell.WaitForExit();

            string[] output = powerShell.StandardOutput.ReadToEnd().Split(Environment.NewLine);

            // Data is formatted as:
            // SerialNumber
            // MediaType
            // i.e pairs of data separated by newlines, therefore if the output is odd, then the data cant be valid as it is not in pairs.
            if (output.Length % 2 == 0)
            {
                yield break;
            }

            for (int i = 0; i < output.Length; i += 2)
            {
                yield return new HarddriveIdentifier(output[i], output[i + 1]);
            }
        }

        internal static string GetDriveMediaType(this DriveInfo drive)
        {
            var result = GetPhysicalDisksInfo();

            foreach (var r in result)
            {
                Debug.WriteLine(r.ToString());
            }

            return "";

            Debug.WriteLine("Volume: " + drive.Name);
            string driveLetter = drive.Name.Substring(0, drive.Name.IndexOf(":"));

            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");

            foreach(var mo in searcher.Get())
            {
                Debug.WriteLine("\n\nManagement Object: \n");
                foreach(var property in mo.Properties)
                {
                    Debug.WriteLine($"Prop Name: {property.Name}, Prop Value: {property.Value}");
                }
            }

            return "";
        }

    }
}
