using DBDefsAnalyser.Models;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace DBDefsAnalyser
{
    public static class Constants
    {
        public const string UserAgent = "User-Agent: DBDefsAnalyser";
        public const string CommitAPI = "https://api.github.com/repos/wowdev/WoWDBDefs/commits";
        public const string CommitFilter = "?author=marlamin";
        public const string CommitNeedle = "Merge defs for ";
        public const string RawDefinitonUrl = "https://github.com/wowdev/WoWDBDefs/raw/master/definitions/{0}.dbd";
        public const string CSVUrl = @"https://wow.tools/dbc/api/export/?name={0}&build={1}";
        public const string NewFieldRegex = @"^\+\w+\sField_(\d{1,2}_\d{1,2}_\d{1,2}_\d+)";
        public const string CommentSuffix = "DBAnalyser:";
        public static readonly Commit EmptyCommit = new Commit();

        /// <summary>
        /// Records to be compared limit
        /// </summary>
        public const int ComparisonLimit = 100000;
        /// <summary>
        /// Max allowed +/- difference in field cardinalities
        /// </summary>
        public const int CardinalityTolerance = 2;
        /// <summary>
        /// Max allowed percentage decrease from top result
        /// </summary>
        public const float DisparityTolerance = 0.2f;
        /// <summary>
        /// Percentage threshold for what quanitifes a true match
        /// </summary>
        public const float MatchThreshold = 0.95f;
        /// <summary>
        /// Minium threshold for a match
        /// </summary>
        public const float MinThreshold = 0.85f;
    }
}
