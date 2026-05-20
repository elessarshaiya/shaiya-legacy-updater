using Ionic.Zip;
using System;
using System.IO;

namespace Shaiya_Invasion_Updater
{
    public static class PatchExtractor
    {
        public static void ExtractZipSafe(string zipPath, string destination)
        {
            Directory.CreateDirectory(destination);
            string root = Path.GetFullPath(destination);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString())) root += Path.DirectorySeparatorChar;

            using (ZipFile zip = ZipFile.Read(zipPath))
            {
                foreach (ZipEntry entry in zip)
                {
                    if (string.IsNullOrWhiteSpace(entry.FileName)) continue;
                    string normalizedEntry = entry.FileName.Replace('\\', '/');
                    if (normalizedEntry.StartsWith("/") || normalizedEntry.Contains(":") || normalizedEntry.Contains("../") || normalizedEntry.Equals("..", StringComparison.Ordinal))
                        throw new InvalidDataException("Unsafe patch entry path: " + entry.FileName);

                    string target = Path.GetFullPath(Path.Combine(destination, entry.FileName));
                    if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Unsafe patch entry path: " + entry.FileName);
                }

                foreach (ZipEntry entry in zip)
                {
                    entry.Extract(destination, ExtractExistingFileAction.OverwriteSilently);
                }
            }
        }
    }
}
