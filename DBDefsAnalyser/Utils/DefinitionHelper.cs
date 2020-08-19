using DBDefsAnalyser.Models;
using DBDefsLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using static DBDefsLib.Structs;

namespace DBDefsAnalyser.Utils
{
    public static class DefinitionHelper
    {
        private static readonly Regex BuildRegex = new Regex(Constants.NewFieldRegex, RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// Extracts all builds from the patch that are still unnamed in the DBDefinition
        /// </summary>
        /// <param name="definition"></param>
        /// <param name="patch"></param>
        /// <returns></returns>
        public static IReadOnlyCollection<Build> ExtractBuilds(DBDefinition definition, string patch)
        {
            var results = new HashSet<Build>(0x10);
            var columns = definition.columnDefinitions;
            var matches = BuildRegex.Matches(patch);

            foreach (Match match in matches)
            {
                var buildstring = match.Groups[1].Value;
                var column = columns.FirstOrDefault(x => x.Key.Contains(buildstring));

                // double check no one has mapped this already
                if (column.Key != null && !(column.Value.comment?.Contains(Constants.CommentSuffix) == true))
                    results.Add(new Build(buildstring.Replace("_", ".")));
            }

            return results;
        }

        /// <summary>
        /// Returns the target build VersionDefinitions 
        /// as well as the closest older and newer named builds
        /// </summary>
        /// <param name="definition"></param>
        /// <param name="build"></param>
        /// <returns></returns>
        public static Versions GetVersionDefinitions(DBDefinition definition, Build build)
        {
            var min = new Build("0.0.0.0");
            var max = new Build("99.99.99.999999");
            var sorter = new BuildSorter(build);
            var result = new Versions();

            Build Closest(VersionDefinitions definition)
            {
                var builds = definition.builds.ToList();
                builds.AddRange(definition.buildRanges.Select(x => x.minBuild));
                builds.AddRange(definition.buildRanges.Select(x => x.maxBuild));
                builds.Sort(sorter);
                return builds[0];
            }

            for(var i = 0; i < definition.versionDefinitions.Length; i++)
            {
                var versionDef = definition.versionDefinitions[i];

                // matching target build
                if (versionDef.builds.Contains(build) || versionDef.buildRanges.Any(x => x.Contains(build)))
                {
                    result.Current = new BuildVersionPair(build, versionDef);
                    continue;
                }

                // ignore unmapped versions
                if (versionDef.definitions.All(x => x.isID || x.name.StartsWith("Field_")))
                    continue;

                var closest = Closest(versionDef);

                if (closest < build && closest > min)
                {
                    min = closest;
                    result.Older = new BuildVersionPair(closest, versionDef);
                }                
                else if (closest > build && closest < max)
                {
                    max = closest;
                    result.Newer = new BuildVersionPair(closest, versionDef);                   
                }
            }

            return result;
        }

        /// <summary>
        /// Returns ordinal suffixed column names for array definitions
        /// </summary>
        /// <param name="definition"></param>
        /// <returns></returns>
        public static string[] GenerateColumnNames(Definition definition)
        {
            if (definition.arrLength <= 1)
                return new[] { definition.name };

            var names = new string[definition.arrLength];
            for (var i = 0; i < definition.arrLength; i++)
                names[i] = definition.name + $"[{i}]";

            return names;
        }

        /// <summary>
        /// Normalises a csv name which is suffixed with the ordinal
        /// </summary>
        /// <param name="column"></param>
        /// <returns></returns>
        public static string Normalise(string column) => column.Split('[', 2)[0];

        public static bool TryParse(string value, out Build result)
        {
            result = null;

            var split = value.Split('.');
            if (split.Length != 4)
                return false;

            if (!short.TryParse(split[0], out var _))
                return false;
            if (!short.TryParse(split[1], out var _))
                return false;
            if (!short.TryParse(split[2], out var _))
                return false;
            if (!uint.TryParse(split[3], out var _))
                return false;

            result = new Build(value);
            return true;
        }
    }
}
