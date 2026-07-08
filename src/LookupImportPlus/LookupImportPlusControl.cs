using System;
using System.Drawing;
using System.IO;
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
        private ComboBox _langCombo;
        private NotifyIcon _notify;

        private ScreenControlBase _current;
        private ScreenName? _currentScreen;
        private object _currentParams;
        private bool _connected;
        private string _connName;

        public new AppContainer Container { get; private set; }

        public LookupImportPlusControl()
        {
            LoadLanguagePreference();
            BuildShell();
        }

        // ── Language ─────────────────────────────────────────────
        private static string LangFilePath =>
            Path.Combine(Paths.SettingsPath, "LookupImportPlus", "language.txt");

        private static void LoadLanguagePreference()
        {
            try
            {
                if (!File.Exists(LangFilePath)) return; // no saved choice → keep culture default
                var v = File.ReadAllText(LangFilePath).Trim().ToLowerInvariant();
                if (v == "de") I18n.Current = Lang.De;
                else if (v == "en") I18n.Current = Lang.En;
            }
            catch { /* fall back to culture default */ }
        }

        private static void SaveLanguagePreference()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LangFilePath));
                File.WriteAllText(LangFilePath, I18n.Current == Lang.De ? "de" : "en");
            }
            catch { /* non-fatal */ }
        }

        private sealed class LangItem
        {
            public Lang Lang;
            private readonly string _text;
            public LangItem(Lang lang, string text) { Lang = lang; _text = text; }
            public override string ToString() => _text;
        }

        private void OnLangChanged(object sender, EventArgs e)
        {
            if (_langCombo.SelectedItem is LangItem li && li.Lang != I18n.Current)
            {
                I18n.Current = li.Lang;
                SaveLanguagePreference();
                ApplyLanguage();
            }
        }

        /// <summary>Re-render nav, status and the current screen in the new language.</summary>
        private void ApplyLanguage()
        {
            foreach (ListViewItem item in _nav.Items)
                if (item.Tag is ScreenName s) item.Text = NavText(s);

            _statusBadge.Text = _connected
                ? I18n.T("shell.live") + (string.IsNullOrEmpty(_connName) ? "" : " · " + _connName)
                : I18n.T("shell.offline");

            if (_connected && _currentScreen.HasValue) Navigate(_currentScreen.Value, _currentParams);
            else ShowNotConnected();
        }

        private static string NavText(ScreenName s)
        {
            switch (s)
            {
                case ScreenName.Configs: return I18n.T("nav.configs");
                case ScreenName.ImportRun: return I18n.T("nav.importruns");
                case ScreenName.Conflicts: return I18n.T("nav.conflicts");
                case ScreenName.History: return I18n.T("nav.history");
                default: return s.ToString();
            }
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
            _currentScreen = screen;
            _currentParams = parameters;

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

            var langBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 32,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(6, 4, 0, 0)
            };
            langBar.Controls.Add(new Label { Text = I18n.T("shell.language") + ":", AutoSize = true, Padding = new Padding(0, 4, 4, 0), ForeColor = UiTheme.Muted });
            _langCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
            _langCombo.Items.Add(new LangItem(Lang.En, "English"));
            _langCombo.Items.Add(new LangItem(Lang.De, "Deutsch"));
            _langCombo.SelectedIndex = I18n.Current == Lang.De ? 1 : 0;
            _langCombo.SelectedIndexChanged += OnLangChanged;
            langBar.Controls.Add(_langCombo);

            var navHost = new Panel { Dock = DockStyle.Fill };
            navHost.Controls.Add(_nav);
            navHost.Controls.Add(langBar);
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
            if (newService == null) { _connected = false; _connName = null; ShowNotConnected(); return; }

            var ctx = new DataverseContext(newService, detail?.WebApplicationUrl);
            Container = new AppContainer(ctx, Paths.SettingsPath);

            _connected = true;
            _connName = detail?.ConnectionName;
            _statusBadge.Text = detail != null
                ? I18n.T("shell.live") + " · " + detail.ConnectionName
                : I18n.T("shell.offline");
            _statusBadge.ForeColor = UiTheme.Success;

            if (_nav.Items.Count > 0) _nav.Items[0].Selected = true;
            Navigate(ScreenName.Configs);
        }
    }
}
