using System;
using System.Collections.Generic;
using System.IO;

namespace Shaiya_Invasion_Updater
{
    public sealed class IniFile
    {
        private readonly Dictionary<string, Dictionary<string, string>> _sections =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public static IniFile Load(string path)
        {
            IniFile ini = new IniFile();
            if (!File.Exists(path)) return ini;

            string section = "General";
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                ini.Set(section, key, value);
            }
            return ini;
        }

        public static IniFile LoadText(string text)
        {
            IniFile ini = new IniFile();
            if (string.IsNullOrEmpty(text)) return ini;

            string section = "General";
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                ini.Set(section, key, value);
            }
            return ini;
        }

        public string Get(string section, string key, string defaultValue)
        {
            Dictionary<string, string> values;
            if (!_sections.TryGetValue(section, out values)) return defaultValue;
            string value;
            return values.TryGetValue(key, out value) ? value : defaultValue;
        }

        public int GetInt(string section, string key, int defaultValue)
        {
            int v;
            return int.TryParse(Get(section, key, defaultValue.ToString()), out v) ? v : defaultValue;
        }

        public bool GetBool(string section, string key, bool defaultValue)
        {
            string v = Get(section, key, defaultValue ? "true" : "false");
            return v.Equals("1") || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        public void Set(string section, string key, string value)
        {
            Dictionary<string, string> values;
            if (!_sections.TryGetValue(section, out values))
            {
                values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _sections[section] = values;
            }
            values[key] = value;
        }
    }
}
