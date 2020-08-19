using DBDefsAnalyser.Models;
using DBDefsAnalyser.Utils;
using DBDefsLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static DBDefsLib.Structs;

namespace DBDefsAnalyser.Services
{
    public class CompareService
    {
        private readonly string Name;
        private readonly DBDefinition Definition;
        private readonly Versions Versions;

        private DataTable DataTable;

        public CompareService(string name, DBDefinition definiton, Versions versions)
        {
            Name = name;
            Definition = definiton;
            Versions = versions;
        }

        public bool LoadDataSets(VersionType type, WoWToolsService wt)
        {
            var version = Versions.Get(type);

            if (version == null || !wt.TryGetDataTable(Name, version, out var dt))
                return false;

            version.Data = dt;
            return UpdateDataTable(wt, version.Build);
        }

        public ComparisonSet Compare(VersionType type)
        {
            var version = Versions.Get(type);
            var keys = DataTable.IntersectKeys(version.Data);
            var matches = new sbyte[keys.Length]; // -1 = "0" match, 0 = not matched, 1 = non "0" match
            var uniqueMatches = new HashSet<string>(matches.Length);
            var results = new ComparisonSet(Name, Versions.Current, DataTable.Columns.Length);

            if (keys.Length == 0)
                return results;

            var columnDefs = Definition.columnDefinitions;

            for (var i = 0; i < DataTable.ColumnCount; i++)
            {
                var col = GetColumn(DataTable.Columns[i], VersionType.Current);

                // already mapped or processed
                if (col.name == null || columnDefs[col.name].comment?.Contains(Constants.CommentSuffix) == true)
                    continue;

                if (!col.name.Contains(DataTable.Build.build.ToString()) && !col.name.Contains(results.Build.build.ToString()))
                    continue;

                if (col.isID) // idfield shortcut
                {
                    results.AddField(DataTable.Columns[i], Versions.Get(type).IdField);
                }
                else // perform column comparison
                {
                    var numeric = !columnDefs[col.name].type.EndsWith("string");
                    var columns = GetApplicableColumns(col, version.Version);
                    var set = results.AddSet(DataTable.Columns[i], columns.Length);

                    foreach (var column in columns)
                    {
                        var index = version.Data.GetOrdinal(column);
                        if (index == -1)
                            continue; // local to remote def mismatch

                        for (var k = 0; k < keys.Length; k++)
                        {
                            var val = DataTable.GetValue(keys[k], i);
                            if (val == version.Data.GetValue(keys[k], index))
                            {
                                matches[k] = (sbyte)(val == "0" ? -1 : 1);
                                uniqueMatches.Add(val);
                            }
                            else
                            {
                                matches[k] = 0;
                            }
                        }

                        var comparison = new Comparison()
                        {
                            Column = DefinitionHelper.Normalise(column),
                            ComparableRecords = keys.Length,
                            UniqueMatches = uniqueMatches.Count
                        };

                        GetMatchStatistics(ref comparison, matches, numeric);

                        if (comparison.Percentage > 0)
                            set.Add(comparison);

                        uniqueMatches.Clear();
                    }
                }
            }

            results.Finalise();

            return results;
        }

        private string[] GetApplicableColumns(Definition column, VersionDefinitions version)
        {
            var result = new List<Definition>(version.definitions.Length);
            var type = Definition.columnDefinitions[column.name].type;

            foreach (var def in version.definitions)
            {
                // ignore id columns and check type
                if (def.isID || type != Definition.columnDefinitions[def.name].type)
                    continue;
                // only use fields with similar cardinalities
                if (Math.Abs(def.arrLength - column.arrLength) > Constants.CardinalityTolerance)
                    continue;
                // column is already used
                if (GetColumn(def.name, VersionType.Current).name != null)
                    continue;
                // relation field shortcut
                if(column.isRelation && def.isRelation)
                {
                    result.Clear();
                    result.Add(def);
                    break;
                }

                result.Add(def);
            }

            // convert fields to their CSV names
            return result.SelectMany(DefinitionHelper.GenerateColumnNames).ToArray();
        }

        /// <summary>
        /// Computes statistics on matches with different calculations for different field types
        /// </summary>
        /// <param name="comparison"></param>
        /// <param name="matches"></param>
        /// <param name="numeric"></param>
        private void GetMatchStatistics(ref Comparison comparison, sbyte[] matches, bool numeric)
        {
            // matches, non-zero-matches, non-zero-count
            int m, nzm, nzc;
            m = nzm = nzc = 0;

            for (var i = 0; i < matches.Length; i++)
            {
                if (matches[i] != 0) m++;
                if (matches[i] == 1) nzm++;
                if (matches[i] > -1) nzc++;
            }

            if (!numeric || (nzm == 0 && nzc > 0)) // total match %
            {
                comparison.Percentage = (float)Math.Round(m / (float)matches.Length, 2);
                comparison.Matches = m;
                comparison.RecordsCompared = matches.Length;
            }
            else if (nzc > 0) // non-zero match %
            {
                comparison.Percentage = (float)Math.Round(nzm / (float)nzc, 2);
                comparison.Matches = nzm;
                comparison.RecordsCompared = nzc;
            }
            else // 100% zero match - limited %
            {
                comparison.Percentage = Constants.MatchThreshold;
                comparison.Matches = 1;
                comparison.RecordsCompared = 1;
            }

            // NOTE: in some scenarios a column may be zero filled then changed later e.g. flags
            // which normally falls into (nzc > 0) however this would always return 0%
            // in this case we need to get the most total matches % (!numeric) however we need
            // to prevent comparing (nzc > 0) to (nzm == 0 && nzc > 0) as the latter is less accurate

            if (nzm == 0 && nzc > 0)
                comparison.ZeroCountFallback = true;
        }

        /// <summary>
        /// Returns a Definition from a CSV column name
        /// </summary>
        /// <param name="column"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private Definition GetColumn(string column, VersionType type)
        {
            var name = DefinitionHelper.Normalise(column); // remove ordinal
            return Versions.Get(type).Version.definitions.FirstOrDefault(x => x.name == name);
        }

        /// <summary>
        /// Updates the target build's datatable to be the closest version as possible
        /// to the comparison build
        /// </summary>
        /// <param name="wt"></param>
        /// <param name="build"></param>
        /// <returns></returns>
        private bool UpdateDataTable(WoWToolsService wt, Build build)
        {
            var builds = Versions.Current.Version.builds.ToList();
            builds.AddRange(Versions.Current.Version.buildRanges.Select(x => x.minBuild));
            builds.AddRange(Versions.Current.Version.buildRanges.Select(x => x.maxBuild));
            builds.Sort(new BuildSorter(build));

            if (DataTable?.Build == builds[0])
                return true;

            if (wt.TryGetDataTable(Name, Versions.Current, out var dt, builds[0]))
            {
                DataTable = dt;
                return true;
            }

            return false;
        }
    }
}
