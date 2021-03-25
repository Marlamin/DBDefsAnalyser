using System;
using System.Net;

namespace DBDefsAnalyser.Utils
{
    public class WebClientEx : WebClient
    {
        public DecompressionMethods AutomaticDecompression { get; set; }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = (HttpWebRequest)base.GetWebRequest(address);
            request.AutomaticDecompression = AutomaticDecompression;
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/89.0.4389.90 Safari/537.36 Edg/89.0.774.57";
            return request;
        }
    }
}
