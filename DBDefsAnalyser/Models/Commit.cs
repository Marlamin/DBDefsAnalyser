using Newtonsoft.Json;
using System.Collections.Generic;

namespace DBDefsAnalyser.Models
{
    public class CommitDetails
    {
        public string Message;

        public List<File> Files = new List<File>();
    }

    public class Commit
    {
        public string Sha;

        [JsonProperty("node_id")]
        public string NodeId;

        [JsonProperty("commit")]
        public CommitDetails Details = new CommitDetails();

        public string Url;

        [JsonProperty("html_url")]
        public string HtmlUrl;
    }

    public class File
    {
        public string Sha;

        public string Filename;

        public string Status;

        public int Additions;

        public int Deletions;

        public int Changes;

        [JsonProperty("blob_url")]
        public string BlobUrl;

        [JsonProperty("raw_url")]
        public string RawUrl;

        [JsonProperty("contents_url")]
        public string ContentsUrl;

        public string Patch;
    }
}
