using System.Collections.Generic;
using System.Linq;

namespace LookupImportPlus.Domain
{
    /// <summary>
    /// Result of checking a saved configuration against the current Dataverse
    /// metadata (schema drift protection, issues.ts). Issues carry a machine code
    /// (+ params) so the UI can render a localized message; they never throw.
    /// </summary>
    public sealed class ConfigIssue
    {
        public IssueSeverity Severity { get; set; }
        public ConfigIssueCode Code { get; set; }

        /// <summary>The config element the issue is about (attribute / lookup / entity).</summary>
        public string Target { get; set; }

        /// <summary>Substitution values for the localized message.</summary>
        public Dictionary<string, string> Params { get; set; }
    }

    public sealed class ConfigValidationResult
    {
        public List<ConfigIssue> Issues { get; set; } = new List<ConfigIssue>();
        public bool HasErrors => Issues.Any(i => i.Severity == IssueSeverity.Error);

        /// <summary>Recomputed metadata fingerprint (store back after a save).</summary>
        public string Fingerprint { get; set; }
    }
}
