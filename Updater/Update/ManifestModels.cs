namespace Shaiya_Invasion_Updater
{
    public class LauncherManifest
    {
        public int clientVersion { get; set; }
        public int launcherVersion { get; set; }
        public int minLauncherVersion { get; set; }
        public bool maintenance { get; set; }
        public string maintenanceMessage { get; set; }
        public string launcherFile { get; set; }
        public PatchManifestItem[] patches { get; set; }
        public ManifestFileItem[] files { get; set; }
        public NewsItem[] news { get; set; }
    }

    public class PatchManifestItem
    {
        public int version { get; set; }
        public string file { get; set; }
        public string sha256 { get; set; }
        public long size { get; set; }
        public bool encrypted { get; set; }
    }

    public class ManifestFileItem
    {
        public string path { get; set; }
        public string file { get; set; }
        public string sha256 { get; set; }
        public long size { get; set; }
        public bool required { get; set; }
    }

    public class NewsItem
    {
        public string title { get; set; }
        public string url { get; set; }
        public string text { get; set; }
    }
}
