using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using LookupImportPlus.App;
using LookupImportPlus.Domain;
using XrmToolBox.Extensibility;

namespace LookupImportPlus.UI.Screens
{
    /// <summary>
    /// 3.1 Job configurations (start page). Cards for each config with
    /// Export ▾ / Edit / Start import / Delete, plus New / Import Excel.
    /// </summary>
    public sealed class ConfigsScreen : ScreenControlBase
    {
        private readonly FlowLayoutPanel _list = new FlowLayoutPanel();

        public ConfigsScreen()
        {
            _list.Dock = DockStyle.Fill;
            _list.FlowDirection = FlowDirection.TopDown;
            _list.WrapContents = false;
            _list.AutoScroll = true;
            Controls.Add(_list);

            var newBtn = UiTheme.PrimaryButton(I18n.T("common.newConfig"));
            newBtn.Click += (s, e) => Host.Navigate(ScreenName.Editor);
            var importBtn = UiTheme.Button(I18n.T("common.importExcel"));
            importBtn.Click += (s, e) => Host.Navigate(ScreenName.ImportRun);

            Controls.Add(UiTheme.PageHead(I18n.T("nav.configs"), I18n.T("configs.subtitle"), importBtn, newBtn));
        }

        public override void OnActivated(object parameters)
        {
            _list.Controls.Clear();
            if (Host.Container == null) return;

            var configs = Host.Container.ListConfigs();
            if (configs.Count == 0)
            {
                var empty = UiTheme.Card();
                empty.Width = 720;
                empty.Height = 60;
                empty.Controls.Add(new Label { Text = I18n.T("configs.subtitle"), AutoSize = false, Dock = DockStyle.Fill, ForeColor = UiTheme.Muted, Font = UiTheme.Body });
                _list.Controls.Add(empty);
                return;
            }

            foreach (var c in configs)
                _list.Controls.Add(BuildCard(c));
        }

        private Panel BuildCard(JobConfiguration c)
        {
            var card = UiTheme.Card();
            card.Width = 760;
            card.Height = 92;

            var title = new Label { Text = string.IsNullOrEmpty(c.Name) ? I18n.T("common.newConfig") : c.Name, AutoSize = true, Font = UiTheme.Subheading, Location = new Point(14, 12) };
            card.Controls.Add(title);

            var version = UiTheme.Chip("v" + c.Version, UiTheme.Muted, Color.FromArgb(237, 237, 237));
            version.Location = new Point(title.Right + 8, 12);
            card.Controls.Add(version);

            if (!c.IsActive)
            {
                var draft = UiTheme.Chip(I18n.T("configs.draft"), UiTheme.Muted, Color.FromArgb(237, 237, 237));
                draft.Location = new Point(version.Right + 6, 12);
                card.Controls.Add(draft);
            }

            var meta = new Label
            {
                Text = $"{I18n.T("configs.entity")}: {(string.IsNullOrEmpty(c.TargetEntity) ? "—" : c.TargetEntity)}    " +
                       $"{I18n.T("configs.operation")}: {c.Operation}    " +
                       $"{c.Columns.Count} {I18n.T("configs.columns")} · {c.Lookups.Count} {I18n.T("configs.lookups")}",
                AutoSize = true,
                ForeColor = UiTheme.Muted,
                Font = UiTheme.Small,
                Location = new Point(14, 44)
            };
            card.Controls.Add(meta);

            var actions = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, Height = 34, AutoSize = false };

            var delBtn = UiTheme.Button(I18n.T("common.delete"));
            delBtn.Click += (s, e) => ConfirmDelete(c);
            actions.Controls.Add(delBtn);

            var startBtn = UiTheme.PrimaryButton(I18n.T("common.startImport"));
            startBtn.Enabled = !string.IsNullOrEmpty(c.TargetEntity);
            startBtn.Click += (s, e) => Host.Navigate(ScreenName.ImportRun, c.Id);
            actions.Controls.Add(startBtn);

            var editBtn = UiTheme.Button(I18n.T("common.edit"));
            editBtn.Click += (s, e) => Host.Navigate(ScreenName.Editor, c.Id);
            actions.Controls.Add(editBtn);

            var exportBtn = UiTheme.Button(I18n.T("common.export") + " ▾");
            exportBtn.Enabled = !string.IsNullOrEmpty(c.TargetEntity);
            var menu = new ContextMenuStrip();
            menu.Items.Add(I18n.T("ed.emptyTemplate"), null, (s, e) => ExportTemplate(c));
            menu.Items.Add(I18n.T("ed.exportData"), null, (s, e) => ExportData(c));
            exportBtn.Click += (s, e) => menu.Show(exportBtn, new Point(0, exportBtn.Height));
            actions.Controls.Add(exportBtn);

            card.Controls.Add(actions);
            return card;
        }

        private void ConfirmDelete(JobConfiguration c)
        {
            var msg = I18n.T("configs.deleteBody", "name", c.Name);
            if (MessageBox.Show(msg, I18n.T("configs.deleteTitle"), MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
            {
                Host.Container.DeleteConfig(c.Id);
                OnActivated(null);
            }
        }

        /// <summary>Export needs at least one selected column; the record key alone is useless.</summary>
        private bool EnsureHasColumns(JobConfiguration c)
        {
            if (c.Columns.Count > 0) return true;
            MessageBox.Show(I18n.T("ed.needColumns"));
            return false;
        }

        private void ExportTemplate(JobConfiguration c)
        {
            if (!EnsureHasColumns(c)) return;
            var path = AskSavePath(c.Name + "_Template");
            if (path == null) return;
            try
            {
                var bytes = Host.Container.Template.Build(c);
                File.WriteAllBytes(path, bytes);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void ExportData(JobConfiguration c)
        {
            if (!EnsureHasColumns(c)) return;
            var path = AskSavePath(c.Name + "_Export");
            if (path == null) return;

            byte[] bytes = null;
            Host.ExecuteWork(new WorkAsyncInfo
            {
                Message = I18n.T("preview.loading"),
                Work = (w, e) =>
                {
                    string viewXml = null;
                    if (c.ExportSource.Kind == ExportSourceKind.SavedView && !string.IsNullOrEmpty(c.ExportSource.Reference))
                    {
                        foreach (var v in Host.Container.Views.ListViews(c.TargetEntity))
                            if (v.Id == c.ExportSource.Reference) { viewXml = v.FetchXml; break; }
                    }
                    var rows = Host.Container.Export.FetchSchemaRows(c, 5000, viewXml);
                    var data = new List<IDictionary<string, object>>();
                    foreach (var r in rows) data.Add(r);
                    bytes = Host.Container.Template.Build(c, data);
                },
                PostWorkCallBack = e =>
                {
                    if (e.Error != null) { MessageBox.Show(e.Error.Message); return; }
                    try { File.WriteAllBytes(path, bytes); }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            });
        }

        private static string AskSavePath(string suggested)
        {
            using (var dlg = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", FileName = suggested + ".xlsx" })
                return dlg.ShowDialog() == DialogResult.OK ? dlg.FileName : null;
        }
    }
}
