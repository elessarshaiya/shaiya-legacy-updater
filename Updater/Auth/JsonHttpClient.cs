using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace Shaiya_Invasion_Updater
{
    public static class JsonHttpClient
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        static JsonHttpClient()
        {
            try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12; }
            catch { }
        }

        public static T Get<T>(string url)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = 15000;
            req.ReadWriteTimeout = 15000;
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                string json = sr.ReadToEnd();
                return Serializer.Deserialize<T>(json);
            }
        }

        public static T Post<T>(string url, object body)
        {
            string json = Serializer.Serialize(body);
            byte[] data = Encoding.UTF8.GetBytes(json);
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/json";
            req.Accept = "application/json";
            req.Timeout = 15000;
            req.ReadWriteTimeout = 15000;
            req.ContentLength = data.Length;
            using (Stream s = req.GetRequestStream()) s.Write(data, 0, data.Length);
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                string response = sr.ReadToEnd();
                return Serializer.Deserialize<T>(response);
            }
        }

        public static string ToJson(object value) { return Serializer.Serialize(value); }
        public static T FromJson<T>(string value) { return Serializer.Deserialize<T>(value); }
    }
}
