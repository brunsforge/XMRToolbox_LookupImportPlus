using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using LookupImportPlus.Domain;

namespace LookupImportPlus.Services.Excel
{
    /// <summary>
    /// Generates the import/export XLSX (port of ExcelTemplateService.ts).
    /// Produces a <c>Daten</c> sheet with visible + technical columns (technical
    /// lookup columns and <c>lip__recordid</c> hidden) and a very-hidden
    /// <c>_LookupImportPlus</c> sheet carrying the manifest.
    /// </summary>
    public sealed class ExcelTemplateService
    {
        /// <summary>Build the manifest (without I/O) — exposed for inspection/tests.</summary>
        public TemplateManifest BuildManifest(JobConfiguration config, List<TemplateColumn> columns = null)
        {
            var cols = columns ?? TemplateColumns.Build(config);
            var manifest = new TemplateManifest
            {
                ConfigId = config.Id,
                ConfigName = config.Name,
                ConfigVersion = config.Version,
                SchemaVersion = TemplateConstants.TemplateSchemaVersion,
                TargetEntity = config.TargetEntity,
                EntitySetName = config.EntitySetName,
                Operation = config.Operation.ToString(),
                Columns = cols
            };
            manifest.Hash = ManifestHash.HashManifest(manifest);
            manifest.GeneratedOn = DateTime.UtcNow.ToString("o");
            return manifest;
        }

        /// <summary>
        /// Build the workbook. Pass <paramref name="dataRows"/> (keyed by column
        /// header) for a data export; omit for an empty template.
        /// </summary>
        public byte[] Build(JobConfiguration config, IReadOnlyList<IDictionary<string, object>> dataRows = null)
        {
            var columns = TemplateColumns.Build(config);
            var manifest = BuildManifest(config, columns);

            using (var wb = new XLWorkbook())
            {
                wb.Properties.Author = "LookupImportPlus";

                var ws = wb.AddWorksheet(TemplateConstants.DataSheet);
                for (int i = 0; i < columns.Count; i++)
                {
                    var c = columns[i];
                    var cell = ws.Cell(1, i + 1);
                    cell.Value = c.Header;
                    cell.Style.Font.Bold = true;
                    var column = ws.Column(i + 1);
                    column.Width = Math.Max(12, c.Header.Length + 2);
                    if (c.Technical) column.Hide();
                }
                ws.SheetView.FreezeRows(1);

                int r = 2;
                foreach (var row in dataRows ?? new List<IDictionary<string, object>>())
                {
                    for (int i = 0; i < columns.Count; i++)
                    {
                        if (row.TryGetValue(columns[i].Header, out var v))
                            SetCell(ws.Cell(r, i + 1), v);
                    }
                    r++;
                }

                var hidden = wb.AddWorksheet(TemplateConstants.ManifestSheet);
                hidden.Cell("A1").Value = "LookupImportPlus-Manifest — do not edit";
                hidden.Cell("A2").Value = Json.Serialize(manifest);
                hidden.Visibility = XLWorksheetVisibility.VeryHidden;

                using (var ms = new MemoryStream())
                {
                    wb.SaveAs(ms);
                    return ms.ToArray();
                }
            }
        }

        private static void SetCell(IXLCell cell, object v)
        {
            switch (v)
            {
                case null: break;
                case string s: cell.Value = s; break;
                case bool b: cell.Value = b; break;
                case int i: cell.Value = i; break;
                case long l: cell.Value = l; break;
                case double d: cell.Value = d; break;
                case decimal m: cell.Value = m; break;
                case DateTime dt: cell.Value = dt; break;
                case Guid g: cell.Value = g.ToString(); break;
                default: cell.Value = v.ToString(); break;
            }
        }
    }
}
