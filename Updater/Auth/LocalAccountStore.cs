using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace Shaiya_Invasion_Updater
{
    public static class LocalAccountStore
    {
        private static readonly object Sync = new object();
        private static string StorePath { get { return AppPaths.InBase("saved_accounts.local.json"); } }

        public static List<string> Load()
        {
            try
            {
                if (!File.Exists(StorePath)) return new List<string>();
                string json = File.ReadAllText(StorePath);
                List<string> accounts = new JavaScriptSerializer().Deserialize<List<string>>(json);
                return accounts ?? new List<string>();
            }
            catch (Exception ex)
            {
                ExceptionManager.Submit(ex);
                return new List<string>();
            }
        }

        public static void Save(IEnumerable<string> accounts)
        {
            try
            {
                lock (Sync)
                {
                    string json = new JavaScriptSerializer().Serialize(new List<string>(accounts));
                    File.WriteAllText(StorePath, json);
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.Submit(ex);
            }
        }

        public static void Add(string userId)
        {
            List<string> accounts = Load();
            if (!accounts.Exists(x => string.Equals(x, userId, StringComparison.OrdinalIgnoreCase)))
                accounts.Add(userId);
            Save(accounts);
        }

        public static void Remove(string userId)
        {
            List<string> accounts = Load();
            accounts.RemoveAll(x => string.Equals(x, userId, StringComparison.OrdinalIgnoreCase));
            Save(accounts);
        }
    }
}
