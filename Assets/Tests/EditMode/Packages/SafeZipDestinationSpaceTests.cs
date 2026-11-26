using System;
using System.IO;
using NUnit.Framework;
using OpenSekai.Infrastructure.Packages;

namespace OpenSekai.Tests.EditMode.Packages
{
    public sealed class SafeZipDestinationSpaceTests
    {
#if UNITY_EDITOR_LINUX
        [Test]
        public void MissingDestinationReportsUnknownFreeSpace()
        {
            string missing = Path.Combine(Path.GetTempPath(), "OpenSekai-Missing-" + Guid.NewGuid().ToString("N"));
            Assert.That(SafeZipExtractor.LinuxAvailableBytes(missing), Is.Null);
        }

        [Test]
        public void SeparateMountUsesItsOwnAvailableSpace()
        {
            const string target = "/dev/shm";
            if (!Directory.Exists(target) || !File.Exists("/bin/df"))
                Assert.Ignore("Requires a Linux shared-memory mount and df.");
            long before = ReadAvailableBytes(target);
            long? observed = SafeZipExtractor.GetAvailableBytes(target);
            long after = ReadAvailableBytes(target);
            // df rounds to 1 KiB and other processes can use the mount during sampling.
            Assert.That(observed, Is.InRange(Math.Min(before, after) - 1024, Math.Max(before, after) + 1024));
        }

        private static long ReadAvailableBytes(string path)
        {
            var info = new System.Diagnostics.ProcessStartInfo("/bin/df", "-kP " + path)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            info.EnvironmentVariables["LC_ALL"] = "C";
            using var process = System.Diagnostics.Process.Start(info);
            string[] lines = process.StandardOutput.ReadToEnd().TrimEnd().Split('\n');
            process.WaitForExit();
            Assert.That(process.ExitCode, Is.Zero);
            return long.Parse(lines[lines.Length - 1].Split((char[])null, StringSplitOptions.RemoveEmptyEntries)[3],
                System.Globalization.CultureInfo.InvariantCulture) * 1024;
        }
#endif

        [Test]
        public void StagingDirectoryOnTheCurrentFilesystemReportsAvailableBytes()
        {
            string directory = Path.Combine(Path.GetTempPath(), "OpenSekai-Space-" + Guid.NewGuid().ToString("N"));
            try
            {
                long? available = SafeZipExtractor.GetAvailableBytes(directory);
                Assert.That(Directory.Exists(directory), Is.True);
                Assert.That(available, Is.Not.Null);
                Assert.That(available.Value, Is.GreaterThanOrEqualTo(0L));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory);
            }
        }
    }
}
