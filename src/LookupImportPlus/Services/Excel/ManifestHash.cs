using LookupImportPlus.Domain;

namespace LookupImportPlus.Services.Excel
{
    /// <summary>
    /// Small, dependency-free string hash (djb2) to detect tampered/edited
    /// templates. Not cryptographic — an integrity signal for the manifest.
    /// Port of src/services/excel/manifestHash.ts.
    /// </summary>
    public static class ManifestHash
    {
        public static string Djb2(string input)
        {
            uint hash = 5381;
            foreach (var ch in input)
            {
                hash = ((hash << 5) + hash + ch); // hash * 33 + ch, wraps at uint (>>> 0)
            }
            return hash.ToString("x8");
        }

        /// <summary>Hash of everything in the manifest except Hash / GeneratedOn.</summary>
        public static string HashManifest(TemplateManifest m)
        {
            var core = new
            {
                configId = m.ConfigId,
                configVersion = m.ConfigVersion,
                schemaVersion = m.SchemaVersion,
                targetEntity = m.TargetEntity,
                entitySetName = m.EntitySetName,
                operation = m.Operation,
                columns = m.Columns
            };
            return Djb2(Json.Serialize(core));
        }
    }
}
