using CommandLine;
using System.Collections.Generic;

namespace DBDefsAnalyser.Models
{
    internal class Options
    {
        [Option("path", HelpText = "Definitons folder location", Default = "")]
        public string DefinitionPath { get; set; }

        [Option("commit", HelpText = "Sha of commit with unnamed fields")]
        public string Commit { get; set; }

        [Option("build", HelpText = "Parse all files for a specific build")]
        public string Build { get; set; }

        [Option("source", HelpText = "Source for table data. Options: wtlcsv, wagocsv", Default = "wtlcsv")]
        public string Source { get; set; }

        [Option("domain", HelpText = "Domain for source", Default = "localhost:5080")]
        public string Domain { get; set; }

        [Option("targets", HelpText = "Specific builds to compare to", Separator = ',', Required = false)]
        public IEnumerable<string> Targets { get; set; }
    }
}
