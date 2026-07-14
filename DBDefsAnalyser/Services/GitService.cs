using DBDefsAnalyser.Models;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace DBDefsAnalyser.Services
{
    public class GitService : IDisposable
    {
        private readonly HttpClient Client;

        public GitService()
        {
            Client = new();
        }

        public Commit GetLatestCommit()
        {
            var latest = Get<Commit[]>(Constants.CommitAPI + Constants.CommitFilter)[0];

            // if valid commit, load changed filters
            if (latest.Details.Message.StartsWith(Constants.CommitNeedle))
                return LoadDetails(latest);
            else
                return Constants.EmptyCommit;
        }

        public Commit GetCommit(string sha)
        {
            if (!string.IsNullOrWhiteSpace(sha))
            {
                var commit = Get<Commit>(Constants.CommitAPI + "/" + sha);
                if (commit != null)
                    return LoadDetails(commit);
            }

            return Constants.EmptyCommit;
        }

        public Stream GetDefinition(string filename)
        {
            if (!CacheService.TryGetDefinition(filename, out var filepath))
            {
                Client.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgent);
                return Client.GetStreamAsync(string.Format(Constants.RawDefinitonUrl, filename)).Result;
            }
            else
            {
                return System.IO.File.OpenRead(filepath);
            }
        }

        private Commit LoadDetails(Commit commit)
        {
            if (CacheService.TryGetCommit(commit.Sha, out var model))
                return model;

            CommitDetails temp;

            for (var i = 1; ; i++)
            {
                temp = Get<CommitDetails>(commit.Url + "?page=" + i);
                commit.Details.Files.AddRange(temp.Files);

                if (temp.Files.Count == 0)
                    break;
            }

            commit.Details.Files.RemoveAll(x => x.Changes <= 2); // remove merged structures
            CacheService.StoreCommit(commit);
            return commit;
        }

        private T Get<T>(string url) where T : class
        {
            if (!Directory.Exists("gitcache"))
                Directory.CreateDirectory("gitcache");

            var urlMD5 = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(url)));

            Client.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgent);
            if (!string.IsNullOrEmpty(Constants.Token))
                Client.DefaultRequestHeaders.Add("Authorization", "Bearer: " + Constants.Token);

            if (System.IO.File.Exists(Path.Combine("gitcache", urlMD5)))
                return JsonConvert.DeserializeObject<T>(System.IO.File.ReadAllText(Path.Combine("gitcache", urlMD5)));

            Console.WriteLine("Downloading: " + url);

            var result = Client.GetStringAsync(url).Result;
            System.IO.File.WriteAllText(Path.Combine("gitcache", urlMD5), result);
            return JsonConvert.DeserializeObject<T>(result);
        }

        public void Dispose() => Client.Dispose();
    }
}
