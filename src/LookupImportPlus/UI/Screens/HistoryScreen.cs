using System;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using LookupImportPlus.App;
using LookupImportPlus.Domain;

namespace LookupImportPlus.UI.Screens
{
    /// <summary>
    /// 3.8 Import history. Every run with its frozen config snapshot and counters
    /// (started, configuration+version, mode, rows, written, conflicts, status).
    /// </summary>
    public sealed class HistoryScreen : ScreenControlBase
    {
        private readonly DataGridView _grid = new DataGridView();

        public HistoryScreen()
        {
            Controls.Add(_grid);
            Controls.Add(UiTheme.PageHead(I18n.T("nav.history"), I18n.T("hist.subtitle")));

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.BackgroundColor = System.Drawing.Color.White;

            _grid.Columns.Add("started", I18n.T("hist.started"));
            _grid.Columns.Add("config", I18n.T("hist.config"));
            _grid.Columns.Add("mode", I18n.T("hist.mode"));
            _grid.Columns.Add("rows", I18n.T("hist.rows"));
            _grid.Columns.Add("written", I18n.T("hist.written"));
            _grid.Columns.Add("conflicts", I18n.T("hist.conflicts"));
            _grid.Columns.Add("status", I18n.T("hist.status"));
        }

        public override void OnActivated(object parameters)
        {
            _grid.Rows.Clear();
            if (Host.Container == null) return;

            var history = Host.Container.ListHistory();
            if (history.Count == 0)
            {
                _grid.Rows.Add("—", I18n.T("hist.none"), "", "", "", "", "");
                return;
            }

            foreach (var job in history)
            {
                var cfg = job.ConfigSnapshot;
                _grid.Rows.Add(
                    FormatDate(job.StartedOn),
                    $"{cfg?.Name} · v{cfg?.Version}",
                    job.Mode.ToString(),
                    job.RowCount,
                    job.CommittedCount,
                    job.ConflictCount,
                    AppContainer.JobStatusLabel(job.Status));
            }
        }

        private static string FormatDate(string iso)
        {
            return DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)
                ? dt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
                : iso;
        }
    }
}
