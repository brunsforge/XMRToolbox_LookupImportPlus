using System;
using System.Collections.Generic;

namespace LookupImportPlus.Domain
{
    /// <summary>
    /// Classification of a row/lookup after the dry run. Port of
    /// src/domain/issues.ts. Extend as the source enumerates more cases.
    /// </summary>
    public enum RowStatus
    {
        /// <summary>Resolved deterministically; safe to write.</summary>
        Ready,

        /// <summary>More than one candidate -> conflict strategy applies.</summary>
        Ambiguous,

        /// <summary>Zero candidates found.</summary>
        NotFound,

        /// <summary>A required value was empty in the source row.</summary>
        MissingRequiredValue
    }

    /// <summary>
    /// Which matching stage produced a resolution. Drives the audit log and the
    /// "why this query" explanation in the resolve screen.
    /// </summary>
    public enum MatchStage
    {
        None,
        Guid,        // stage 1
        BusinessKey, // stage 2
        SearchField  // stage 3
    }

    /// <summary>
    /// One candidate returned while resolving a lookup, shown in the conflict
    /// resolution screen (3.7).
    /// </summary>
    public sealed class Candidate
    {
        public Guid Id { get; set; }
        public string TargetEntity { get; set; }
        public string PrimaryName { get; set; }
        public string BusinessKey { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }
}
