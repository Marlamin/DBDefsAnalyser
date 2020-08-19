using CommandLine;
using System;
using System.Collections.Generic;
using System.Text;

namespace DBDefsAnalyser.Models
{
    internal class Options
    {
        [Option("path", HelpText = "Definitons folder location", Default = "")]
        public string DefinitionPath { get; set; }

        [Option("commit", HelpText = "Sha of commit with unnamed fields")]
        public string Commit { get; set; }

        [Option("build", HelpText = "Parse all files for a specific")]
        public string Build { get; set; }
    }
}
