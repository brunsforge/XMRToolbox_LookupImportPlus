using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using LookupImportPlus.UI;
using LookupImportPlus.UI.Screens;
using McTools.Xrm.Connection;
using Microsoft.Xrm.Sdk;
using XrmToolBox.Extensibility;
// Disambiguate: both System.Windows.Forms and Microsoft.Xrm.Sdk define Label.
using Label = System.Windows.Forms.Label;

namespace LookupImportPlus
{
    /// <summary>
    /// The plugin's root control. Hosts the shell (left navigation + content
    /// panel) and routes navigation between the screens. Mirrors the Code App's
    /// sidebar + main layout (src/app). The authenticated connection is provided
    /// by the XrmToolBox host via <see cref="PluginControlBase.Service"/>.
    /// </summary>
    public partial class LookupImportPlusControl : PluginControlBase, IScreenHost
    {
        private SplitContainer _split;
        private ListView _nav;
        private Panel _content;
        private Label _statusBadge;

        private readonly Dictionary<ScreenName, ScreenControlBase> _screens =
            new Dictionary<ScreenName, ScreenControlBase>();

        private ScreenControlBase _current;

        public LookupImportPlusControl()
        {
            BuildShell();
        }

        // --- IScreenHost -----------------------------------------------------

        IOrganizationService IScreenHost.Service => Service;

        public string WebApplicationUrl => ConnectionDetail?.WebApplicationUrl ?? string.Empty;

        public void Navigate(ScreenName screen, object parameters = null)
        {
            var control = GetOrCreate(screen);

            if (_current != control)
            {
                _content.SuspendLayout();
                _content.Controls.Clear();
                control.Attach(this);
                _content.Controls.Add(control);
                _content.ResumeLayout();
                _current = control;
                HighlightNav(screen);
            }

            control.OnActivated(parameters);
        }

        // --- Shell construction ---------------------------------------------

        private void BuildShell()
        {
            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel1,
                SplitterDistance = 228,
                IsSplitterFixed = false
            };

            _nav = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                HeaderStyle = ColumnHeaderStyle.None,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                BorderStyle = BorderStyle.None
            };
            _nav.Columns.Add("Screen", -2);
            AddNavItem("Job-Konfigurationen", ScreenName.Configs);
            AddNavItem("Importlaeufe", ScreenName.ImportRun);
            AddNavItem("Konfliktkorb", ScreenName.Conflicts);
            AddNavItem("Importhistorie", ScreenName.History);
            _nav.ItemSelectionChanged += OnNavSelectionChanged;

            _statusBadge = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Text = "Nicht verbunden",
                ForeColor = SystemColors.GrayText,
                BorderStyle = BorderStyle.None
            };

            var navHost = new Panel { Dock = DockStyle.Fill };
            navHost.Controls.Add(_nav);
            navHost.Controls.Add(_statusBadge);

            _content = new Panel { Dock = DockStyle.Fill };

            _split.Panel1.Controls.Add(navHost);
            _split.Panel2.Controls.Add(_content);

            Controls.Add(_split);

            // Land on the start page even before a connection is established;
            // the placeholder screens do not require the Dataverse service.
            _nav.Items[0].Selected = true;
        }

        private void AddNavItem(string text, ScreenName screen)
        {
            _nav.Items.Add(new ListViewItem(text) { Tag = screen });
        }

        private void OnNavSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (e.IsSelected && e.Item.Tag is ScreenName screen)
            {
                Navigate(screen);
            }
        }

        private void HighlightNav(ScreenName screen)
        {
            foreach (ListViewItem item in _nav.Items)
            {
                if (item.Tag is ScreenName s && s == screen && !item.Selected)
                {
                    item.Selected = true;
                }
            }
        }

        private ScreenControlBase GetOrCreate(ScreenName screen)
        {
            if (_screens.TryGetValue(screen, out var existing))
            {
                return existing;
            }

            ScreenControlBase control;
            switch (screen)
            {
                case ScreenName.Configs: control = new ConfigsScreen(); break;
                case ScreenName.Editor: control = new EditorScreen(); break;
                case ScreenName.ImportRun: control = new ImportRunScreen(); break;
                case ScreenName.Conflicts: control = new ConflictsScreen(); break;
                case ScreenName.Resolve: control = new ResolveScreen(); break;
                case ScreenName.History: control = new HistoryScreen(); break;
                default: throw new ArgumentOutOfRangeException(nameof(screen), screen, null);
            }

            _screens[screen] = control;
            return control;
        }

        // --- Host lifecycle --------------------------------------------------

        /// <summary>
        /// Called by the host when the (re)connection is established. We land on
        /// the Job-Konfigurationen start page.
        /// </summary>
        public override void UpdateConnection(
            IOrganizationService newService,
            ConnectionDetail detail,
            string actionName = "",
            object parameter = null)
        {
            base.UpdateConnection(newService, detail, actionName, parameter);

            _statusBadge.Text = detail != null
                ? $"Verbunden mit {detail.ConnectionName}"
                : "Nicht verbunden";
            _statusBadge.ForeColor = detail != null ? Color.SeaGreen : SystemColors.GrayText;

            Navigate(ScreenName.Configs);
        }
    }
}
