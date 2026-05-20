using System;
using System.Collections.Generic;
using System.IO;

namespace Shaiya_Invasion_Updater
{
    public sealed class PatchTransaction : IDisposable
    {
        private sealed class BackupRecord
        {
            public string Target;
            public string Backup;
            public bool Existed;
        }

        private readonly string _baseDir;
        private readonly string _backupRoot;
        private readonly List<BackupRecord> _records = new List<BackupRecord>();
        private bool _completed;

        public string BackupRoot { get { return _backupRoot; } }

        public PatchTransaction(string patchName)
        {
            _baseDir = Path.GetFullPath(AppPaths.BaseDirectory);
            _backupRoot = AppPaths.InBase(Path.Combine("backup", DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + SafeName(patchName)));
            Directory.CreateDirectory(_backupRoot);
            AppLogger.Patch("Patch transaction started. BackupRoot=" + _backupRoot);
        }

        public void BackupFile(string target)
        {
            target = Path.GetFullPath(target);
            EnsureInsideClient(target);

            foreach (BackupRecord existing in _records)
                if (string.Equals(existing.Target, target, StringComparison.OrdinalIgnoreCase)) return;

            bool existed = File.Exists(target);
            string rel = GetRelativePath(_baseDir, target);
            string backup = Path.Combine(_backupRoot, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (existed) File.Copy(target, backup, true);

            _records.Add(new BackupRecord { Target = target, Backup = backup, Existed = existed });
            AppLogger.Patch("Backed up target=" + target + " existed=" + existed);
        }

        public void Complete()
        {
            _completed = true;
            AppLogger.Patch("Patch transaction completed.");
            if (!AppConfig.KeepSuccessfulBackups)
            {
                try { if (Directory.Exists(_backupRoot)) Directory.Delete(_backupRoot, true); } catch (Exception ex) { AppLogger.PatchWarn("Could not delete backup directory: " + ex.Message); }
            }
        }

        public void Rollback()
        {
            AppLogger.PatchWarn("Patch transaction rollback started.");
            for (int i = _records.Count - 1; i >= 0; i--)
            {
                BackupRecord record = _records[i];
                try
                {
                    if (record.Existed)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(record.Target));
                        File.Copy(record.Backup, record.Target, true);
                    }
                    else
                    {
                        if (File.Exists(record.Target)) File.Delete(record.Target);
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.PatchError("Rollback failed for " + record.Target, ex);
                }
            }
            AppLogger.PatchWarn("Patch transaction rollback finished.");
        }

        public void Dispose()
        {
            if (!_completed && _records.Count > 0)
            {
                try { Rollback(); } catch { }
            }
        }

        private void EnsureInsideClient(string fullPath)
        {
            string baseWithSlash = _baseDir.EndsWith(Path.DirectorySeparatorChar.ToString()) ? _baseDir : _baseDir + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(baseWithSlash, StringComparison.OrdinalIgnoreCase) && !string.Equals(fullPath, _baseDir, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Patch tried to touch a file outside client directory: " + fullPath);
        }

        private static string SafeName(string value)
        {
            value = string.IsNullOrWhiteSpace(value) ? "patch" : value;
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value;
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
