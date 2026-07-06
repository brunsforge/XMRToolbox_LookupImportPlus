using System;
using System.Collections.Generic;

namespace LookupImportPlus.Domain
{
    /// <summary>A candidate record returned while resolving a lookup value (import.ts).</summary>
    public sealed class LookupCandidate
    {
        public string Id { get; set; }
        public string EntityLogicalName { get; set; }
        public string PrimaryName { get; set; }

        /// <summary>Selected display attributes → raw values, for the conflict dialog.</summary>
        public Dictionary<string, object> Attributes { get; set; } = new Dictionary<string, object>();

        /// <summary>Deep link to open the record in Dataverse.</summary>
        public string RecordUrl { get; set; }
    }

    /// <summary>Outcome of resolving one lookup value on one row (import.ts).</summary>
    public sealed class LookupResolution
    {
        public string LookupConfigId { get; set; }
        public string LookupAttribute { get; set; }
        public string SourceValue { get; set; }
        public LookupResolutionStatus Status { get; set; } = LookupResolutionStatus.Pending;

        public ResolutionMethod? Method { get; set; }
        public string ResolvedId { get; set; }
        public string ResolvedEntity { get; set; }

        /// <summary>All candidates found (populated on <c>Ambiguous</c>).</summary>
        public List<LookupCandidate> Candidates { get; set; }

        /// <summary>Human-readable filter actually used, captured for audit.</summary>
        public string AppliedFilter { get; set; }

        /// <summary>Relative-date anchors resolved to concrete timestamps at run time.</summary>
        public Dictionary<string, string> ResolvedTimeAnchors { get; set; }

        public LookupResolution Clone()
        {
            return new LookupResolution
            {
                LookupConfigId = LookupConfigId,
                LookupAttribute = LookupAttribute,
                SourceValue = SourceValue,
                Status = Status,
                Method = Method,
                ResolvedId = ResolvedId,
                ResolvedEntity = ResolvedEntity,
                Candidates = Candidates == null ? null : new List<LookupCandidate>(Candidates),
                AppliedFilter = AppliedFilter,
                ResolvedTimeAnchors = ResolvedTimeAnchors == null
                    ? null
                    : new Dictionary<string, string>(ResolvedTimeAnchors)
            };
        }
    }

    public sealed class WriteResult
    {
        public bool Success { get; set; }
        public string RecordId { get; set; }
        public string Error { get; set; }
        public int? HttpStatus { get; set; }
    }

    public sealed class ImportRow
    {
        /// <summary>1-based Excel row number (data rows, excluding header).</summary>
        public int RowNumber { get; set; }

        /// <summary>Raw cell values keyed by Excel header.</summary>
        public Dictionary<string, object> Raw { get; set; } = new Dictionary<string, object>();

        /// <summary>Target record id from <c>lip__recordid</c>, if present.</summary>
        public string TargetRecordId { get; set; }

        public RowStatus Status { get; set; } = RowStatus.Ready;
        public List<string> Messages { get; set; } = new List<string>();
        public List<LookupResolution> Lookups { get; set; } = new List<LookupResolution>();
        public WriteResult WriteResult { get; set; }
    }

    /// <summary>An audit record of a manual conflict resolution (import.ts).</summary>
    public sealed class ResolutionDecision
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public int RowNumber { get; set; }
        public string LookupAttribute { get; set; }
        public string SourceValue { get; set; }
        public List<LookupCandidate> Candidates { get; set; } = new List<LookupCandidate>();

        /// <summary>Chosen target, or null when the user chose to skip.</summary>
        public string ChosenId { get; set; }
        public string ChosenEntity { get; set; }
        public string AppliedFilter { get; set; }
        public string DecidedBy { get; set; }
        public string DecidedOn { get; set; }

        /// <summary>True when the decision was applied to all matching conflicts.</summary>
        public bool AppliedToAll { get; set; }
    }

    public sealed class ImportJob
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string ConfigId { get; set; }

        /// <summary>Immutable snapshot of the configuration used for this run.</summary>
        public JobConfiguration ConfigSnapshot { get; set; }

        public ImportMode Mode { get; set; }
        public ImportJobStatus Status { get; set; }
        public string StartedOn { get; set; }
        public string FinishedOn { get; set; }
        public string StartedBy { get; set; }
        public string FileName { get; set; }

        public int RowCount { get; set; }
        public int ReadyCount { get; set; }
        public int ErrorCount { get; set; }
        public int ConflictCount { get; set; }
        public int CommittedCount { get; set; }

        public List<ImportRow> Rows { get; set; } = new List<ImportRow>();
        public List<ResolutionDecision> Decisions { get; set; } = new List<ResolutionDecision>();
    }

    /// <summary>Status sets shared by the runner and UI (import.ts).</summary>
    public static class StatusSets
    {
        /// <summary>Statuses that block a Strict-mode commit until resolved.</summary>
        public static readonly HashSet<RowStatus> Blocking = new HashSet<RowStatus>
        {
            RowStatus.MissingRequiredValue,
            RowStatus.InvalidFormat,
            RowStatus.LookupNotFound,
            RowStatus.LookupAmbiguous,
            RowStatus.LookupWrongTargetType,
            RowStatus.WriteBlocked,
            RowStatus.CommitFailed
        };

        /// <summary>Statuses whose rows are eligible to be written on commit.</summary>
        public static readonly HashSet<RowStatus> Writable = new HashSet<RowStatus>
        {
            RowStatus.Ready,
            RowStatus.LookupResolved,
            RowStatus.Warning
        };
    }
}
