using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DBDefsAnalyser.Services
{
    public static class CacheService
    {
        private const string TempPath = "Temp";

        private static Dictionary<string, string> DefinitionLookup;
        private static Dictionary<string, Models.Commit> CommitLookup;
        private static bool IsValid => Directory.Exists(TempPath);

        public static void Initalise(string definitionPath)
        {
            DefinitionLookup = CreateDefinitionLookup(definitionPath);
            CommitLookup = CreateCommitLookup();
        }

        public static bool TryGetDefinition(string filename, out string filepath)
        {
            return DefinitionLookup.TryGetValue(filename, out filepath);
        }

        public static bool TryGetCommit(string sha, out Models.Commit commit)
        {
            return CommitLookup.TryGetValue(sha, out commit);
        }


        public static void StoreCommit(Models.Commit commit)
        {
            if (!IsValid)
                Directory.CreateDirectory(TempPath);

            using var file = File.CreateText(Path.Combine(TempPath, commit.Sha));
            file.Write(JsonConvert.SerializeObject(commit));
            file.Flush();

            CommitLookup.Add(commit.Sha, commit);
        }

        public static void RemoveCommit(string sha)
        {
            if (IsValid)
                File.Delete(Path.Combine(TempPath, sha));

            if (CommitLookup.Remove(sha) && CommitLookup.Count == 0)
                Clear();
        }

        public static void Clear()
        {
            if (IsValid)
                Directory.Delete(TempPath);
        }

        public static IEnumerable<string> GetAllDefinitions() => DefinitionLookup.Keys;

        private static Dictionary<string, string> CreateDefinitionLookup(string definitionPath)
        {
            if (definitionPath == null || !Directory.Exists(definitionPath))
                return new Dictionary<string, string>();

            var files = Directory.EnumerateFiles(definitionPath, "*.dbd", SearchOption.AllDirectories);
            return files.ToDictionary(x => Path.GetFileNameWithoutExtension(x), x => x, StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, Models.Commit> CreateCommitLookup()
        {
            var lookup = new Dictionary<string, Models.Commit>(0x10);

            if(IsValid)
            {
                foreach (var file in Directory.EnumerateFiles(TempPath))
                {
                    var commit = JsonConvert.DeserializeObject<Models.Commit>(File.ReadAllText(file));

                    if (commit != null && !string.IsNullOrEmpty(commit.Sha))
                        lookup[commit.Sha] = commit;
                }
            }

            return lookup;
        }
    }
}
