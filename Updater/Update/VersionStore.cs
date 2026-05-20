using System;
using System.IO;

namespace Shaiya_Invasion_Updater
{
    public static class VersionStore
    {
        private static string VersionFile { get { return AppPaths.InBase("updater_version.ini"); } }

        public static int ReadClientVersion()
        {
            try
            {
                if (File.Exists(VersionFile))
                {
                    IniFile ini = IniFile.Load(VersionFile);
                    return ini.GetInt("Client", "Version", 0);
                }

                string saf = AppPaths.InBase("data.saf");
                if (File.Exists(saf) && new FileInfo(saf).Length >= 4)
                {
                    using (BinaryReader br = new BinaryReader(File.Open(saf, FileMode.Open, FileAccess.Read, FileShare.Read)))
                    {
                        br.BaseStream.Position = br.BaseStream.Length - 4;
                        int legacy = br.ReadInt32();
                        if (legacy >= 0 && legacy <= 1000)
                        {
                            WriteClean(legacy, "legacy-import", "Imported from data.saf tail.");
                            return legacy;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.Submit(ex);
            }
            WriteClean(0, "initial", "Created new updater_version.ini.");
            return 0;
        }

        public static void StartPatch(int currentVersion, PatchManifestItem patch)
        {
            Write(currentVersion, "Applying", patch == null ? "" : patch.file, patch == null ? 0 : patch.version, "Patch apply started.");
        }

        public static void CommitPatch(int version, PatchManifestItem patch)
        {
            Write(version, "Clean", patch == null ? "" : patch.file, patch == null ? version : patch.version, "Patch apply completed.");
        }

        public static void FailPatch(int currentVersion, PatchManifestItem patch, string message)
        {
            Write(currentVersion, "Failed", patch == null ? "" : patch.file, patch == null ? 0 : patch.version, message);
        }

        public static void WriteClean(int version, string lastPatch, string note)
        {
            Write(version, "Clean", lastPatch, version, note);
        }

        public static void WriteClientVersion(int version)
        {
            WriteClean(version, "manual", "Version updated.");
        }

        private static void Write(int version, string state, string patchFile, int patchVersion, string note)
        {
            string text =
                "[Client]" + Environment.NewLine +
                "Version=" + version + Environment.NewLine +
                "LastPatch=" + Escape(patchFile) + Environment.NewLine +
                "LastPatchVersion=" + patchVersion + Environment.NewLine +
                "LastUpdate=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
                "LastStatus=" + Escape(state) + Environment.NewLine +
                Environment.NewLine +
                "[Recovery]" + Environment.NewLine +
                "State=" + Escape(state) + Environment.NewLine +
                "PendingPatch=" + Escape(patchFile) + Environment.NewLine +
                "PendingPatchVersion=" + patchVersion + Environment.NewLine +
                "Message=" + Escape(note) + Environment.NewLine;
            File.WriteAllText(VersionFile, text);
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
        }
    }
}
