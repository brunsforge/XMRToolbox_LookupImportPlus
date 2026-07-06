using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using LookupImportPlus.App;
using LookupImportPlus.Domain;
using LookupImportPlus.Services.Excel;
using Microsoft.Xrm.Sdk;
using XrmToolBox.Extensibility;
using Label = System.Windows.Forms.Label;

namespace LookupImportPlus.UI
{
    /// <summary>
    /// 3.3 Data preview (modal). Toggles CRM columns (raw Dataverse fields) vs
    /// schema columns (the Excel layout with generated technical columns); row
    /// count 10/25/50.
    /// </summary>
    public sealed class DataPreviewModal : Form
    {
        private readonly IScreenHost _host;
        private readonly JobConfiguration _config;

        private readonly RadioButton _crm = new RadioButton { Text = I18n.T("preview.crmCols"), AutoSize = true, Checked = true };
        private readonly RadioButton _schema = new RadioButton { Text = I18n.T("preview.schemaCols"), AutoSize = true };
        private readonly ComboBox _count = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _hint = new Label { Dock = DockStyle.Bottom, Height = 34, ForeColor = UiTheme.Muted, Font = UiTheme.Small, Text = I18n.T("preview.hint") };

        private List<Entity> _records = new List<Entity>();

        public DataPreviewModal(IScreenHost host, JobConfiguration config)
        {
            _host = host;
            _config = config;

            Text = I18n.T("preview.title");
            Width = 900;
            Height = 560;
            StartPosition = FormStartPosition.CenterParent;

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.RowHeadersVisible = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;

            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, WrapContents = false };
            _crm.Margin = new Padding(4, 8, 8, 0);
            _schema.Margin = new Padding(4, 8, 8, 0);
            _crm.CheckedChanged += (s, e) => Render();
            _count.Items.AddRange(new object[] { 10, 25, 50 });
            _count.SelectedIndex = 0;
            _count.Margin = new Padding(12, 6, 4, 0);
            _count.SelectedIndexChanged += (s, e) => Fetch();
            bar.Controls.Add(_crm);
            bar.Controls.Add(_schema);
            bar.Controls.Add(new Label { Text = I18n.T("preview.rows"), AutoSize = true, Padding = new Padding(8, 8, 0, 0) });
            bar.Controls.Add(_count);

            var close = UiTheme.Button(I18n.T("common.close"));
            close.Dock = DockStyle.Bottom;
            close.Click += (s, e) => Close();

            Controls.Add(_grid);
            Controls.Add(bar);
            Controls.Add(_hint);
            Controls.Add(close);

            Load += (s, e) => Fetch();
        }

        private int Count => _count.SelectedItem is int n ? n : 10;

        private void Fetch()
        {
            _host.ExecuteWork(new WorkAsyncInfo
            {
                Message = I18n.T("preview.loading"),
                Work = (w, e) =>
                {
                    string viewXml = null;
                    if (_config.ExportSource.Kind == ExportSourceKind.SavedView && !string.IsNullOrEmpty(_config.ExportSource.Reference))
                        viewXml = _host.Container.Views.ListViews(_config.TargetEntity).FirstOrDefault(v => v.Id == _config.ExportSource.Reference)?.FetchXml;
                    e.Result = _host.Container.Export.FetchRecords(_config, Count, viewXml);
                },
                PostWorkCallBack = e =>
                {
                    if (e.Error != null) { MessageBox.Show(e.Error.Message); return; }
                    _records = (List<Entity>)e.Result;
                    Render();
                }
            });
        }

        private void Render()
        {
            _grid.Columns.Clear();
            _grid.Rows.Clear();

            if (_records.Count == 0)
            {
                _grid.Columns.Add("c", I18n.T("preview.empty"));
                return;
            }

            if (_crm.Checked)
            {
                var cols = _host.Container.Export.CrmColumns(_config);
                foreach (var c in cols) _grid.Columns.Add(c, c);
                foreach (var rec in _records)
                {
                    var row = _host.Container.Export.ToCrmRow(_config, rec);
                    _grid.Rows.Add(cols.Select(c => row.TryGetValue(c, out var v) ? v : "").ToArray());
                }
            }
            else
            {
                var headers = TemplateColumns.Build(_config).Select(c => c.Role == TemplateColumnRole.RecordId ? TemplateConstants.RecordIdColumn : c.Header).Distinct().ToList();
                foreach (var h in headers) _grid.Columns.Add(h, h);
                foreach (var schemaRow in _host.Container.Export.ToSchemaRows(_config, _records))
                    _grid.Rows.Add(headers.Select(h => schemaRow.TryGetValue(h, out var v) ? v : "").ToArray());
            }
        }
    }
}
