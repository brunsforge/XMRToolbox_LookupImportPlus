using System;
using System.Collections.Generic;
using System.Linq;
using LookupImportPlus.Data;
using LookupImportPlus.Domain;
using Microsoft.Xrm.Sdk;
using Query = Microsoft.Xrm.Sdk.Query;

namespace LookupImportPlus.Services
{
    /// <summary>
    /// Resolves one Excel value to a target Dataverse record for a configured
    /// lookup, following the fixed, auditable order (port of LookupResolver.ts):
    ///   1. GUID column present      → retrieve by id, verify target type
    ///   2. Business key present     → query by business-key attribute
    ///   3. Search attribute + conditions → query candidates
    ///        · exactly one → resolved · zero → NotFound · many → Ambiguous
    /// Ambiguity is never guessed — it is escalated. The resolver performs NO writes.
    /// </summary>
    public sealed class LookupResolver
    {
        private readonly DataverseContext _ctx;
        private readonly MetadataService _metadata;

        public LookupResolver(DataverseContext ctx, MetadataService metadata)
        {
            _ctx = ctx;
            _metadata = metadata;
        }

        public LookupResolution Resolve(LookupConfig lookup, IReadOnlyDictionary<string, object> row, DateTime? now = null)
        {
            var runNow = now ?? DateTime.UtcNow;
            var res = new LookupResolution
            {
                LookupConfigId = lookup.Id,
                LookupAttribute = lookup.LookupAttribute,
                SourceValue = ReadString(Get(row, lookup.VisibleColumn)),
                Status = LookupResolutionStatus.Pending
            };

            // A filled logical-name column pins the target (must be allowed).
            var pinned = lookup.LogicalNameColumn != null ? ReadString(Get(row, lookup.LogicalNameColumn)) : null;
            if (pinned != null && !lookup.TargetEntities.Contains(pinned))
            {
                res.Status = LookupResolutionStatus.WrongTargetType;
                return res;
            }
            var targets = pinned != null ? new List<string> { pinned } : lookup.TargetEntities;

            // ── 1. GUID ──────────────────────────────────────────
            var guidText = lookup.GuidColumn != null ? ReadString(Get(row, lookup.GuidColumn)) : null;
            if (lookup.Strategy.UseGuidColumn && guidText != null && Guid.TryParse(guidText, out var guid))
            {
                var byGuid = ResolveByGuid(lookup, guid, targets);
                if (byGuid != null)
                {
                    res.Status = LookupResolutionStatus.Resolved;
                    res.Method = ResolutionMethod.Guid;
                    res.ResolvedId = guid.ToString();
                    res.ResolvedEntity = byGuid;
                    return res;
                }
                // GUID missing/invalid → fall through.
            }

            // ── 2. Business key ──────────────────────────────────
            var bkValue = lookup.BusinessKeyColumn != null ? ReadString(Get(row, lookup.BusinessKeyColumn)) : null;
            if (lookup.Strategy.UseBusinessKey && bkValue != null)
            {
                var candidates = QueryCandidates(lookup, targets, target =>
                {
                    var attr = BkAttrFor(lookup, target);
                    return attr == null
                        ? (Query.ConditionExpression[])null
                        : new[] { new Query.ConditionExpression(attr, Query.ConditionOperator.Equal, bkValue) };
                });
                Decide(res, candidates, ResolutionMethod.BusinessKey);
                if (res.Status != LookupResolutionStatus.NotFound) return res;
                // No business-key hit → fall through to search matching.
                res.Candidates = null;
            }

            // ── 3. Search attribute + conditions (per-target for polymorphic) ──
            if (lookup.Strategy.UseSearchMatch)
            {
                var value = res.SourceValue;
                if (value == null) { res.Status = LookupResolutionStatus.NotFound; return res; }

                var anchors = new Dictionary<string, string>();
                string firstReadable = null;

                Func<string, Query.ConditionExpression[]> filterFor = target =>
                {
                    var attr = SearchAttrFor(lookup, target);
                    if (attr == null) return null;
                    var compiled = ConditionCompiler.Compile(ConditionsFor(lookup, target),
                        new CompileContext { Row = row, Now = runNow });
                    foreach (var kv in compiled.TimeAnchors) anchors[kv.Key] = kv.Value;

                    var conds = new List<Query.ConditionExpression>
                    {
                        new Query.ConditionExpression(attr, Query.ConditionOperator.Equal, value)
                    };
                    conds.AddRange(compiled.Conditions);

                    var readable = $"{attr} eq '{value.Replace("'", "''")}'";
                    if (!string.IsNullOrEmpty(compiled.ReadableFilter)) readable += " and " + compiled.ReadableFilter;
                    if (firstReadable == null) firstReadable = readable;
                    return conds.ToArray();
                };

                var candidates = QueryCandidates(lookup, targets, filterFor);
                Decide(res, candidates, ResolutionMethod.SearchMatch);
                res.AppliedFilter = firstReadable;
                res.ResolvedTimeAnchors = anchors;
                return res;
            }

            res.Status = LookupResolutionStatus.NotFound;
            return res;
        }

        private string ResolveByGuid(LookupConfig lookup, Guid guid, List<string> targets)
        {
            foreach (var logicalName in targets)
            {
                EntitySummary summary;
                try { summary = _metadata.GetEntitySummary(logicalName); }
                catch { continue; }
                var record = _ctx.Retrieve(logicalName, guid, new Query.ColumnSet(SelectFor(lookup, summary)));
                if (record != null) return logicalName;
            }
            return null;
        }

        /// <summary>
        /// Query each target with its own conditions and collect candidates.
        /// Targets whose condition set is null (e.g. no business-key attribute for
        /// that table) are skipped — never queried unfiltered.
        /// </summary>
        private List<LookupCandidate> QueryCandidates(
            LookupConfig lookup,
            List<string> targets,
            Func<string, Query.ConditionExpression[]> filterFor)
        {
            var outList = new List<LookupCandidate>();
            foreach (var logicalName in targets)
            {
                var conditions = filterFor(logicalName);
                if (conditions == null) continue;

                EntitySummary summary;
                try { summary = _metadata.GetEntitySummary(logicalName); }
                catch { continue; }

                var query = new Query.QueryExpression(logicalName)
                {
                    ColumnSet = new Query.ColumnSet(SelectFor(lookup, summary)),
                    TopCount = 50
                };
                foreach (var c in conditions) query.Criteria.AddCondition(c);

                var result = _ctx.RetrieveMultiple(query);
                foreach (var rec in result.Entities)
                    outList.Add(ToCandidate(rec, summary, lookup));
            }
            return outList;
        }

        private string[] SelectFor(LookupConfig lookup, EntitySummary summary)
        {
            var cols = new List<string> { summary.PrimaryIdAttribute, summary.PrimaryNameAttribute };
            cols.AddRange(lookup.CandidateDisplayAttributes ?? Enumerable.Empty<string>());
            return cols.Where(c => !string.IsNullOrEmpty(c)).Distinct().ToArray();
        }

        private LookupCandidate ToCandidate(Entity rec, EntitySummary summary, LookupConfig lookup)
        {
            var id = rec.Id;
            var attributes = new Dictionary<string, object>();
            foreach (var a in lookup.CandidateDisplayAttributes ?? Enumerable.Empty<string>())
                attributes[a] = FormatValue(rec, a);

            return new LookupCandidate
            {
                Id = id.ToString(),
                EntityLogicalName = summary.LogicalName,
                PrimaryName = ReadString(FormatValue(rec, summary.PrimaryNameAttribute)) ?? "",
                Attributes = attributes,
                RecordUrl = _ctx.RecordUrl(summary.LogicalName, id)
            };
        }

        /// <summary>Turn a candidate set into a resolution outcome (never guesses).</summary>
        private static void Decide(LookupResolution res, List<LookupCandidate> candidates, ResolutionMethod method)
        {
            if (candidates.Count == 0) { res.Status = LookupResolutionStatus.NotFound; return; }
            if (candidates.Count == 1)
            {
                res.Status = LookupResolutionStatus.Resolved;
                res.Method = method;
                res.ResolvedId = candidates[0].Id;
                res.ResolvedEntity = candidates[0].EntityLogicalName;
                return;
            }
            res.Status = LookupResolutionStatus.Ambiguous;
            res.Candidates = candidates;
        }

        private static object Get(IReadOnlyDictionary<string, object> row, string key)
        {
            if (string.IsNullOrEmpty(key) || row == null) return null;
            row.TryGetValue(key, out var v);
            return v;
        }

        private static string ReadString(object v)
        {
            if (v == null) return null;
            var s = v.ToString().Trim();
            return s.Length == 0 ? null : s;
        }

        /// <summary>Prefer the formatted (display) value of a field, else the raw value.</summary>
        private static object FormatValue(Entity rec, string attr)
        {
            if (string.IsNullOrEmpty(attr)) return null;
            if (rec.FormattedValues.Contains(attr)) return rec.FormattedValues[attr];
            var raw = rec.Contains(attr) ? rec[attr] : null;
            if (raw is EntityReference er) return er.Name ?? er.Id.ToString();
            if (raw is OptionSetValue osv) return osv.Value;
            if (raw is Money m) return m.Value;
            return raw;
        }

        private static string SearchAttrFor(LookupConfig lk, string target)
        {
            if (lk.TargetOverrides != null && lk.TargetOverrides.TryGetValue(target, out var o) && !string.IsNullOrEmpty(o.SearchAttribute))
                return o.SearchAttribute;
            return lk.SearchAttribute;
        }

        private static string BkAttrFor(LookupConfig lk, string target)
        {
            if (lk.TargetOverrides != null && lk.TargetOverrides.TryGetValue(target, out var o) && !string.IsNullOrEmpty(o.BusinessKeyAttribute))
                return o.BusinessKeyAttribute;
            return lk.BusinessKeyAttribute;
        }

        private static ConditionGroup ConditionsFor(LookupConfig lk, string target)
        {
            if (lk.TargetOverrides != null && lk.TargetOverrides.TryGetValue(target, out var o) && o.Conditions != null)
                return o.Conditions;
            return lk.Conditions;
        }
    }
}
