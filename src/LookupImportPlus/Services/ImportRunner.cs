using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LookupImportPlus.Data;
using LookupImportPlus.Domain;
using LookupImportPlus.Services.Excel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace LookupImportPlus.Services
{
    /// <summary>
    /// Orchestrates a run: dry-run validation, conflict-decision application, and
    /// the controlled commit (the ONLY place records are written). Port of
    /// ImportRunner.ts. Commit uses ExecuteMultipleRequest (ContinueOnError) so
    /// one bad row never aborts the batch; Strict mode refuses to write while any
    /// blocking row remains.
    /// </summary>
    public sealed class ImportRunner
    {
        private const int CommitBatchSize = 200;

        private readonly DataverseContext _ctx;
        private readonly MetadataService _metadata;
        private readonly LookupResolver _resolver;

        public ImportRunner(DataverseContext ctx, MetadataService metadata, LookupResolver resolver)
        {
            _ctx = ctx;
            _metadata = metadata;
            _resolver = resolver;
        }

        // ── Dry run ──────────────────────────────────────────────
        public ImportJob DryRun(
            JobConfiguration config,
            IReadOnlyList<ParsedRow> parsed,
            ImportMode? mode = null,
            DateTime? now = null,
            Action<int, int> onProgress = null)
        {
            var runNow = now ?? DateTime.UtcNow;
            var startedBy = _ctx.WhoAmI().ToString();
            var total = parsed.Count;

            // Dedup identical lookup inputs so N rows with the same value cost one query.
            var resolveCache = new Dictionary<string, LookupResolution>();

            var rows = new List<ImportRow>();
            var done = 0;
            foreach (var p in parsed)
            {
                var raw = p.Values;
                var targetRecordId = ReadString(GetVal(raw, TemplateConstants.RecordIdColumn));
                var messages = new List<string>();

                // required-field validation
                RowStatus? blockingValidation = null;
                foreach (var rule in config.ValidationRules.Where(r => r.Kind == "required"))
                {
                    var col = config.Columns.FirstOrDefault(c => c.Attribute == rule.Attribute);
                    var header = col?.Header ?? rule.Attribute;
                    if (ReadString(GetVal(raw, header)) == null)
                    {
                        blockingValidation = RowStatus.MissingRequiredValue;
                        messages.Add(rule.Message ?? $"Pflichtfeld fehlt: {header}");
                    }
                }

                // lookups (cached by the row values each lookup actually reads)
                var lookups = new List<LookupResolution>();
                foreach (var lk in config.Lookups)
                {
                    var key = LookupCacheKey(lk, raw);
                    if (!resolveCache.TryGetValue(key, out var res))
                    {
                        try
                        {
                            res = _resolver.Resolve(lk, raw, runNow);
                        }
                        catch (Exception e)
                        {
                            res = new LookupResolution
                            {
                                LookupConfigId = lk.Id,
                                LookupAttribute = lk.LookupAttribute,
                                SourceValue = ReadString(GetVal(raw, lk.VisibleColumn)),
                                Status = LookupResolutionStatus.NotFound
                            };
                            messages.Add($"Lookup '{lk.LookupAttribute}' konnte nicht ausgewertet werden: {e.Message}");
                        }
                        resolveCache[key] = res;
                    }
                    lookups.Add(res.Clone());
                }

                var status = blockingValidation ?? LookupRowStatus(lookups);
                rows.Add(new ImportRow
                {
                    RowNumber = p.RowNumber,
                    Raw = raw,
                    TargetRecordId = targetRecordId,
                    Status = status,
                    Messages = messages,
                    Lookups = lookups
                });
                onProgress?.Invoke(++done, total);
            }

            FlagDuplicates(rows);

            var job = new ImportJob
            {
                ConfigId = config.Id,
                ConfigSnapshot = CloneConfig(config),
                Mode = mode ?? config.DefaultMode,
                Status = ImportJobStatus.Validated,
                StartedOn = runNow.ToString("o"),
                StartedBy = startedBy,
                RowCount = rows.Count,
                Rows = rows
            };
            RecomputeCounts(job);
            job.Status = job.ConflictCount > 0 ? ImportJobStatus.AwaitingConflicts : ImportJobStatus.Validated;
            return job;
        }

        // ── Apply a conflict resolution decision ─────────────────
        public ImportJob ApplyDecision(ImportJob job, ResolutionDecision decision)
        {
            var targets = job.Rows.Where(row =>
            {
                if (!decision.AppliedToAll) return row.RowNumber == decision.RowNumber;
                return row.Lookups.Any(l =>
                    l.LookupAttribute == decision.LookupAttribute &&
                    l.SourceValue == decision.SourceValue &&
                    (l.Status == LookupResolutionStatus.Ambiguous || l.Status == LookupResolutionStatus.NotFound));
            }).ToList();

            foreach (var row in targets)
            {
                var res = row.Lookups.FirstOrDefault(l => l.LookupAttribute == decision.LookupAttribute);
                if (res == null) continue;
                if (decision.ChosenId == null)
                {
                    row.Status = RowStatus.Skipped;
                }
                else
                {
                    res.Status = LookupResolutionStatus.Resolved;
                    res.Method = ResolutionMethod.Manual;
                    res.ResolvedId = decision.ChosenId;
                    res.ResolvedEntity = decision.ChosenEntity;
                    res.Candidates = null;
                    row.Status = row.Messages.Count > 0 ? row.Status : LookupRowStatus(row.Lookups);
                }
            }

            job.Decisions.Add(decision);
            RecomputeCounts(job);
            job.Status = job.ConflictCount > 0 ? ImportJobStatus.AwaitingConflicts : ImportJobStatus.Validated;
            return job;
        }

        // ── Commit ───────────────────────────────────────────────
        public ImportJob Commit(ImportJob job, Action<int, int> onProgress = null)
        {
            var config = job.ConfigSnapshot;
            var entityMeta = _metadata.GetEntity(config.TargetEntity);

            var hasBlocking = job.Rows.Any(r => StatusSets.Blocking.Contains(r.Status));
            if (job.Mode == ImportMode.Strict && hasBlocking)
                return job; // Strict never writes while blocking rows remain.

            var writable = job.Rows.Where(r => StatusSets.Writable.Contains(r.Status)).ToList();
            var total = writable.Count;
            var done = 0;
            job.Status = ImportJobStatus.Committing;

            foreach (var batch in Batch(writable, CommitBatchSize))
            {
                var request = new ExecuteMultipleRequest
                {
                    Settings = new ExecuteMultipleSettings { ContinueOnError = true, ReturnResponses = true },
                    Requests = new OrganizationRequestCollection()
                };
                var batchRows = new List<(ImportRow Row, bool Update)>();

                foreach (var row in batch)
                {
                    try
                    {
                        var entity = BuildPayload(config, entityMeta, row);
                        var doUpdate = config.Operation == OperationType.Update ||
                                       (config.Operation == OperationType.CreateOrUpdate && !string.IsNullOrEmpty(row.TargetRecordId));

                        if (config.Operation == OperationType.Update && string.IsNullOrEmpty(row.TargetRecordId))
                        {
                            row.WriteResult = new WriteResult { Success = false, Error = $"Update ohne {TemplateConstants.RecordIdColumn}", HttpStatus = 400 };
                            row.Status = RowStatus.CommitFailed;
                            done++; onProgress?.Invoke(done, total);
                            continue;
                        }

                        if (doUpdate)
                        {
                            entity.Id = Guid.Parse(row.TargetRecordId);
                            request.Requests.Add(new UpdateRequest { Target = entity });
                        }
                        else
                        {
                            request.Requests.Add(new CreateRequest { Target = entity });
                        }
                        batchRows.Add((row, doUpdate));
                    }
                    catch (Exception e)
                    {
                        row.WriteResult = new WriteResult { Success = false, Error = e.Message };
                        row.Status = RowStatus.CommitFailed;
                        done++; onProgress?.Invoke(done, total);
                    }
                }

                if (request.Requests.Count == 0) continue;

                ExecuteMultipleResponse response;
                try
                {
                    response = (ExecuteMultipleResponse)_ctx.Execute(request);
                }
                catch (Exception e)
                {
                    foreach (var (row, _) in batchRows)
                    {
                        row.WriteResult = new WriteResult { Success = false, Error = e.Message };
                        row.Status = RowStatus.CommitFailed;
                        done++; onProgress?.Invoke(done, total);
                    }
                    continue;
                }

                foreach (var item in response.Responses)
                {
                    var (row, update) = batchRows[item.RequestIndex];
                    if (item.Fault != null)
                    {
                        row.WriteResult = new WriteResult { Success = false, Error = item.Fault.Message };
                        row.Status = RowStatus.CommitFailed;
                    }
                    else
                    {
                        var id = update ? row.TargetRecordId
                            : (item.Response is CreateResponse cr ? cr.id.ToString() : null);
                        row.WriteResult = new WriteResult { Success = true, RecordId = id };
                        row.Status = RowStatus.Committed;
                    }
                    done++; onProgress?.Invoke(done, total);
                }
            }

            RecomputeCounts(job);
            job.CommittedCount = job.Rows.Count(r => r.Status == RowStatus.Committed);
            job.Status = job.Rows.Any(r => r.Status == RowStatus.CommitFailed)
                ? ImportJobStatus.CompletedWithErrors : ImportJobStatus.Completed;
            job.FinishedOn = DateTime.UtcNow.ToString("o");
            return job;
        }

        /// <summary>Map a row to a Dataverse entity, including EntityReference lookups.</summary>
        private Entity BuildPayload(JobConfiguration config, EntityMetadata entityMeta, ImportRow row)
        {
            var entity = new Entity(config.TargetEntity);

            foreach (var col in config.Columns)
            {
                if (col.Usage == ColumnUsage.Technical || col.Usage == ColumnUsage.ExportOnly) continue;
                if (config.Lookups.Any(l => l.LookupAttribute == col.Attribute)) continue; // lookups below
                var value = Coerce(GetVal(row.Raw, col.Header), col.Kind);
                if (value != null) entity[col.Attribute] = value;
            }

            foreach (var res in row.Lookups)
            {
                if (res.Status != LookupResolutionStatus.Resolved || res.ResolvedId == null || res.ResolvedEntity == null) continue;
                var attr = entityMeta.Attributes.FirstOrDefault(a => a.LogicalName == res.LookupAttribute);
                var target = attr?.Lookup?.Targets.FirstOrDefault(t => t.LogicalName == res.ResolvedEntity);
                if (target == null) continue;
                entity[res.LookupAttribute] = new EntityReference(res.ResolvedEntity, Guid.Parse(res.ResolvedId));
            }
            return entity;
        }

        // ── helpers ──────────────────────────────────────────────

        private static object Coerce(object raw, AttributeKind kind)
        {
            var s = raw?.ToString();
            if (string.IsNullOrEmpty(s)) return null;
            switch (kind)
            {
                case AttributeKind.Integer:
                    return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? (object)i : null;
                case AttributeKind.BigInt:
                    return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? (object)l : null;
                case AttributeKind.Decimal:
                    return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? (object)d : null;
                case AttributeKind.Double:
                    return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var db) ? (object)db : null;
                case AttributeKind.Money:
                    return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var mv) ? (object)new Money(mv) : null;
                case AttributeKind.Boolean:
                    if (raw is bool b) return b;
                    return new[] { "true", "1", "ja", "yes", "wahr" }.Contains(s.ToLowerInvariant());
                case AttributeKind.DateTime:
                    return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt) ? (object)dt : null;
                case AttributeKind.Choice:
                    return int.TryParse(s, out var opt) ? (object)new OptionSetValue(opt) : null;
                default:
                    return s;
            }
        }

        private static IEnumerable<string> CollectExcelColumns(ConditionGroup group)
        {
            if (group == null) yield break;
            foreach (var c in group.Conditions)
                if (c.Value?.Kind == ValueSourceKind.ExcelColumn && !string.IsNullOrEmpty(c.Value.Column))
                    yield return c.Value.Column;
            foreach (var g in group.Groups ?? Enumerable.Empty<ConditionGroup>())
                foreach (var col in CollectExcelColumns(g))
                    yield return col;
        }

        private static string LookupCacheKey(LookupConfig lk, IDictionary<string, object> row)
        {
            var cols = new List<string> { lk.VisibleColumn, lk.GuidColumn, lk.LogicalNameColumn, lk.BusinessKeyColumn };
            cols.AddRange(CollectExcelColumns(lk.Conditions));
            var parts = cols.Where(c => !string.IsNullOrEmpty(c)).Select(c => $"{c}={ReadString(GetVal(row, c)) ?? ""}");
            return $"{lk.Id}|{string.Join("|", parts)}";
        }

        /// <summary>
        /// Row status contributed by its lookups (excluding validation blocks).
        /// <see cref="LookupResolutionStatus.Empty"/> is non-blocking: it neither
        /// resolves nor conflicts, so a row whose only lookups are empty is Ready.
        /// </summary>
        public static RowStatus LookupRowStatus(List<LookupResolution> lookups)
        {
            if (lookups.Any(l => l.Status == LookupResolutionStatus.WrongTargetType)) return RowStatus.LookupWrongTargetType;
            if (lookups.Any(l => l.Status == LookupResolutionStatus.NotFound)) return RowStatus.LookupNotFound;
            if (lookups.Any(l => l.Status == LookupResolutionStatus.Ambiguous)) return RowStatus.LookupAmbiguous;
            if (lookups.Any(l => l.Status == LookupResolutionStatus.Resolved)
                && lookups.All(l => l.Status == LookupResolutionStatus.Resolved || l.Status == LookupResolutionStatus.Empty))
                return RowStatus.LookupResolved;
            return RowStatus.Ready;
        }

        private static void FlagDuplicates(List<ImportRow> rows)
        {
            var seen = new Dictionary<string, ImportRow>();
            foreach (var row in rows)
            {
                if (string.IsNullOrEmpty(row.TargetRecordId)) continue;
                if (seen.TryGetValue(row.TargetRecordId, out var prev))
                {
                    row.Status = RowStatus.DuplicateInFile;
                    row.Messages.Add($"Dieselbe Ziel-ID wie Zeile {prev.RowNumber}.");
                }
                else
                {
                    seen[row.TargetRecordId] = row;
                }
            }
        }

        private static void RecomputeCounts(ImportJob job)
        {
            int ready = 0, error = 0, conflict = 0;
            foreach (var r in job.Rows)
            {
                if (r.Status == RowStatus.Ready || r.Status == RowStatus.LookupResolved) ready++;
                if (r.Status == RowStatus.LookupAmbiguous || r.Status == RowStatus.LookupNotFound || r.Status == RowStatus.LookupWrongTargetType) conflict++;
                else if (StatusSets.Blocking.Contains(r.Status)) error++;
            }
            job.ReadyCount = ready;
            job.ErrorCount = error;
            job.ConflictCount = conflict;
        }

        private static JobConfiguration CloneConfig(JobConfiguration config)
            => Json.Deserialize<JobConfiguration>(Json.Serialize(config));

        private static object GetVal(IDictionary<string, object> row, string key)
        {
            if (row == null || string.IsNullOrEmpty(key)) return null;
            row.TryGetValue(key, out var v);
            return v;
        }

        private static string ReadString(object v)
        {
            if (v == null) return null;
            var s = v.ToString().Trim();
            return s.Length == 0 ? null : s;
        }

        private static IEnumerable<List<T>> Batch<T>(List<T> items, int size)
        {
            for (int i = 0; i < items.Count; i += size)
                yield return items.GetRange(i, Math.Min(size, items.Count - i));
        }
    }
}
