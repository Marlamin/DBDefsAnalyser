using DBDefsLib;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DBDefsAnalyser.Utils
{
    public class BuildSorter : IComparer<Build>
    {
        public Build Target { get; }

        public BuildSorter(Build target)
        {
            Target = target;
        }

        public int Compare(Build x, Build y)
        {
            int c;
            if ((c = ClosestCompare(x.expansion, y.expansion, Target.expansion)) != 0)
                return c;
            if ((c = ClosestCompare(x.major, y.major, Target.major)) != 0)
                return c;
            if ((c = ClosestCompare(x.minor, y.minor, Target.minor)) != 0)
                return c;

            return ClosestCompare(x.build, y.build, Target.build);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ClosestCompare(long x, long y, long target)
        {
            return Math.Abs(x - target).CompareTo(Math.Abs(y - target));
        }
    }
}
