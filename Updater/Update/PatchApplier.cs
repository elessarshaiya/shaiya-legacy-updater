using System;
using System.IO;

namespace Shaiya_Invasion_Updater
{
    public static class PatchApplier
    {
        public static void ApplyExtractedPatch(string extractedDir, PatchManifestItem item)
        {
            string patchName = item == null ? "manual" : (item.version.ToString("D4") + "_" + Path.GetFileName(item.file ?? "patch"));
            using (PatchTransaction tx = new PatchTransaction(patchName))
            {
                try
                {
                    ApplyExtractedPatchInternal(extractedDir, tx);
                    tx.Complete();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        public static void ApplyExtractedPatch(string extractedDir)
        {
            ApplyExtractedPatch(extractedDir, null);
        }

        private static void ApplyExtractedPatchInternal(string extractedDir, PatchTransaction tx)
        {
            string updateSah = Path.Combine(extractedDir, "update.sah");
            string updateSaf = Path.Combine(extractedDir, "update.saf");

            if (File.Exists(updateSah) && File.Exists(updateSaf))
                ApplySahPatch(updateSah, updateSaf, tx);

            CopyPatchFiles(extractedDir, AppPaths.BaseDirectory, tx);
        }

        private static void ApplySahPatch(string updateSah, string updateSaf, PatchTransaction tx)
        {
            string dataSah = AppPaths.InBase("data.sah");
            string dataSaf = AppPaths.InBase("data.saf");
            if (!File.Exists(dataSah) || !File.Exists(dataSaf)) throw new FileNotFoundException("data.sah/data.saf is missing.");

            tx.BackupFile(dataSah);
            tx.BackupFile(dataSaf);

            AppLogger.Patch("SAH patch started. data.sah state=" + SahCrypt.GetState(dataSah) + ", update.sah state=" + SahCrypt.GetState(updateSah));

            if (AppConfig.SahCryptEnabled)
            {
                SahCrypt.UnlockIfNeeded(dataSah, null);

                if (AppConfig.SahCryptUnlockPatchSah)
                    SahCrypt.UnlockIfNeeded(updateSah, null);
            }

            SAH client = new SAH(dataSah);
            SAH patch = new SAH(updateSah);
            if (!client.OK) throw new InvalidDataException("data.sah could not be read.");
            if (!patch.OK) throw new InvalidDataException("update.sah could not be read.");
            client.MergeWithPatch(patch);

            if (AppConfig.SahCryptEnabled && AppConfig.SahCryptProtectDataSahAfterPatch)
            {
                SahCrypt.ProtectIfNeeded(dataSah, null);
                if (SahCrypt.GetState(dataSah) != SahCryptState.EncryptedEsah)
                    throw new InvalidDataException("ProtectDataSahAfterPatch=true, but data.sah is not ESAH encrypted after patch.");
            }

            AppLogger.Patch("SAH patch completed. data.sah state=" + SahCrypt.GetState(dataSah));
        }

        private static void CopyPatchFiles(string source, string destination, PatchTransaction tx)
        {
            foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                string rel = GetRelativePath(source, dir);
                Directory.CreateDirectory(SafeTarget(destination, rel));
            }

            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (name.Equals("update.sah", StringComparison.OrdinalIgnoreCase) || name.Equals("update.saf", StringComparison.OrdinalIgnoreCase)) continue;
                string rel = GetRelativePath(source, file);
                string target = SafeTarget(destination, rel);
                tx.BackupFile(target);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
                AppLogger.Patch("Copied patch file: " + rel);
            }
        }

        public static string SafeTarget(string destination, string relativePath)
        {
            if (Path.IsPathRooted(relativePath)) throw new InvalidDataException("Patch path is rooted: " + relativePath);
            string root = Path.GetFullPath(destination);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString())) root += Path.DirectorySeparatorChar;
            string target = Path.GetFullPath(Path.Combine(destination, relativePath));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Patch target is outside client folder: " + relativePath);
            return target;
        }

        private static string GetRelativePath(string root, string path)
        {
            Uri rootUri = new Uri(AppendSlash(Path.GetFullPath(root)));
            Uri pathUri = new Uri(Path.GetFullPath(path));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private static string AppendSlash(string path) { return path.EndsWith(Path.DirectorySeparatorChar.ToString()) ? path : path + Path.DirectorySeparatorChar; }
    }
}
