using System;

namespace DBDefsAnalyser.Models
{
    public struct Comparison : IComparable<Comparison>
    {
        public string Column;
        public int ComparableRecords; // count of records with matching keys in the two sets
        public int RecordsCompared; // count of records actually compared (may exclude 0 values)
        public int Matches; // count of matches found (may exclude 0 values)
        public int UniqueMatches; // count of unique values matched
        public float Percentage; // % of compared records matched
        public bool ZeroCountFallback; // fallback non-zero count calculation

        /// <summary>
        /// Scalar to differentiate more likely matches from less likely based on stats
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public float GetScalar(Comparison other)
        {
            var scalar = UniqueMatches / Math.Max(other.UniqueMatches, 1f);
            scalar *= Matches / Math.Max(other.Matches, 1f);
            scalar *= RecordsCompared / Math.Max(other.RecordsCompared, 1f);
            return scalar == 0f ? 1f : scalar;
        }

        public int CompareTo(Comparison other)
        {
            if (ZeroCountFallback != other.ZeroCountFallback)
                return ZeroCountFallback.CompareTo(other.ZeroCountFallback); // force Fallback to end

            var scalar = GetScalar(other);

            int c;
            if ((c = (Percentage * scalar).CompareTo(other.Percentage / scalar)) != 0)
                return -c;
            if ((c = UniqueMatches.CompareTo(other.UniqueMatches)) != 0)
                return -c;
            if ((c = Matches.CompareTo(other.Matches)) != 0)
                return -c;
            if ((c = RecordsCompared.CompareTo(other.RecordsCompared)) != 0)
                return -c;
            if ((c = ComparableRecords.CompareTo(other.ComparableRecords)) != 0)
                return -c;

            return 0;
        }

        public override string ToString() => $"[{Column}, {Percentage:G2}%]";
    }
}
