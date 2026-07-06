using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using LookupImportPlus.App;
using LookupImportPlus.Domain;
using LookupImportPlus.Services.Excel;
using XrmToolBox.Extensibility;

namespace LookupImportPlus.UI.Screens
{
    /// <summary>
    /// 3.5 Import run. Upload XLSX → config check (schema drift) → dry run (each
    /// row classified) → status tiles → write mode Strict/Partial → commit
    /// (ExecuteMultiple).
    /// </summary>
    public sealed class ImportRunScreen : ScreenControlBase
    {
        private JobConfiguration _config;

        private readonly Label _info = new Label();
        private readonly FlowLayoutPanel _issues = new FlowLayoutPanel();
        private readonly FlowLayoutPanel _tiles = new FlowLayoutPanel();
        private readonly DataGridView _grid = new DataGridView();
        private readonly RadioButton _strict = new RadioButton { Text = "Strict", AutoSize = true };
        private readonly RadioButton _partial = new RadioButton { Text = "Partial", AutoSize = true };
        private readonly Button _commit = UiTheme.PrimaryButton("");
        private readonly Button _openBasket = UiTheme.Button("");

        public ImportRunScreen()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.BackgroundColor = Color.White;
            _grid.Columns.Add("row", I18n.T("run.colRow"));
            _grid.Columns.Add("resolution", I18n.T("run.colResolution"));
            _grid.Columns.Add("status", I18n.T("run.colStatus"));
            _grid.Columns[0].FillWeight = 20;
            _grid.Columns[1].FillWeight = 55;
            _grid.Columns[2].FillWeight = 25;
            _grid.CellFormatting += GridCellFormatting;
            Controls.Add(_grid);

            _tiles.Dock = DockStyle.Top;
            _tiles.Height = 60;
            _tiles.FlowDirection = FlowDirection.LeftToRight;
            Controls.Add(_tiles);

            _issues.Dock = DockStyle.Top;
            _issues.AutoSize = true;
            _issues.FlowDirection = FlowDirection.TopDown;
            _issues.WrapContents = false;
            _issues.Visible = false;
            Controls.Add(_issues);

            Controls.Add(BuildToolbar());

            _info.Dock = DockStyle.Top;
            _info.Height = 24;
            _info.Font = UiTheme.Small;
            _info.ForeColor = UiTheme.Muted;
            Controls.Add(_info);

            Controls.Add(UiTheme.PageHead(I18n.T("run.title"), null));
        }

        private Panel BuildToolbar()
        {
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };

            var upload = UiTheme.Button(I18n.T("run.uploadXlsx"));
            upload.Click += (s, e) => Upload();
            bar.Controls.Add(upload);

            bar.Controls.Add(new Label { Text = I18n.T("run.writeMode"), AutoSize = true, Padding = new Padding(12, 6, 2, 0), Font = UiTheme.Body });
            _strict.CheckedChanged += (s, e) => { if (_strict.Checked) SetMode(ImportMode.Strict); };
            _partial.CheckedChanged += (s, e) => { if (_partial.Checked) SetMode(ImportMode.Partial); };
            _strict.Margin = new Padding(4, 8, 4, 0);
            _partial.Margin = new Padding(4, 8, 4, 0);
            bar.Controls.Add(_strict);
            bar.Controls.Add(_partial);

            _openBasket.Text = I18n.T("run.openBasket");
            _openBasket.Visible = false;
            _openBasket.Click += (s, e) => Host.Navigate(ScreenName.Conflicts);
            bar.Controls.Add(_openBasket);

            _commit.Text = I18n.T("run.commit");
            _commit.Enabled = false;
            _commit.Click += (s, e) => Commit();
            bar.Controls.Add(_commit);

            return bar;
        }

        public override void OnActivated(object parameters)
        {
            if (Host.Container == null) return;

            if (parameters is string configId)
                _config = Host.Container.GetConfig(configId);

            var job = Host.Container.ActiveJob;
            if (_config == null && job != null) _config = job.ConfigSnapshot;

            _strict.Checked = (job?.Mode ?? _config?.DefaultMode ?? ImportMode.Strict) == ImportMode.Strict;
            _partial.Checked = !_strict.Checked;

            UpdateInfo();
            if (job != null) RenderJob(job);
            else { _grid.Rows.Clear(); _grid.Rows.Add("", I18n.T("run.empty"), ""); ClearTiles(); }
        }

        private void UpdateInfo()
        {
            _info.Text = _config != null
                ? $"{I18n.T("run.targetEntity")}: {_config.TargetEntity}  ·  {I18n.T("run.snapshot")}: {_config.Name} v{_config.Version}"
                : I18n.T("common.importExcel");
        }

        private void Upload()
        {
            if (Host.Container == null) return;
            string path;
            using (var dlg = new OpenFileDialog { Filter = "Excel (*.xlsx)|*.xlsx" })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                path = dlg.FileName;
            }

            ParsedWorkbook parsed = null;
            ConfigValidationResultHolder validation = null;
            ImportJob job = null;

            Host.ExecuteWork(new WorkAsyncInfo
            {
                Message = I18n.T("run.busyRead"),
                Work = (w, e) =>
                {
                    var worker = (BackgroundWorker)w;
                    var bytes = File.ReadAllBytes(path);
                    parsed = Host.Container.Parser.Parse(bytes);

                    // Resolve the config: preselected, else from the manifest.
                    if (_config == null && parsed.Manifest != null)
                        _config = Host.Container.GetConfig(parsed.Manifest.ConfigId);
                    if (_config == null)
                        throw new InvalidOperationException("Keine passende Konfiguration gefunden (Manifest configId unbekannt).");

                    var result = Host.Container.Validation.Validate(_config);
                    validation = new ConfigValidationResultHolder { Result = result };
                    if (result.HasErrors) return; // block; do not dry-run

                    worker.ReportProgress(0, I18n.T("run.busyDry"));
                    job = Host.Container.Runner.DryRun(_config, parsed.Rows, CurrentMode(), null,
                        (done, total) => worker.ReportProgress(total == 0 ? 0 : done * 100 / total, I18n.T("run.busyDry")));
                    job.FileName = Path.GetFileName(path);
                },
                ProgressChanged = e => { /* message handled by host */ },
                PostWorkCallBack = e =>
                {
                    if (e.Error != null) { MessageBox.Show(e.Error.Message); return; }
                    UpdateInfo();
                    RenderIssues(validation?.Result, parsed);
                    if (validation != null && validation.Result.HasErrors) { _grid.Rows.Clear(); ClearTiles(); _commit.Enabled = false; return; }
                    Host.Container.ActiveJob = job;
                    RenderJob(job);
                },
                IsCancelable = false
            });
        }

        private void RenderIssues(ConfigValidationResult result, ParsedWorkbook parsed)
        {
            _issues.Controls.Clear();
            _issues.Visible = false;
            if (result == null) return;

            var lines = new List<(Color, string)>();
            foreach (var w in parsed?.Warnings ?? Enumerable.Empty<string>())
                lines.Add((UiTheme.Warning, "⚠ " + w));
            foreach (var issue in result.Issues)
            {
                var color = issue.Severity == IssueSeverity.Error ? UiTheme.Error
                    : issue.Severity == IssueSeverity.Warning ? UiTheme.Warning : UiTheme.Muted;
                lines.Add((color, AppContainer.IssueMessage(issue)));
            }
            if (result.HasErrors)
                lines.Insert(0, (UiTheme.Error, I18n.T("val.blocked")));

            foreach (var (color, text) in lines)
                _issues.Controls.Add(new Label { Text = text, AutoSize = true, ForeColor = color, Font = UiTheme.Small, Margin = new Padding(2) });

            _issues.Visible = _issues.Controls.Count > 0;
        }

        private void RenderJob(ImportJob job)
        {
            RenderTiles(job);
            _grid.Rows.Clear();
            foreach (var row in job.Rows)
            {
                var idx = _grid.Rows.Add(row.RowNumber, ResolutionSummary(row), AppContainer.StatusLabel(row.Status));
                _grid.Rows[idx].Tag = row.Status;
            }
            _openBasket.Visible = job.ConflictCount > 0;
            _openBasket.Text = $"{job.ConflictCount} {I18n.T("run.openBasket")}";
            UpdateCommitButton(job);
        }

        private string ResolutionSummary(ImportRow row)
        {
            if (row.Lookups.Count == 0)
                return string.Join("; ", row.Messages);
            return string.Join("   ", row.Lookups.Select(l =>
            {
                switch (l.Status)
                {
                    case LookupResolutionStatus.Resolved: return $"{l.LookupAttribute}: ✓";
                    case LookupResolutionStatus.Ambiguous: return $"{l.LookupAttribute}: {l.Candidates?.Count ?? 0} {I18n.T("run.candidates")}";
                    case LookupResolutionStatus.NotFound: return $"{l.LookupAttribute}: {I18n.T("run.noMatch")}";
                    case LookupResolutionStatus.WrongTargetType: return $"{l.LookupAttribute}: !";
                    default: return $"{l.LookupAttribute}: …";
                }
            }));
        }

        private void RenderTiles(ImportJob job)
        {
            _tiles.Controls.Clear();
            _tiles.Controls.Add(Tile(I18n.T("run.ready"), job.ReadyCount, UiTheme.Success));
            _tiles.Controls.Add(Tile(I18n.T("run.conflicts"), job.ConflictCount, UiTheme.Warning));
            _tiles.Controls.Add(Tile(I18n.T("run.errors"), job.ErrorCount, UiTheme.Error));
            _tiles.Controls.Add(Tile(I18n.T("run.totalRows"), job.RowCount, UiTheme.Muted));
        }

        private void ClearTiles() => _tiles.Controls.Clear();

        private Panel Tile(string label, int value, Color color)
        {
            var p = new Panel { Width = 150, Height = 50, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 0, 8, 0) };
            p.Controls.Add(new Label { Text = value.ToString(), AutoSize = true, Font = new Font("Segoe UI", 15F, FontStyle.Bold), ForeColor = color, Location = new Point(8, 2) });
            p.Controls.Add(new Label { Text = label, AutoSize = true, Font = UiTheme.Small, ForeColor = UiTheme.Muted, Location = new Point(8, 30) });
            return p;
        }

        private void GridCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.ColumnIndex == 2 && _grid.Rows[e.RowIndex].Tag is RowStatus status)
            {
                var (fore, back) = UiTheme.StatusColors(status);
                e.CellStyle.ForeColor = fore;
                e.CellStyle.BackColor = back;
            }
        }

        private ImportMode CurrentMode() => _strict.Checked ? ImportMode.Strict : ImportMode.Partial;

        private void SetMode(ImportMode mode)
        {
            if (Host.Container?.ActiveJob != null)
            {
                Host.Container.ActiveJob.Mode = mode;
                UpdateCommitButton(Host.Container.ActiveJob);
            }
        }

        private void UpdateCommitButton(ImportJob job)
        {
            var blocking = job.Rows.Any(r => StatusSets.Blocking.Contains(r.Status));
            if (job.Mode == ImportMode.Strict && blocking)
            {
                _commit.Enabled = false;
                _commit.Text = I18n.T("run.commitBlocked");
            }
            else
            {
                var writable = job.Rows.Count(r => StatusSets.Writable.Contains(r.Status));
                _commit.Enabled = writable > 0;
                _commit.Text = $"{I18n.T("run.commit")} ({writable})";
            }
        }

        private void Commit()
        {
            var job = Host.Container?.ActiveJob;
            if (job == null) return;
            job.Mode = CurrentMode();

            Host.ExecuteWork(new WorkAsyncInfo
            {
                Message = I18n.T("run.busyWrite"),
                Work = (w, e) =>
                {
                    var worker = (BackgroundWorker)w;
                    Host.Container.Runner.Commit(job,
                        (done, total) => worker.ReportProgress(total == 0 ? 0 : done * 100 / total, I18n.T("run.busyWrite")));
                },
                PostWorkCallBack = e =>
                {
                    if (e.Error != null) { MessageBox.Show(e.Error.Message); return; }
                    Host.Container.AddHistory(job);
                    Host.Notify(I18n.T("notif.doneTitle"),
                        I18n.T("notif.doneBody", new Dictionary<string, object>
                        {
                            ["written"] = job.CommittedCount,
                            ["errors"] = job.Rows.Count(r => r.Status == RowStatus.CommitFailed),
                            ["conflicts"] = job.ConflictCount
                        }));
                    RenderJob(job);
                }
            });
        }

        private sealed class ConfigValidationResultHolder { public ConfigValidationResult Result; }
    }
}
