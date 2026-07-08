using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using LookupImportPlus.App;
using LookupImportPlus.Domain;

namespace LookupImportPlus.Services.Excel
{
    public sealed class ParsedRow
    {
        /// <summary>1-based data row number (excludes the header row).</summary>
        public int RowNumber { get; set; }

        /// <summary>Cell values keyed by column header.</summary>
        public Dictionary<string, object> Values { get; set; } = new Dictionary<string, object>();
    }

    public sealed class ParsedWorkbook
    {
        public TemplateManifest Manifest { get; set; }

        /// <summary>True when a manifest was present and its hash verified.</summary>
        public bool ManifestValid { get; set; }

        public List<string> Headers { get; set; } = new List<string>();
        public List<ParsedRow> Rows { get; set; } = new List<ParsedRow>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// Reads an uploaded XLSX back into headers + rows and the embedded manifest
    /// (port of ExcelParserService.ts). The importer uses the manifest, not the
    /// visible headers alone, to know the mapping/target/version and to detect a
    /// tampered/outdated template.
    /// </summary>
    public sealed class ExcelParserService
    {
        public ParsedWorkbook Parse(byte[] data)
        {
            var result = new ParsedWorkbook();
            using (var ms = new MemoryStream(data))
            using (var wb = new XLWorkbook(ms))
            {
                // ── manifest ──
                if (wb.TryGetWorksheet(TemplateConstants.ManifestSheet, out var manifestSheet))
                {
                    var raw = manifestSheet.Cell("A2").GetString();
                    try
                    {
                        result.Manifest = Json.Deserialize<TemplateManifest>(raw);
                        result.ManifestValid = ManifestHash.HashManifest(result.Manifest) == result.Manifest.Hash;
                        if (!result.ManifestValid)
                            result.Warnings.Add(I18n.T("parse.manifestTampered"));
                    }
                    catch
                    {
                        result.Warnings.Add(I18n.T("parse.manifestUnreadable"));
                    }
                }
                else
                {
                    result.Warnings.Add(I18n.T("parse.noManifest"));
                }

                // ── data sheet ──
                IXLWorksheet ws = null;
                if (!wb.TryGetWorksheet(TemplateConstants.DataSheet, out ws))
                    ws = wb.Worksheets.FirstOrDefault(w => w.Name != TemplateConstants.ManifestSheet);

                if (ws == null)
                {
                    result.Warnings.Add(I18n.T("parse.noDataSheet"));
                    return result;
                }

                var colIndexToHeader = new Dictionary<int, string>();
                foreach (var cell in ws.Row(1).CellsUsed())
                {
                    var h = (NormalizeCell(cell)?.ToString() ?? "").Trim();
                    if (h.Length > 0)
                    {
                        result.Headers.Add(h);
                        colIndexToHeader[cell.Address.ColumnNumber] = h;
                    }
                }

                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
                for (int r = 2; r <= lastRow; r++)
                {
                    var values = new Dictionary<string, object>();
                    bool hasValue = false;
                    foreach (var kv in colIndexToHeader)
                    {
                        var v = NormalizeCell(ws.Cell(r, kv.Key));
                        values[kv.Value] = v;
                        if (v != null && !(v is string s && s.Length == 0)) hasValue = true;
                    }
                    if (hasValue) result.Rows.Add(new ParsedRow { RowNumber = r - 1, Values = values });
                }

                // ── manifest vs actual headers ──
                if (result.Manifest != null)
                {
                    var missing = result.Manifest.Columns
                        .Select(c => c.Header)
                        .Where(h => !result.Headers.Contains(h))
                        .ToList();
                    if (missing.Count > 0)
                        result.Warnings.Add(I18n.T("parse.missingColumns", "cols", string.Join(", ", missing)));
                }
            }
            return result;
        }

        private static object NormalizeCell(IXLCell cell)
        {
            var v = cell.Value;
            if (v.IsBlank) return null;
            if (v.IsDateTime) return v.GetDateTime().ToString("o", CultureInfo.InvariantCulture);
            if (v.IsBoolean) return v.GetBoolean();
            if (v.IsNumber) return v.GetNumber();
            if (v.IsError) return null;
            return v.GetText();
        }
    }
}
