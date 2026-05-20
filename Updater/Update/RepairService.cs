using System;
using System.IO;
using System.Net;

namespace Shaiya_Invasion_Updater
{
    public static class RepairService
    {
        public static void VerifyAndRepair(LauncherManifest manifest, Action<int, string> progress)
        {
            if (manifest == null) throw new InvalidOperationException("Manifest is not loaded.");
            ManifestFileItem[] files = manifest.files ?? new ManifestFileItem[0];
            if (files.Length == 0) throw new InvalidOperationException("Server manifest does not contain a repair file list.");

            GameProcessGuard.ThrowIfGameRunningForPatch();

            int repaired = 0;
            for (int i = 0; i < files.Length; i++)
            {
                ManifestFileItem item = files[i];
                string rel = PatchPathUtil.NormalizeRelativeFilePath(!string.IsNullOrWhiteSpace(item.path) ? item.path : item.file);
                string target = PatchApplier.SafeTarget(AppPaths.BaseDirectory, rel);
                int percent = (int)((i * 100.0) / Math.Max(1, files.Length));
                if (progress != null) progress(percent, "Verifying " + rel);

                bool missing = !File.Exists(target);
                bool badSize = !missing && item.size > 0 && new FileInfo(target).Length != item.size;
                bool badHash = false;
                if (!missing && !badSize && !string.IsNullOrWhiteSpace(item.sha256))
                {
                    string actual = HashUtil.Sha256File(target);
                    badHash = !string.Equals(actual, item.sha256, StringComparison.OrdinalIgnoreCase);
                }

                if (!missing && !badSize && !badHash) continue;

                if (progress != null) progress(percent, "Repairing " + rel);
                AppLogger.PatchWarn("Repairing file: " + rel + " missing=" + missing + " badSize=" + badSize + " badHash=" + badHash);
                DownloadRepairFile(item, target);
                repaired++;
            }

            if (progress != null) progress(100, repaired == 0 ? "All files verified." : "Repair completed. Files repaired: " + repaired);
            AppLogger.Patch("Repair completed. Files repaired=" + repaired);
        }

        private static void DownloadRepairFile(ManifestFileItem item, string target)
        {
            string file = !string.IsNullOrWhiteSpace(item.file) ? item.file : item.path;
            if (string.IsNullOrWhiteSpace(file)) throw new InvalidDataException("Repair file entry has no file/path value.");
            string url = AppConfig.CombineUrl(AppConfig.BaseUrl, file.Replace('\\', '/').TrimStart('/'));
            string tmp = target + ".download";
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            if (File.Exists(tmp)) File.Delete(tmp);

            Exception last = null;
            for (int attempt = 1; attempt <= AppConfig.DownloadRetryCount; attempt++)
            {
                try
                {
                    using (TimeoutWebClient wc = new TimeoutWebClient(AppConfig.DownloadTimeoutSeconds * 1000))
                    {
                        wc.Headers[HttpRequestHeader.UserAgent] = "LegacyShaiyaUpdater/1.0";
                        wc.DownloadFile(url, tmp);
                    }
                    if (item.size > 0 && new FileInfo(tmp).Length != item.size)
                        throw new InvalidDataException("Repair size mismatch for " + file + ".");
                    if (!string.IsNullOrWhiteSpace(item.sha256))
                    {
                        string actual = HashUtil.Sha256File(tmp);
                        if (!string.Equals(actual, item.sha256, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("Repair SHA256 mismatch for " + file + ".");
                    }

                    using (PatchTransaction tx = new PatchTransaction("repair_" + Path.GetFileName(target)))
                    {
                        tx.BackupFile(target);
                        File.Copy(tmp, target, true);
                        tx.Complete();
                    }
                    try { File.Delete(tmp); } catch { }
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                    AppLogger.PatchWarn("Repair download attempt " + attempt + " failed for " + file + ": " + ex.Message);
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                }
            }
            throw new InvalidOperationException("Repair failed for " + file + ": " + (last == null ? "unknown error" : last.Message), last);
        }
    }
}
