using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LookupImportPlus.App;
using LookupImportPlus.Domain;

namespace LookupImportPlus.UI.Screens
{
    /// <summary>
    /// 3.6 Conflict basket. Grouped by source value; per group the target field,
    /// affected rows, candidate count, status, and Resolve → / Edit →. Nothing is
    /// guessed automatically.
    /// </summary>
    public sealed class ConflictsScreen : ScreenControlBase
    {
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _empty = new Label();
        private List<Group> _groups = new List<Group>();

        public ConflictsScreen()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.RowHeadersVisible = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.BackgroundColor = Color.White;
            _grid.Columns.Add("source", I18n.T("conf.colSource"));
            _grid.Columns.Add("field", I18n.T("conf.colField"));
            _grid.Columns.Add("affected", I18n.T("conf.colAffected"));
            _grid.Columns.Add("candidates", I18n.T("conf.colCandidates"));
            var action = new DataGridViewButtonColumn { Name = "action", HeaderText = "", Text = "", UseColumnTextForButtonValue = false };
            _grid.Columns.Add(action);
            _grid.CellClick += GridCellClick;
            Controls.Add(_grid);

            _empty.Dock = DockStyle.Fill;
            _empty.TextAlign = ContentAlignment.MiddleCenter;
            _empty.ForeColor = UiTheme.Muted;
            _empty.Font = UiTheme.Body;
            _empty.Visible = false;
            Controls.Add(_empty);

            var back = UiTheme.Button(I18n.T("conf.backToRun"));
            back.Click += (s, e) => Host.Navigate(ScreenName.ImportRun);
            Controls.Add(UiTheme.PageHead(I18n.T("nav.conflicts"), I18n.T("conf.subtitle"), back));

            var audit = new Label { Dock = DockStyle.Bottom, Height = 22, Text = I18n.T("conf.audit"), Font = UiTheme.Small, ForeColor = UiTheme.Muted };
            Controls.Add(audit);
        }

        public override void OnActivated(object parameters)
        {
            var job = Host.Container?.ActiveJob;
            _grid.Rows.Clear();
            _groups = BuildGroups(job);

            if (job == null)
            {
                ShowEmpty(I18n.T("conf.noRun"));
                return;
            }
            if (_groups.Count == 0)
            {
                ShowEmpty(I18n.T("conf.allResolvedHint"));
                return;
            }

            _empty.Visible = false;
            _grid.Visible = true;
            foreach (var g in _groups)
            {
                var idx = _grid.Rows.Add(
                    g.SourceValue ?? "—",
                    g.LookupAttribute,
                    $"{g.Rows.Count} {I18n.T("conf.rows")}",
                    g.HasCandidates ? $"{g.Candidates}" : I18n.T("conf.hits0"));
                _grid.Rows[idx].Cells["action"].Value = g.HasCandidates ? I18n.T("conf.resolve") : I18n.T("conf.editRow");
            }
        }

        private void ShowEmpty(string text)
        {
            _grid.Visible = false;
            _empty.Text = text;
            _empty.Visible = true;
        }

        private void GridCellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != _grid.Columns["action"].Index) return;
            if (e.RowIndex >= _groups.Count) return;
            var g = _groups[e.RowIndex];
            Host.Navigate(ScreenName.Resolve, new ConflictKey { LookupAttribute = g.LookupAttribute, SourceValue = g.SourceValue });
        }

        private static List<Group> BuildGroups(ImportJob job)
        {
            var groups = new Dictionary<string, Group>();
            if (job == null) return new List<Group>();

            foreach (var row in job.Rows)
            {
                foreach (var l in row.Lookups)
                {
                    if (l.Status != LookupResolutionStatus.Ambiguous && l.Status != LookupResolutionStatus.NotFound) continue;
                    var key = l.LookupAttribute + "|" + (l.SourceValue ?? "");
                    if (!groups.TryGetValue(key, out var g))
                    {
                        g = new Group { LookupAttribute = l.LookupAttribute, SourceValue = l.SourceValue };
                        groups[key] = g;
                    }
                    g.Rows.Add(row.RowNumber);
                    if (l.Status == LookupResolutionStatus.Ambiguous)
                    {
                        g.HasCandidates = true;
                        g.Candidates = System.Math.Max(g.Candidates, l.Candidates?.Count ?? 0);
                    }
                }
            }
            return groups.Values.ToList();
        }

        private sealed class Group
        {
            public string LookupAttribute;
            public string SourceValue;
            public HashSet<int> Rows = new HashSet<int>();
            public bool HasCandidates;
            public int Candidates;
        }
    }
}
