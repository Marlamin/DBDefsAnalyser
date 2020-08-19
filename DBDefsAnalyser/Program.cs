using CommandLine;
using DBDefsAnalyser.Models;
using DBDefsAnalyser.Services;
using DBDefsAnalyser.Utils;
using DBDefsLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime;

namespace DBDefsAnalyser
{
    class Program
    {
        static void Main(string[] args)
        {
            using var parser = new Parser(s =>
            {
                s.HelpWriter = Console.Error;
                s.AutoVersion = false;
            });

            var options = parser.ParseArguments<Options>(args);
            if (options.Tag == ParserResultType.Parsed)
                options.WithParsed(Run);
        }

        private static void Run(Options options)
        {
            CacheService.Initalise(options.DefinitionPath);

            using var wt = new WoWToolsService();
            using var git = new GitService();
            var dbReader = new DBDReader();
            var dbWriter = new DBDWriter();

            var commit = true switch
            {
                true when !string.IsNullOrEmpty(options.Build) => GetBuildCommit(options.Build),
                true when !string.IsNullOrEmpty(options.Commit) => git.GetCommit(options.Commit),
                _ => git.GetLatestCommit(),
            };

            foreach (var file in commit.Details.Files)
            {
                var filename = Path.GetFileNameWithoutExtension(file.Filename);
                var definition = dbReader.Read(git.GetDefinition(filename)); // TODO make dbreader less awful
                var builds = DefinitionHelper.ExtractBuilds(definition, file.Patch);
                var comparisons = new List<ComparisonSet>(builds.Count);

                foreach (var build in builds)
                {
                    var versions = DefinitionHelper.GetVersionDefinitions(definition, build);
                    var comparer = new CompareService(filename, definition, versions);
                    var set = new ComparisonSet(filename, versions.Current);

                    if (comparer.LoadDataSets(VersionType.Newer, wt))
                        set.Merge(comparer.Compare(VersionType.Newer));

                    if (comparer.LoadDataSets(VersionType.Older, wt))
                        set.Merge(comparer.Compare(VersionType.Older));

                    if (set.Count == 0)
                        continue;

                    comparisons.Add(set);
                }

                if (comparisons.Count > 0)
                {
                    foreach (var set in comparisons)
                    {
                        set.UpdateDefinition(definition);
                        set.Print();
                    }

                    dbWriter.Save(definition, Path.Combine(options.DefinitionPath, $"{filename}.dbd"));
                }
            }

            Logger.Save(true);
            CacheService.RemoveCommit(commit.Sha);
        }

        /// <summary>
        /// Creates a fake commit for a specific build
        /// </summary>
        /// <param name="buildstring"></param>
        /// <returns></returns>
        private static Commit GetBuildCommit(string buildstring)
        {
            if (DefinitionHelper.TryParse(buildstring, out var _))
            {
                var patch = $"+int Field_{buildstring}".Replace(".", "_");
                var commit = new Commit()
                {
                    Sha = buildstring
                };

                // append all files with a fake patch that matches the build regex
                foreach (var fn in CacheService.GetAllDefinitions())
                {
                    commit.Details.Files.Add(new Models.File()
                    {
                        Filename = fn,
                        Patch = patch
                    });
                }

                return commit;
            }

            return Constants.EmptyCommit;
        }
    }
}
