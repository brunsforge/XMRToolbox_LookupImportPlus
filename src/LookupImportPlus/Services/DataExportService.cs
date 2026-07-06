using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LookupImportPlus.Data;
using LookupImportPlus.Domain;
using LookupImportPlus.Services.Excel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace LookupImportPlus.Services
{
    /// <summary>
    /// Pulls records for a configuration's target entity for a data export or a
    /// preview (port of DataExportService.ts). SDK-native: reads via
    /// QueryExpression, or a saved view's FetchXML when the config is view-based.
    /// Presents records as CRM columns (raw fields) or as the Excel schema layout.
    /// </summary>
    public sealed class DataExportService
    {
        private readonly DataverseContext _ctx;
        private readonly MetadataService _metadata;

        public DataExportService(DataverseContext ctx, MetadataService metadata)
        {
            _ctx = ctx;
            _metadata = metadata;
        }

        /// <summary>Real Dataverse attributes for a config (business + lookup fields).</summary>
        public List<string> CrmColumns(JobConfiguration config)
        {
            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { config.PrimaryIdAttribute };
            foreach (var c in config.Columns) cols.Add(c.Attribute);
            return cols.Where(c => !string.IsNullOrEmpty(c)).ToList();
        }

        /// <summary>Fetch up to <paramref name="count"/> records for the config.</summary>
        public List<Entity> FetchRecords(JobConfiguration config, int count, string viewFetchXml = null)
        {
            if (config.ExportSource.Kind == ExportSourceKind.SavedView && !string.IsNullOrEmpty(viewFetchXml))
            {
                var res = _ctx.RetrieveMultiple(new FetchExpression(viewFetchXml));
                return res.Entities.Take(count).ToList();
            }

            var query = new QueryExpression(config.TargetEntity)
            {
                ColumnSet = new ColumnSet(CrmColumns(config).ToArray()),
                TopCount = Math.Min(count, 5000)
            };
            return _ctx.RetrieveMultiple(query).Entities.ToList();
        }

        /// <summary>Render a record as CRM columns → display values (for the preview toggle).</summary>
        public Dictionary<string, object> ToCrmRow(JobConfiguration config, Entity record)
        {
            var row = new Dictionary<string, object>();
            foreach (var col in CrmColumns(config))
                row[col] = DisplayValue(record, col);
            return row;
        }

        /// <summary>Map records to Excel schema rows (keyed by template header), with enrichment.</summary>
        public List<Dictionary<string, object>> ToSchemaRows(JobConfiguration config, IReadOnlyList<Entity> records)
        {
            var businessKeys = ResolveBusinessKeys(config, records);
            return records.Select(r => ToSchemaRow(config, r, businessKeys)).ToList();
        }

        public List<Dictionary<string, object>> FetchSchemaRows(JobConfiguration config, int count, string viewFetchXml = null)
        {
            return ToSchemaRows(config, FetchRecords(config, count, viewFetchXml));
        }

        private Dictionary<string, object> ToSchemaRow(
            JobConfiguration config,
            Entity record,
            Dictionary<string, Dictionary<Guid, string>> businessKeys)
        {
            var row = new Dictionary<string, object>();
            foreach (var col in TemplateColumns.Build(config))
            {
                var a = col.Attribute;
                switch (col.Role)
                {
                    case TemplateColumnRole.Value:
                        row[col.Header] = a != null ? RawValue(record, a) : "";
                        break;
                    case TemplateColumnRole.LookupVisible:
                        row[col.Header] = a != null ? (LookupName(record, a) ?? "") : "";
                        break;
                    case TemplateColumnRole.LookupId:
                        row[col.Header] = a != null ? (LookupId(record, a)?.ToString() ?? "") : "";
                        break;
                    case TemplateColumnRole.LookupLogicalName:
                        row[col.Header] = a != null ? (LookupLogicalName(record, a) ?? "") : "";
                        break;
                    case TemplateColumnRole.LookupBusinessKey:
                        row[col.Header] = BusinessKeyFor(record, a, col.LookupId, config, businessKeys) ?? "";
                        break;
                    case TemplateColumnRole.RecordId:
                        row[TemplateConstants.RecordIdColumn] = record.Id.ToString();
                        break;
                }
            }
            return row;
        }

        /// <summary>Batch-resolve business-key values for lookups that need them.</summary>
        private Dictionary<string, Dictionary<Guid, string>> ResolveBusinessKeys(
            JobConfiguration config,
            IReadOnlyList<Entity> records)
        {
            // keyed by "target|bkAttr" → (referenced id → business-key value)
            var map = new Dictionary<string, Dictionary<Guid, string>>();

            foreach (var lk in config.Lookups)
            {
                if (string.IsNullOrEmpty(lk.BusinessKeyColumn)) continue;

                foreach (var target in lk.TargetEntities)
                {
                    var bkAttr = BkAttrFor(lk, target);
                    if (string.IsNullOrEmpty(bkAttr)) continue;

                    var ids = records
                        .Select(r => r.GetAttributeValue<EntityReference>(lk.LookupAttribute))
                        .Where(er => er != null && string.Equals(er.LogicalName, target, StringComparison.OrdinalIgnoreCase))
                        .Select(er => er.Id)
                        .Distinct()
                        .ToList();
                    if (ids.Count == 0) continue;

                    EntitySummary summary;
                    try { summary = _metadata.GetEntitySummary(target); }
                    catch { continue; }

                    var byId = new Dictionary<Guid, string>();
                    foreach (var chunk in Chunk(ids, 200))
                    {
                        var query = new QueryExpression(target)
                        {
                            ColumnSet = new ColumnSet(summary.PrimaryIdAttribute, bkAttr)
                        };
                        query.Criteria.AddCondition(summary.PrimaryIdAttribute, Microsoft.Xrm.Sdk.Query.ConditionOperator.In, chunk.Cast<object>().ToArray());
                        try
                        {
                            foreach (var rec in _ctx.RetrieveMultiple(query).Entities)
                                byId[rec.Id] = RawValue(rec, bkAttr)?.ToString() ?? "";
                        }
                        catch { /* ignore a failed batch */ }
                    }
                    map[$"{target}|{bkAttr}"] = byId;
                }
            }
            return map;
        }

        private string BusinessKeyFor(
            Entity record,
            string lookupAttribute,
            string lookupId,
            JobConfiguration config,
            Dictionary<string, Dictionary<Guid, string>> businessKeys)
        {
            var lk = config.Lookups.FirstOrDefault(l => l.Id == lookupId)
                     ?? config.Lookups.FirstOrDefault(l => l.LookupAttribute == lookupAttribute);
            if (lk == null) return null;
            var er = record.GetAttributeValue<EntityReference>(lookupAttribute);
            if (er == null) return null;
            var bkAttr = BkAttrFor(lk, er.LogicalName);
            if (string.IsNullOrEmpty(bkAttr)) return null;
            return businessKeys.TryGetValue($"{er.LogicalName}|{bkAttr}", out var byId) && byId.TryGetValue(er.Id, out var v)
                ? v : null;
        }

        private static string BkAttrFor(LookupConfig lk, string target)
        {
            if (lk.TargetOverrides != null && lk.TargetOverrides.TryGetValue(target, out var o) && !string.IsNullOrEmpty(o.BusinessKeyAttribute))
                return o.BusinessKeyAttribute;
            return lk.BusinessKeyAttribute;
        }

        private static Guid? LookupId(Entity record, string attr)
            => record.GetAttributeValue<EntityReference>(attr)?.Id;

        private static string LookupLogicalName(Entity record, string attr)
            => record.GetAttributeValue<EntityReference>(attr)?.LogicalName;

        private static string LookupName(Entity record, string attr)
        {
            var er = record.GetAttributeValue<EntityReference>(attr);
            if (er == null) return null;
            if (!string.IsNullOrEmpty(er.Name)) return er.Name;
            if (record.FormattedValues.Contains(attr)) return record.FormattedValues[attr];
            return er.Id.ToString();
        }

        /// <summary>Excel-friendly raw value for scalar attributes (round-trippable).</summary>
        private static object RawValue(Entity record, string attr)
        {
            if (!record.Contains(attr)) return "";
            var v = record[attr];
            switch (v)
            {
                case null: return "";
                case EntityReference er: return er.Name ?? er.Id.ToString();
                case OptionSetValue osv:
                    return record.FormattedValues.Contains(attr) ? (object)record.FormattedValues[attr] : osv.Value;
                case Money m: return m.Value;
                case DateTime dt: return dt.ToString("o", CultureInfo.InvariantCulture);
                case bool b: return b;
                default: return v;
            }
        }

        /// <summary>Display value (prefers formatted) for the CRM-columns preview.</summary>
        private static object DisplayValue(Entity record, string attr)
        {
            if (record.FormattedValues.Contains(attr)) return record.FormattedValues[attr];
            return RawValue(record, attr);
        }

        private static IEnumerable<List<Guid>> Chunk(List<Guid> items, int size)
        {
            for (int i = 0; i < items.Count; i += size)
                yield return items.GetRange(i, Math.Min(size, items.Count - i));
        }
    }
}
