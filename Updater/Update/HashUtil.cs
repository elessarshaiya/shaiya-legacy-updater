using System;
using System.IO;
using System.Security.Cryptography;

namespace Shaiya_Invasion_Updater
{
    public static class HashUtil
    {
        public static string Sha256File(string file)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream fs = File.OpenRead(file))
            {
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
