using DBDefsAnalyser.Models;
using DBDefsAnalyser.Utils;
using DBDefsLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using static DBDefsLib.Structs;

namespace DBDefsAnalyser
{
    public class ComparisonSet
    {
        public string Definition { get; }
        public Build Build { get; }
        public Dictionary<string, List<Comparison>> Results { get; }

        public int Count => Results.Count(x => x.Value.Count > 0);

        public ComparisonSet(string definition, BuildVersionPair buildVersion, int capacity = 0x10)
        {
            Definition = definition;
            Build = buildVersion.Build;
            Results = new Dictionary<string, List<Comparison>>(capacity);
        }

        /// <summary>
        /// Adds or gets a comparison set for the specific column
        /// </summary>
        /// <param name="column"></param>
        /// <param name="capacity"></param>
        /// <returns></returns>
        public List<Comparison> AddSet(string column, int capacity = 0x10)
        {
            column = DefinitionHelper.Normalise(column); // use the same bucket for all array fields

            if (!Results.TryGetValue(column, out var set))
                set = Results[column] = new List<Comparison>(capacity);

            return set;
        }

        /// <summary>
        /// Adds a confirmed comparison directly to the set
        /// </summary>
        /// <param name="column"></param>
        /// <param name="field"></param>
        public void AddField(string column, string field)
        {
            column = DefinitionHelper.Normalise(column);

            if (!Results.ContainsKey(column))
            {
                Results.Add(column, new List<Comparison>
                {
                    new Comparison()
                    {
                        Column = DefinitionHelper.Normalise(field),
                        Percentage = 1f
                    }
                });
            }
        }

        /// <summary>
        /// Merges two sets and finalises them
        /// </summary>
        /// <param name="other"></param>
        public void Merge(ComparisonSet other)
        {
            foreach (var result in other.Results)
            {
                if (!Results.TryGetValue(result.Key, out var set))
                    set = Results[result.Key] = new List<Comparison>();

                set.AddRange(result.Value);
            }

            Finalise();
        }

        /// <summary>
        /// Performs post comparison validation removing unneeded results
        /// </summary>
        public void Finalise()
        {
            foreach (var result in Results)
                Validate(result.Value, true);
        }

        public bool UpdateDefinition(DBDefinition definition)
        {
            var duplicates = GetDuplicates();
            var updated = false;

            foreach (var result in Results)
            {
                if (result.Value.Count == 0)
                    continue;

                var column = definition.columnDefinitions[result.Key];
                var prefix = $" {Constants.CommentSuffix} ";
                var match = result.Value[0];

                if (result.Value.Count > 1 || duplicates.Contains(match.Column) || match.Percentage < Constants.MinThreshold)
                {
                    column.comment += prefix + string.Join(", ", result.Value);
                    column.comment.Trim();
                    definition.columnDefinitions[result.Key] = column;
                }
                else
                {
                    var comment = "";
                    if (match.Percentage < Constants.MatchThreshold)
                        comment = prefix + match;

                    definition.columnDefinitions.Remove(result.Key);

                    for (var i = 0; i < definition.versionDefinitions.Length; i++)
                    {
                        for (var j = 0; j < definition.versionDefinitions[i].definitions.Length; j++)
                        {
                            if (definition.versionDefinitions[i].definitions[j].name == result.Key)
                            {
                                definition.versionDefinitions[i].definitions[j].name = match.Column;
                                definition.versionDefinitions[i].definitions[j].comment += comment;
                                definition.versionDefinitions[i].definitions[j].comment.Trim();
                            }
                        }
                    }
                }

                updated = true;
            }

            return updated;
        }

        private ICollection<string> GetDuplicates()
        {
            var results = new List<string>(Results.Count);

            var duplicates = Results.SelectMany(x => x.Value)
                .GroupBy(x => x.Column)
                .Where(x => x.Count() > 1)
                .ToDictionary(x => x.Key, x => x.ToList());

            foreach (var dupe in duplicates)
            {
                // re-validate duplicates as one may be a true mapping
                if (Validate(dupe.Value, false))
                {
                    foreach (var result in Results)
                    {
                        result.Value.RemoveAll(x => x.Column == dupe.Key && !dupe.Value.Contains(x));
                    }
                }

                if (dupe.Value.Count > 1)
                    results.Add(dupe.Key);
            }

            return results;
        }

        private bool Validate(List<Comparison> set, bool deduplicate)
        {
            if (set.Count <= 1)
                return true;

            var count = set.Count;

            if (!set.TrueForAll(x => x.Percentage < Constants.MinThreshold))
                set.RemoveAll(x => x.Percentage < Constants.MinThreshold); // clear below min threshold

            set.Sort(); // sort by highest scaled %

            // percentage threshold required to be keep in the set
            var threshold = true switch
            {
                true when set[0].Percentage >= 0.99f => 0.99f, // 100% match with tolerance
                true when set[0].Percentage >= Constants.MatchThreshold => Constants.MatchThreshold, // our match limit
                _ => set[0].Percentage - Constants.DisparityTolerance // minimum value in tolerable range
            };

            var knownkeys = new HashSet<string>(set.Count);
            for (var i = 0; i < set.Count; i++)
            {
                // remove < threshold or fallback calculated comparisons
                if (set[i].Percentage < threshold || set[i].ZeroCountFallback != set[0].ZeroCountFallback)
                    set.RemoveAt(i--);
                // remove duplicate names if applicable
                else if (deduplicate && !knownkeys.Add(set[i].Column))
                    set.RemoveAt(i--);
                // remove < scaled percentage
                else if (i > 0 && set[0].GetScalar(set[i]) - 1f >= Constants.DisparityTolerance)
                    set.RemoveAt(i--);
            }

            return set.Count < count;
        }

        public void Print()
        {
            Logger.WriteLine();
            Logger.WriteLine(Definition + ":");

            foreach (var result in Results)
            {
                Logger.Write(" " + result.Key + " : ");
                Logger.WriteLine(string.Join(", ", result.Value.Select(x => x)));
            }
        }
    }
}
