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
            return request;
        }
    }
}
