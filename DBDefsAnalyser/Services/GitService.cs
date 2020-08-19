using DBDefsAnalyser.Models;
using DBDefsAnalyser.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace DBDefsAnalyser.Services
{
    public class GitService : IDisposable
    {
        private readonly WebClientEx Client;

        public GitService()
        {
            Client = new WebClientEx();
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
            if(!string.IsNullOrWhiteSpace(sha))
            {
                var commit = Get<Commit>(Constants.CommitAPI + "/" + sha);
                if (commit != null)
                    return LoadDetails(commit);
            }          

            return Constants.EmptyCommit;
        }

        public Stream GetDefinition(string filename)
        {
            if(!CacheService.TryGetDefinition(filename, out var filepath))
            {
                Client.Headers.Add(HttpRequestHeader.UserAgent, Constants.UserAgent);
                return Client.OpenRead(string.Format(Constants.RawDefinitonUrl, filename));
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
            Client.Headers.Add(HttpRequestHeader.UserAgent, Constants.UserAgent);
            return JsonConvert.DeserializeObject<T>(Client.DownloadString(url));
        }

        public void Dispose() => Client.Dispose();
    }
}
