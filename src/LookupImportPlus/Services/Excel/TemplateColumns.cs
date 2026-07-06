using System.Collections.Generic;
using System.Linq;
using LookupImportPlus.Domain;

namespace LookupImportPlus.Services.Excel
{
    /// <summary>
    /// Pure derivation of the Excel column layout from a job configuration.
    /// Port of src/services/excel/templateColumns.ts.
    /// </summary>
    public static class TemplateColumns
    {
        private static IEnumerable<TemplateColumn> LookupColumns(LookupConfig lk)
        {
            if (!string.IsNullOrEmpty(lk.GuidColumn))
                yield return new TemplateColumn { Header = lk.GuidColumn, Role = TemplateColumnRole.LookupId, Technical = true, LookupId = lk.Id, Attribute = lk.LookupAttribute };
            if (!string.IsNullOrEmpty(lk.LogicalNameColumn))
                yield return new TemplateColumn { Header = lk.LogicalNameColumn, Role = TemplateColumnRole.LookupLogicalName, Technical = true, LookupId = lk.Id, Attribute = lk.LookupAttribute };
            if (!string.IsNullOrEmpty(lk.BusinessKeyColumn))
                yield return new TemplateColumn { Header = lk.BusinessKeyColumn, Role = TemplateColumnRole.LookupBusinessKey, Technical = true, LookupId = lk.Id, Attribute = lk.LookupAttribute };
        }

        /// <summary>
        /// Ordered template columns: each configured column in order, and
        /// immediately after a lookup's visible column its technical columns.
        /// <c>lip__recordid</c> is appended for operations that can update.
        /// </summary>
        public static List<TemplateColumn> Build(JobConfiguration config)
        {
            var lookupByAttr = config.Lookups.ToDictionary(l => l.LookupAttribute, l => l);
            var ordered = config.Columns.OrderBy(c => c.Order).ToList();
            var outList = new List<TemplateColumn>();

            foreach (var col in ordered)
            {
                lookupByAttr.TryGetValue(col.Attribute, out var lk);
                outList.Add(new TemplateColumn
                {
                    Header = col.Header,
                    Attribute = col.Attribute,
                    Role = lk != null ? TemplateColumnRole.LookupVisible : TemplateColumnRole.Value,
                    Technical = col.Usage == ColumnUsage.Technical,
                    LookupId = lk?.Id
                });
                if (lk != null) outList.AddRange(LookupColumns(lk));
            }

            if (config.Operation != OperationType.Create)
            {
                outList.Add(new TemplateColumn
                {
                    Header = TemplateConstants.RecordIdColumn,
                    Role = TemplateColumnRole.RecordId,
                    Technical = true
                });
            }
            return outList;
        }
    }
}
