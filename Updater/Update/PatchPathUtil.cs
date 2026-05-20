using System;
using System.IO;

namespace Shaiya_Invasion_Updater
{
    public static class PatchPathUtil
    {
        public static string GetPatchUrl(PatchManifestItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.file)) throw new InvalidDataException("Patch file is missing in manifest.");
            string file = item.file.Replace('\\', '/').TrimStart('/');
            if (file.StartsWith("Patches/", StringComparison.OrdinalIgnoreCase))
                return AppConfig.CombineUrl(AppConfig.BaseUrl, file);
            return AppConfig.CombineUrl(AppConfig.BaseUrl, "Patches/" + file);
        }

        public static string GetPatchLocalPath(string downloadDir, PatchManifestItem item)
        {
            string fileName = Path.GetFileName((item.file ?? string.Empty).Replace('\\', '/'));
            if (string.IsNullOrWhiteSpace(fileName)) throw new InvalidDataException("Invalid patch file path in manifest.");
            return Path.Combine(downloadDir, fileName);
        }

        public static string NormalizeRelativeFilePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("Manifest file path is empty.");
            string rel = path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(rel) || rel.Contains(".." + Path.DirectorySeparatorChar) || rel == ".." || rel.StartsWith(".." + Path.DirectorySeparatorChar))
                throw new InvalidDataException("Unsafe manifest file path: " + path);
            return rel;
        }
    }
}
