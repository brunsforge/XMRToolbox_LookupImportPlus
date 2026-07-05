using System;
using System.Collections.Generic;
using LookupImportPlus.Domain;
using Microsoft.Xrm.Sdk;

namespace LookupImportPlus.Services
{
    /// <summary>Outcome of resolving one lookup for one row.</summary>
    public sealed class ResolutionResult
    {
        public RowStatus Status { get; set; } = RowStatus.NotFound;
        public MatchStage Stage { get; set; } = MatchStage.None;
        public Guid? ResolvedId { get; set; }
        public string ResolvedTargetEntity { get; set; }
        public IReadOnlyList<Candidate> Candidates { get; set; } = Array.Empty<Candidate>();
    }

    /// <summary>
    /// Deterministic lookup resolution. Exact port of src/services/LookupResolver.ts
    /// and the matching order in src/domain/config.ts. Per row and lookup:
    ///
    ///   1) GUID column filled?   -> Retrieve by id, verify target type -> bind. Done.
    ///   2) Business-key column?  -> Retrieve-by-key (alternate key) / query the
    ///                               business-key attribute. exactly 1 -> bind; 0 -> NotFound.
    ///   3) Search field + conditions -> QueryExpression: search eq excelValue + conditions.
    ///                               exactly 1 -> bind | 0 -> NotFound | >1 -> Ambiguous.
    ///
    /// Ambiguity is never resolved silently; it escalates per ConflictStrategy.
    /// Polymorphic lookups run stages 2-3 per checked target with that target's
    /// own attribute names.
    /// </summary>
    public sealed class LookupResolver
    {
        private readonly IOrganizationService _service;

        public LookupResolver(IOrganizationService service)
        {
            _service = service;
        }

        /// <summary>
        /// Resolve one lookup for one source row. <paramref name="row"/> maps Excel
        /// column header -> cell value.
        /// </summary>
        public ResolutionResult Resolve(LookupConfig config, IReadOnlyDictionary<string, string> row)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (row == null) throw new ArgumentNullException(nameof(row));

            // Stage 1 - GUID column (highest precedence, no conflict screen).
            if (TryGetValue(row, config.GuidColumn, out var guidText)
                && Guid.TryParse(guidText, out var id))
            {
                // TODO: Retrieve by id across config.TargetEntities, verify the
                // record exists and its type matches; set ResolvedTargetEntity.
                return new ResolutionResult
                {
                    Status = RowStatus.Ready,
                    Stage = MatchStage.Guid,
                    ResolvedId = id
                };
            }

            // Stage 2 - business key (alternate key) per target.
            // TODO: for each target with a BusinessKeyAttribute, Retrieve-by-key
            // using the value in config.BusinessKeyColumn.

            // Stage 3 - search field + conditions per target.
            // TODO: build a QueryExpression per target (searchAttribute eq excel
            // value + compiled Conditions), aggregate candidates across targets,
            // then classify: 1 -> Ready, 0 -> NotFound, >1 -> Ambiguous.

            return new ResolutionResult
            {
                Status = RowStatus.NotFound,
                Stage = MatchStage.None
            };
        }

        private static bool TryGetValue(IReadOnlyDictionary<string, string> row, string column, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(column)) return false;
            return row.TryGetValue(column, out value) && !string.IsNullOrWhiteSpace(value);
        }
    }
}
