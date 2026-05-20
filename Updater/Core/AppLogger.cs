using System;
using System.IO;

namespace Shaiya_Invasion_Updater
{
    public static class AppLogger
    {
        private static readonly object Sync = new object();

        public static void Info(string message) { Write("launcher", "INFO", message, null); }
        public static void Warn(string message) { Write("launcher", "WARN", message, null); }
        public static void Error(string message, Exception ex = null) { Write("launcher", "ERROR", message, ex); }

        public static void Login(string message) { Write("login", "INFO", message, null); }
        public static void Patch(string message) { Write("patch", "INFO", message, null); }
        public static void PatchWarn(string message) { Write("patch", "WARN", message, null); }
        public static void PatchError(string message, Exception ex = null) { Write("patch", "ERROR", message, ex); }
        public static void SahCrypt(string message) { Write("sahcrypt", "INFO", message, null); }
        public static void SahCryptError(string message, Exception ex = null) { Write("sahcrypt", "ERROR", message, ex); }

        public static void Write(string category, string level, string message, Exception ex)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(category)) category = "launcher";
                foreach (char c in Path.GetInvalidFileNameChars()) category = category.Replace(c, '_');

                string dir = AppPaths.InBase("logs");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, category + "_" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                lock (Sync)
                {
                    File.AppendAllText(file,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + level + "] " + message + Environment.NewLine +
                        (ex == null ? "" : ex.ToString() + Environment.NewLine));
                }
            }
            catch { }
        }
    }
}
