using DBDefsAnalyser.Utils;
using DBDefsLib;
using DBDefsLib.Structs;
using System;
using System.Linq;

namespace DBDefsAnalyser.Models
{
    public class Versions
    {
        public BuildVersionPair Older { get; set; }
        public BuildVersionPair Current { get; set; }
        public BuildVersionPair Newer { get; set; }

        public BuildVersionPair Get(VersionType type)
        {
            return type switch
            {
                VersionType.Current => Current,
                VersionType.Newer => Newer,
                VersionType.Older => Older,
                _ => throw new ArgumentException()
            };
        }
    }

    public class BuildVersionPair
    {
        public Build Build { get; }
        public VersionDefinitions Version { get; }
        public DataTable Data { get; set; }

        public string IdField => Version.definitions.First(x => x.isID).name;

        public BuildVersionPair(Build build, VersionDefinitions version)
        {
            Build = build;
            Version = version;
        }
    }

    public enum VersionType
    {
        Older,
        Current,
        Newer
    }
}
