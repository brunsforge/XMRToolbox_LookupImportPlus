using System;
using System.Drawing;
using System.Windows.Forms;
using LookupImportPlus.App;
using LookupImportPlus.Data;
using LookupImportPlus.UI;
using LookupImportPlus.UI.Screens;
using McTools.Xrm.Connection;
using Microsoft.Xrm.Sdk;
using XrmToolBox.Extensibility;
using Label = System.Windows.Forms.Label;

namespace LookupImportPlus
{
    /// <summary>
    /// The plugin's root control. Hosts the shell (left navigation + content
    /// panel), builds the composition root when a connection arrives, and routes
    /// navigation between the screens (mirrors the Code App's Shell + AppContext).
    /// </summary>
    public class LookupImportPlusControl : PluginControlBase, IScreenHost
    {
        private SplitContainer _split;
        private ListView _nav;
        private Panel _content;
        private Label _statusBadge;
        private NotifyIcon _notify;

        private ScreenControlBase _current;

        public new AppContainer Container { get; private set; }

        public LookupImportPlusControl()
        {
            BuildShell();
        }

        // ── IScreenHost ──────────────────────────────────────────
        public void Navigate(ScreenName screen, object parameters = null)
        {
            var control = CreateScreen(screen);
            control.Attach(this);

            _content.SuspendLayout();
            var previous = _current;
            _content.Controls.Clear();
            _content.Controls.Add(control);
            _content.ResumeLayout();
            previous?.Dispose();
            _current = control;

            HighlightNav(screen);
            control.OnActivated(parameters);
        }

        public void ExecuteWork(WorkAsyncInfo info) => WorkAsync(info);

        public void Notify(string title, string body)
        {
            try
            {
                _notify.Visible = true;
                _notify.ShowBalloonTip(4000, title, body, ToolTipIcon.Info);
            }
            catch { /* balloon tips can fail silently on some hosts */ }
        }

        // ── Shell construction ───────────────────────────────────
        private void BuildShell()
        {
            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel1,
                SplitterDistance = 228
            };

            _nav = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                HeaderStyle = ColumnHeaderStyle.None,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10F)
            };
            _nav.Columns.Add("Screen", -2);
            AddNavItem(I18n.T("nav.configs"), ScreenName.Configs);
            AddNavItem(I18n.T("nav.importruns"), ScreenName.ImportRun);
            AddNavItem(I18n.T("nav.conflicts"), ScreenName.Conflicts);
            AddNavItem(I18n.T("nav.history"), ScreenName.History);
            _nav.ItemSelectionChanged += OnNavSelectionChanged;

            _statusBadge = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Text = I18n.T("shell.offline"),
                ForeColor = UiTheme.Muted
            };

            var navHost = new Panel { Dock = DockStyle.Fill };
            navHost.Controls.Add(_nav);
            navHost.Controls.Add(_statusBadge);

            _content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };

            _split.Panel1.Controls.Add(navHost);
            _split.Panel2.Controls.Add(_content);
            Controls.Add(_split);

            _notify = new NotifyIcon { Icon = SystemIcons.Information, Text = "LookupImportPlus" };

            ShowNotConnected();
        }

        private void AddNavItem(string text, ScreenName screen)
            => _nav.Items.Add(new ListViewItem(text) { Tag = screen });

        private void OnNavSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (e.IsSelected && e.Item.Tag is ScreenName screen)
                Navigate(screen);
        }

        private void HighlightNav(ScreenName screen)
        {
            foreach (ListViewItem item in _nav.Items)
                if (item.Tag is ScreenName s && s == screen && !item.Selected)
                    item.Selected = true;
        }

        private ScreenControlBase CreateScreen(ScreenName screen)
        {
            switch (screen)
            {
                case ScreenName.Configs: return new ConfigsScreen();
                case ScreenName.Editor: return new EditorScreen();
                case ScreenName.ImportRun: return new ImportRunScreen();
                case ScreenName.Conflicts: return new ConflictsScreen();
                case ScreenName.Resolve: return new ResolveScreen();
                case ScreenName.History: return new HistoryScreen();
                default: throw new ArgumentOutOfRangeException(nameof(screen));
            }
        }

        private void ShowNotConnected()
        {
            _content.Controls.Clear();
            _content.Controls.Add(new Label
            {
                Text = I18n.T("shell.offline"),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = UiTheme.Muted,
                Font = UiTheme.Body
            });
        }

        // ── Host lifecycle ───────────────────────────────────────
        public override void UpdateConnection(
            IOrganizationService newService,
            ConnectionDetail detail,
            string actionName = "",
            object parameter = null)
        {
            base.UpdateConnection(newService, detail, actionName, parameter);
            if (newService == null) { ShowNotConnected(); return; }

            var ctx = new DataverseContext(newService, detail?.WebApplicationUrl);
            Container = new AppContainer(ctx, Paths.SettingsPath);

            _statusBadge.Text = detail != null
                ? I18n.T("shell.live") + " · " + detail.ConnectionName
                : I18n.T("shell.offline");
            _statusBadge.ForeColor = UiTheme.Success;

            if (_nav.Items.Count > 0) _nav.Items[0].Selected = true;
            Navigate(ScreenName.Configs);
        }
    }
}
