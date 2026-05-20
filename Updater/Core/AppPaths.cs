using System;
using System.IO;
using System.Diagnostics;

namespace Shaiya_Invasion_Updater
{
    public static class AppPaths
    {
        public static string BaseDirectory
        {
            get
            {
                string exe = Process.GetCurrentProcess().MainModule.FileName;
                return Path.GetDirectoryName(exe) ?? AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        public static string Combine(params string[] parts)
        {
            if (parts == null || parts.Length == 0) return BaseDirectory;
            string path = parts[0];
            for (int i = 1; i < parts.Length; i++) path = Path.Combine(path, parts[i]);
            return path;
        }

        public static string InBase(string relative) { return Path.Combine(BaseDirectory, relative); }
    }
}
