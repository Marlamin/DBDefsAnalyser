using CommandLine;
using DBDefsAnalyser.Models;
using DBDefsAnalyser.Providers;
using DBDefsAnalyser.Services;
using DBDefsAnalyser.Utils;
using DBDefsLib;
using DBDefsLib.Constants;
using DBDefsLib.Structs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

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

            var urlFormat = "";
            if (options.Source == "wtlcsv")
                urlFormat = Constants.WTLCSVURL;
            else if (options.Source == "wagocsv")
                urlFormat = Constants.WagoCSVURL;
            else
                throw new ArgumentException($"Invalid source specified: {options.Source}");

            var domain = options.Domain;
            if (options.Source == "wagocsv" && options.Domain == "localhost:5080") // if wago is the source and wtl is the default domain still, update it
                domain = "wago.tools";

            using var csv = new CSVService(domain, urlFormat);
            using var git = new GitService();

            if (options.Mode == "updateDefs")
                UpdateDefinitions(options, git, csv);
            else if (options.Mode == "updateMeta")
                UpdateMeta(options, git, csv);
            else
                throw new ArgumentException($"Invalid mode specified: {options.Mode}");

        }
        public struct ManifestEntry
        {
            public string tableName { get; set; }
            public string tableHash { get; set; }

            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
            public int dbcFileDataID { get; set; }

            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
            public int db2FileDataID { get; set; }
        }

        private static void UpdateDefinitions(Options options, GitService git, CSVService csv)
        {
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
                    var versions = DefinitionHelper.GetVersionDefinitions(definition, build, options.Targets);
                    var comparer = new CompareService(filename, definition, versions);
                    var set = new ComparisonSet(filename, versions.Current);

                    if (comparer.LoadDataSets(VersionType.Newer, csv))
                        set.Merge(comparer.Compare(VersionType.Newer));

                    if (comparer.LoadDataSets(VersionType.Older, csv))
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

                    dbWriter.Save(definition, Path.Combine(options.DefinitionPath, $"{filename}.dbd"), true);
                }
            }

            Logger.Save(true);
            CacheService.RemoveCommit(commit.Sha);
        }

        private static EnumDefinition? ReadEnumFile(MappingDefinition mapping, string metaDirectory)
        {
            string path = ((mapping.meta == MetaType.ENUM) ? "enums" : "flags");
            string text = ((mapping.meta == MetaType.ENUM) ? ".dbde" : ".dbdf");
            string text2 = Path.Combine(metaDirectory, path, mapping.metaValue + text);

            if (!System.IO.File.Exists(text2))
                return null;

            return new DBDEnumReader().Read(text2, mapping.meta);
        }

        private static void UpdateMeta(Options options, GitService git, CSVService csv)
        {
            var dbReader = new DBDReader();
            var dbWriter = new DBDWriter();

            var enumProvider = new EnumProvider(options.DefinitionPath);

            var enumCache = new Dictionary<string, EnumDefinition>();
            var enumCacheLock = new Lock();

            var filesWithMeta = enumProvider.Mappings.Where(x => x.meta != MetaType.COLOR).Select(x => x.tableName).Distinct().ToList();

            var missingValues = new Dictionary<string, List<long>>();
            var missingLock = new Lock();

            // Columns to skip because they have special values
            var skippedColumns = new HashSet<string>() { "ChrCustomizationReq::ClassMask", "ItemSearchName::AllowableClass", "ItemSparse::AllowableClass", "ItemSearchName::ExpansionID", "ItemSparse::ExpansionID", "ContentTuning::ExpansionID", "AreaPOI::Flags" };

            Parallel.ForEach(filesWithMeta, file =>
            {
                var definition = dbReader.Read(git.GetDefinition(file));
                var versions = DefinitionHelper.GetVersionDefinitions(definition, new Build(options.Build), options.Targets);
                if (versions.Current == null)
                    return;

                var versionDefinition = definition.versionDefinitions.Where(x => x.builds.Contains(new Build(options.Build))).First();

                if (csv.TryGetDataTable(file, versions.Current, out var dataTable))
                {
                    var metaForThisFile = enumProvider.Mappings.Where(x => x.tableName == file && x.meta != MetaType.COLOR).ToList();
                    foreach (var meta in metaForThisFile)
                    {
                        var enumValues = new List<long>();

                        lock (enumCacheLock)
                        {
                            if (!enumCache.TryGetValue(meta.metaValue, out EnumDefinition enumDef))
                                enumDef = ReadEnumFile(meta, options.DefinitionPath + "/../meta").Value;

                            enumCache[meta.metaValue] = enumDef;
                            enumValues = [.. enumDef.entries.Select(x => x.value)];
                        }

                        if (enumValues.Count == 0)
                            continue;

                        foreach (var row in dataTable.Keys)
                        {
                            foreach (var column in dataTable.Columns)
                            {
                                var columnName = column;
                                var arrIndex = -1;
                                if (column.Contains('[') && column.Contains(']'))
                                {
                                    var start = column.IndexOf('[');
                                    var end = column.IndexOf(']');
                                    if (start < end)
                                    {
                                        var indexStr = column.Substring(start + 1, end - start - 1);
                                        if (int.TryParse(indexStr, out var index))
                                        {
                                            arrIndex = index;
                                            columnName = column.Substring(0, start);
                                        }
                                    }
                                }

                                if (columnName != meta.columnName)
                                    continue;

                                if (arrIndex != -1 && meta.arrIndex != null && arrIndex != meta.arrIndex)
                                    continue;

                                var fullname = file + "::" + column;
                                if (skippedColumns.Contains(fullname))
                                    continue;

                                var ordinal = -1;
                                if (arrIndex == -1)
                                    ordinal = dataTable.GetOrdinal(columnName);
                                else
                                    ordinal = dataTable.GetOrdinal(columnName + "[" + arrIndex + "]");

                                if (meta.conditionalColumn != null && meta.conditionalValue != null)
                                {
                                    var conditionalColumnIndex = dataTable.GetOrdinal(meta.conditionalColumn);
                                    var conditionalValue = dataTable.GetValue(row, conditionalColumnIndex)?.ToString();
                                    if (conditionalValue != meta.conditionalValue)
                                        continue;
                                }

                                var columnValue = long.Parse(dataTable.GetValue(row, ordinal));
                                if (columnValue == 0 || columnValue == -1 || columnValue == byte.MaxValue || columnValue == uint.MaxValue || columnValue == long.MaxValue) // this will miss some for obvious reasons but wahtever
                                    continue;

                                if (meta.meta == MetaType.FLAGS)
                                {
                                    var bits = versionDefinition.definitions.Where(x => x.name == meta.columnName).First().size;

                                    for (var i = 0; i < bits; i++)
                                    {
                                        var flagValue = 1L << i;
                                        if ((columnValue & flagValue) != 0 && !enumValues.Contains(flagValue))
                                        {
                                            lock (missingLock)
                                            {
                                                var key = meta.metaValue;

                                                if (!missingValues.ContainsKey(key))
                                                    missingValues[key] = new List<long>();

                                                if (!missingValues[key].Contains(flagValue))
                                                {
                                                    missingValues[key].Add(flagValue);
                                                    Logger.WriteLine("Missing flag value 0x" + flagValue.ToString("X") + " in " + file + " for column " + meta.columnName);
                                                }
                                            }
                                        }
                                    }
                                }
                                else if (meta.meta == MetaType.ENUM)
                                {
                                    if (columnValue == 0 || columnValue == -1 || columnValue == 255) // this will miss some
                                        continue;

                                    if (!enumValues.Contains(columnValue))
                                    {
                                        lock (missingLock)
                                        {
                                            var key = meta.metaValue;

                                            if (!missingValues.ContainsKey(key))
                                                missingValues[key] = new List<long>();

                                            if (!missingValues[key].Contains(columnValue))
                                            {
                                                missingValues[key].Add(columnValue);
                                                Logger.WriteLine("Missing enum value " + columnValue + " in " + file + " for column " + meta.columnName + " (enum: " + meta.metaValue + ")");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            });

            var enumWriter = new DBDEnumWriter();
            foreach (var missingValue in missingValues)
            {
                var enumFile = missingValue.Key;
                var missingEnumValues = missingValue.Value;

                if (enumCache.TryGetValue(enumFile, out var enumDef))
                {
                    foreach (var value in missingEnumValues)
                    {
                        if (enumFile == "PrimaryStats" && value > 100)
                            continue;

                        enumDef.entries.Add(new EnumEntry() { value = value, name = "" });
                    }

                    var enumPath = "";
                    if (enumDef.metaType == MetaType.FLAGS)
                        enumPath = Path.Combine(options.DefinitionPath, "..", "meta", "flags", enumFile + ".dbdf");
                    else if (enumDef.metaType == MetaType.ENUM)
                        enumPath = Path.Combine(options.DefinitionPath, "..", "meta", "enums", enumFile + ".dbde");
                    else
                        continue;

                    enumDef.entries = enumDef.entries.OrderBy(x => x.value).ToList();

                    enumWriter.Save(enumDef, enumPath);
                }
            }

            Logger.Save(true);
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
