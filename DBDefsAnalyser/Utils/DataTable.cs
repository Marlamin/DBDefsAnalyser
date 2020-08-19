using DBDefsLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DBDefsAnalyser.Utils
{
    public class DataTable
    {
        public Build Build { get; }
        public string[] Columns { get; }
        public int[] Keys => Data.Keys.ToArray();
        public int ColumnCount => Columns.Length;
        public int RowCount => Data.Count;

        private readonly int IdIndex;
        private readonly Dictionary<int, string[]> Data;

        public DataTable(Build build, string[] columns, string key)
        {
            Build = build;
            Columns = columns;
            IdIndex = Array.IndexOf(columns, key);
            Data = new Dictionary<int, string[]>(0x1000);
        }

        public void AddRow(string[] fields) => Data.Add(int.Parse(fields[IdIndex]), fields);

        public string GetValue(int key, int index) => Data[key][index];

        public int GetOrdinal(string column) => Array.IndexOf(Columns, column);

        public int[] IntersectKeys(DataTable dataTable)
        {
            var keys = Keys.ToHashSet();
            keys.IntersectWith(dataTable.Keys);
            return keys.Take(Constants.ComparisonLimit).ToArray();
        }
    }
}
