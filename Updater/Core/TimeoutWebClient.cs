using System;
using System.Net;

namespace Shaiya_Invasion_Updater
{
    public sealed class TimeoutWebClient : WebClient
    {
        private readonly int _timeoutMs;

        public TimeoutWebClient(int timeoutMs)
        {
            _timeoutMs = timeoutMs <= 0 ? 30000 : timeoutMs;
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest request = base.GetWebRequest(address);
            request.Timeout = _timeoutMs;
            HttpWebRequest http = request as HttpWebRequest;
            if (http != null)
            {
                http.ReadWriteTimeout = _timeoutMs;
                http.KeepAlive = false;
            }
            return request;
        }
    }
}
