using DBCD.Providers;
using DBDefsLib.Structs;
using System.Collections.Generic;
using System.IO;

namespace DBDefsAnalyser.Providers
{
    public class EnumProvider : IEnumProvider
    {
        private FilesystemEnumProvider? filesystemEnumProvider = null;
        public List<MappingDefinition> Mappings { get; private set; }
        public Dictionary<string, EnumDefinition> EnumDefinitions { get; private set; }

        public EnumProvider(string definitionDir)
        {
            var mappingPath = Path.Combine(definitionDir, "..", "meta", "mapping.dbdm");
            filesystemEnumProvider = new FilesystemEnumProvider(mappingPath);
            Mappings = filesystemEnumProvider.Mappings;
        }

        public EnumDefinition? GetEnumDefinition(string tableName, string columnName, int? arrayIndex = null, string? conditionalTable = null, string? conditionalColumn = null, string? conditionalValue = null)
        {
            return filesystemEnumProvider?.GetEnumDefinition(tableName, columnName, arrayIndex, conditionalTable, conditionalColumn, conditionalValue);
        }
    }
}
