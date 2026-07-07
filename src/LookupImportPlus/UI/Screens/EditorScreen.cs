using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LookupImportPlus.App;
using LookupImportPlus.Domain;
using LookupImportPlus.Services;
using XrmToolBox.Extensibility;

namespace LookupImportPlus.UI.Screens
{
    /// <summary>
    /// 3.2 Configuration editor (wizard). Four tabs; tabs 2–4 are locked until a
    /// target entity is chosen. Tab 4 (lookups) is the core: per lookup column a
    /// card with GUID / business-key / search-field resolution.
    /// </summary>
    public sealed class EditorScreen : ScreenControlBase
    {
        private JobConfiguration _config;
        private EntityMetadata _entity;

        private readonly TabControl _tabs = new TabControl { Dock = DockStyle.Fill };
        private readonly TabPage _tab1 = new TabPage();
        private readonly TabPage _tab2 = new TabPage();
        private readonly TabPage _tab3 = new TabPage();
        private readonly TabPage _tab4 = new TabPage();

        // Tab 1
        private readonly ComboBox _entityCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360, Sorted = false };
        private readonly Label _entityInfo = new Label { AutoSize = true, ForeColor = UiTheme.Muted, Font = UiTheme.Small };
        private readonly RadioButton _srcEntity = new RadioButton { AutoSize = true, Checked = true };
        private readonly RadioButton _srcView = new RadioButton { AutoSize = true };
        private readonly ComboBox _viewCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360, Enabled = false };

        // Tab 2
        private readonly TextBox _name = new TextBox { Width = 360 };
        private readonly TextBox _desc = new TextBox { Width = 360, Multiline = true, Height = 60 };
        private readonly ComboBox _operation = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        private readonly ComboBox _mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };

        // Tab 3
        private readonly TextBox _search = new TextBox { Width = 220 };
        private readonly CheckBox _fSelected = new CheckBox { Text = I18n.T("ed.selectedOnly"), AutoSize = true };
        private readonly CheckBox _fLookups = new CheckBox { Text = I18n.T("ed.lookupsOnly"), AutoSize = true };
        private readonly CheckBox _fRequired = new CheckBox { Text = I18n.T("ed.requiredOnly"), AutoSize = true };
        private readonly CheckBox _fWritable = new CheckBox { Text = I18n.T("ed.writableOnly"), AutoSize = true };
        private readonly Label _colCount = new Label { AutoSize = true, ForeColor = UiTheme.Muted, Font = UiTheme.Small };
        private readonly DataGridView _colGrid = new DataGridView();

        // Tab 4
        private readonly FlowLayoutPanel _lookupCards = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };

        private bool _loadingGrid;

        public EditorScreen()
        {
            BuildTab1();
            BuildTab2();
            BuildTab3();
            BuildTab4();
            _tabs.TabPages.AddRange(new[] { _tab1, _tab2, _tab3, _tab4 });
            _tabs.Selected += (s, e) => { if (e.TabPage == _tab3) RefreshColumnsGrid(); if (e.TabPage == _tab4) RefreshLookupCards(); };
            Controls.Add(_tabs);

            var cancel = UiTheme.Button(I18n.T("common.cancel"));
            cancel.Click += (s, e) => Host.Navigate(ScreenName.Configs);
            var save = UiTheme.Button(I18n.T("common.save"));
            save.Click += (s, e) => { CommitGeneral(); Host.Container.SaveConfig(_config); Host.Navigate(ScreenName.Configs); };
            var start = UiTheme.PrimaryButton(I18n.T("common.startImport"));
            start.Click += (s, e) => { CommitGeneral(); Host.Container.SaveConfig(_config); Host.Navigate(ScreenName.ImportRun, _config.Id); };
            Controls.Add(UiTheme.PageHead(I18n.T("ed.tabEntitySource"), null, start, save, cancel));
        }

        public override void OnActivated(object parameters)
        {
            if (Host.Container == null) return;
            var configId = parameters as string;
            var existing = configId != null ? Host.Container.GetConfig(configId) : null;
            _config = existing != null
                ? Json.Deserialize<JobConfiguration>(Json.Serialize(existing)) // edit a copy
                : ConfigBuilder.BlankConfig();

            LoadEntityPicker();
            LoadGeneral();
            UpdateTabState();
            if (!string.IsNullOrEmpty(_config.TargetEntity)) LoadEntityMetadata(_config.TargetEntity, afterLoad: false);
        }

        // ── Tab 1 ────────────────────────────────────────────────
        private void BuildTab1()
        {
            _tab1.Text = "1 · " + I18n.T("ed.tabEntitySource");
            _tab1.Padding = new Padding(12);
            var y = 12;
            _tab1.Controls.Add(Lbl(I18n.T("ed.targetEntity"), 12, y)); y += 22;
            _entityCombo.Location = new Point(12, y);
            _entityCombo.SelectedIndexChanged += (s, e) =>
            {
                if (_entityCombo.SelectedItem is EntitySummary sum) LoadEntityMetadata(sum.LogicalName, afterLoad: true);
            };
            _tab1.Controls.Add(_entityCombo); y += 30;
            _entityInfo.Location = new Point(12, y); _tab1.Controls.Add(_entityInfo); y += 34;

            _tab1.Controls.Add(Lbl(I18n.T("ed.exportSource"), 12, y)); y += 22;
            _srcEntity.Text = I18n.T("ed.sourceEntity"); _srcEntity.Location = new Point(12, y);
            _srcEntity.CheckedChanged += (s, e) => { _viewCombo.Enabled = _srcView.Checked; if (_srcEntity.Checked) _config.ExportSource.Kind = ExportSourceKind.Entity; };
            _tab1.Controls.Add(_srcEntity); y += 24;
            _srcView.Text = I18n.T("ed.sourceView"); _srcView.Location = new Point(12, y);
            _srcView.CheckedChanged += (s, e) => { _viewCombo.Enabled = _srcView.Checked; if (_srcView.Checked) { _config.ExportSource.Kind = ExportSourceKind.SavedView; LoadViews(); } };
            _tab1.Controls.Add(_srcView); y += 24;
            _viewCombo.Location = new Point(32, y);
            _viewCombo.SelectedIndexChanged += (s, e) => { if (_viewCombo.SelectedItem is SavedView v) _config.ExportSource.Reference = v.Id; };
            _tab1.Controls.Add(_viewCombo);
        }

        private void LoadEntityPicker()
        {
            if (_entityCombo.Items.Count > 0) return;
            Host.ExecuteWork(new WorkAsyncInfo
            {
                Message = I18n.T("ed.loadingMeta"),
                Work = (w, e) => e.Result = Host.Container.Metadata.RetrieveEntityList(),
                PostWorkCallBack = e =>
                {
                    if (e.Error != null) { MessageBox.Show(e.Error.Message); return; }
                    _entityCombo.Items.Clear();
                    _entityCombo.DisplayMember = "DisplayName";
                    foreach (var sum in (IReadOnlyList<EntitySummary>)e.Result) _entityCombo.Items.Add(sum);
                    SelectCurrentEntity();
                }
            });
        }

        private void SelectCurrentEntity()
        {
            if (string.IsNullOrEmpty(_config.TargetEntity)) return;
            for (int i = 0; i < _entityCombo.Items.Count; i++)
                if (_entityCombo.Items[i] is EntitySummary s && s.LogicalName == _config.TargetEntity)
                { _entityCombo.SelectedIndex = i; break; }
        }

        private void LoadEntityMetadata(string logicalName, bool afterLoad)
        {
            Host.ExecuteWork(new WorkAsyncInfo
            {
                Message = I18n.T("ed.loadingMeta"),
                Work = (w, e) => e.Result = Host.Container.Metadata.GetEntity(logicalName),
                PostWorkCallBack = e =>
                {
                    if (e.Error != null) { MessageBox.Show(e.Error.Message); return; }
                    _entity = (EntityMetadata)e.Result;
                    if (afterLoad || string.IsNullOrEmpty(_config.EntitySetName))
                    {
                        _config.TargetEntity = _entity.LogicalName;
                        _config.EntitySetName = _entity.EntitySetName;
                        _config.PrimaryIdAttribute = _entity.PrimaryIdAttribute;
                    }
                    _entityInfo.Text = I18n.T("ed.entitySetInfo", new Dictionary<string, object> { ["set"] = _entity.EntitySetName, ["id"] = _entity.PrimaryIdAttribute });
                    UpdateTabState();
                    RefreshColumnsGrid();
                }
            });
        }

        private void LoadViews()
        {
            if (string.IsNullOrEmpty(_config.TargetEntity)) return;
            Host.ExecuteWork(new WorkAsyncInfo
            {
                Message = I18n.T("ed.loadingMeta"),
                Work = (w, e) => e.Result = Host.Container.Views.ListViews(_config.TargetEntity),
                PostWorkCallBack = e =>
                {
                    if (e.Error != null) return;
                    _viewCombo.Items.Clear();
                    _viewCombo.DisplayMember = "Name";
                    SavedView toSelect = null;
                    foreach (var v in (IReadOnlyList<SavedView>)e.Result)
                    {
                        _viewCombo.Items.Add(v);
                        if (v.Id == _config.ExportSource.Reference) toSelect = v;
                    }
                    // Restore the previously saved view so it doesn't look unsaved on reopen.
                    if (toSelect != null) _viewCombo.SelectedItem = toSelect;
                }
            });
        }

        // ── Tab 2 ────────────────────────────────────────────────
        private void BuildTab2()
        {
            _tab2.Text = "2 · " + I18n.T("ed.tabGeneral");
            _tab2.Padding = new Padding(12);
            var y = 12;
            _tab2.Controls.Add(Lbl(I18n.T("ed.name"), 12, y)); y += 22;
            _name.Location = new Point(12, y); _tab2.Controls.Add(_name); y += 30;
            _tab2.Controls.Add(Lbl(I18n.T("ed.description"), 12, y)); y += 22;
            _desc.Location = new Point(12, y); _tab2.Controls.Add(_desc); y += 70;
            _tab2.Controls.Add(Lbl(I18n.T("ed.operation"), 12, y)); y += 22;
            _operation.Items.AddRange(new object[] { OperationType.Create, OperationType.Update, OperationType.CreateOrUpdate });
            _operation.Location = new Point(12, y); _tab2.Controls.Add(_operation); y += 30;
            _tab2.Controls.Add(Lbl(I18n.T("ed.defaultMode"), 12, y)); y += 22;
            _mode.Items.AddRange(new object[] { ImportMode.Strict, ImportMode.Partial });
            _mode.Location = new Point(12, y); _tab2.Controls.Add(_mode);
        }

        private void LoadGeneral()
        {
            _name.Text = _config.Name;
            _desc.Text = _config.Description;
            _operation.SelectedItem = _config.Operation;
            _mode.SelectedItem = _config.DefaultMode;
            _srcEntity.Checked = _config.ExportSource.Kind != ExportSourceKind.SavedView;
            _srcView.Checked = _config.ExportSource.Kind == ExportSourceKind.SavedView;
        }

        private void CommitGeneral()
        {
            _config.Name = _name.Text;
            _config.Description = _desc.Text;
            if (_operation.SelectedItem is OperationType op) _config.Operation = op;
            if (_mode.SelectedItem is ImportMode m) _config.DefaultMode = m;
        }

        // ── Tab 3 ────────────────────────────────────────────────
        private void BuildTab3()
        {
            _tab3.Text = "3 · " + I18n.T("ed.tabColumns");
            _tab3.Padding = new Padding(8);

            var filterBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, WrapContents = false };
            _search.Location = new Point(0, 4);
            _search.TextChanged += (s, e) => RefreshColumnsGrid();
            filterBar.Controls.Add(WithLabel(I18n.T("ed.searchCols"), _search));
            foreach (var cb in new[] { _fSelected, _fLookups, _fRequired, _fWritable })
            {
                cb.Margin = new Padding(8, 8, 4, 0);
                cb.CheckedChanged += (s, e) => RefreshColumnsGrid();
                filterBar.Controls.Add(cb);
            }
            _colCount.Margin = new Padding(12, 10, 0, 0);
            filterBar.Controls.Add(_colCount);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, WrapContents = false };
            var preview = UiTheme.Button(I18n.T("ed.previewData"));
            preview.Click += (s, e) => OpenPreview();
            var tmpl = UiTheme.Button(I18n.T("ed.emptyTemplate"));
            tmpl.Click += (s, e) => ExportTemplate();
            var expData = UiTheme.Button(I18n.T("ed.exportData"));
            expData.Click += (s, e) => ExportData();
            buttons.Controls.Add(preview);
            buttons.Controls.Add(tmpl);
            buttons.Controls.Add(expData);

            _colGrid.Dock = DockStyle.Fill;
            _colGrid.AllowUserToAddRows = false;
            _colGrid.RowHeadersVisible = false;
            _colGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _colGrid.EditMode = DataGridViewEditMode.EditOnEnter;
            _colGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "sel", HeaderText = "", FillWeight = 8 });
            _colGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "display", HeaderText = I18n.T("ed.displayName"), ReadOnly = true, FillWeight = 30 });
            _colGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "logical", HeaderText = I18n.T("ed.logicalName"), ReadOnly = true, FillWeight = 30 });
            _colGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "type", HeaderText = I18n.T("ed.type"), ReadOnly = true, FillWeight = 15 });
            var usage = new DataGridViewComboBoxColumn { Name = "usage", HeaderText = I18n.T("ed.usage"), FillWeight = 17 };
            usage.Items.AddRange(ColumnUsage.ImportExport, ColumnUsage.ExportOnly, ColumnUsage.ImportOnly);
            _colGrid.Columns.Add(usage);
            _colGrid.CurrentCellDirtyStateChanged += (s, e) => { if (_colGrid.IsCurrentCellDirty) _colGrid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            _colGrid.CellValueChanged += ColGridCellValueChanged;

            var pkHint = new Label
            {
                Dock = DockStyle.Top,
                Height = 22,
                Text = I18n.T("ed.pkAutoIncluded"),
                ForeColor = UiTheme.Muted,
                Font = UiTheme.Small,
                Padding = new Padding(2, 4, 0, 0)
            };

            // Dock order: filterBar (added last) sits at the very top, pkHint just below it,
            // grid fills the middle, buttons pinned to the bottom.
            _tab3.Controls.Add(_colGrid);
            _tab3.Controls.Add(buttons);
            _tab3.Controls.Add(pkHint);
            _tab3.Controls.Add(filterBar);
        }

        private void RefreshColumnsGrid()
        {
            if (_entity == null) { _colGrid.Rows.Clear(); return; }
            _loadingGrid = true;
            _colGrid.Rows.Clear();

            var term = _search.Text.Trim().ToLowerInvariant();
            var selected = new HashSet<string>(_config.Columns.Select(c => c.Attribute), StringComparer.OrdinalIgnoreCase);

            foreach (var a in _entity.Attributes.OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase))
            {
                if (a.IsPrimaryId) continue;
                if (_fLookups.Checked && a.Kind != AttributeKind.Lookup) continue;
                if (_fRequired.Checked && !a.IsRequired) continue;
                if (_fWritable.Checked && !a.IsWritable) continue;
                if (_fSelected.Checked && !selected.Contains(a.LogicalName)) continue;
                if (term.Length > 0 && !(a.DisplayName ?? "").ToLowerInvariant().Contains(term) && !a.LogicalName.ToLowerInvariant().Contains(term)) continue;

                var col = _config.Columns.FirstOrDefault(c => c.Attribute == a.LogicalName);
                var idx = _colGrid.Rows.Add(col != null, a.DisplayName, a.LogicalName, a.Kind.ToString() + (a.IsRequired ? " · " + I18n.T("ed.req") : ""), (object)(col?.Usage ?? ColumnUsage.ImportExport));
                _colGrid.Rows[idx].Tag = a;
            }

            _colCount.Text = $"{_config.Columns.Count} {I18n.T("ed.selected")}";
            _loadingGrid = false;
            UpdateTabTitles();
        }

        private void ColGridCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_loadingGrid || e.RowIndex < 0) return;
            var row = _colGrid.Rows[e.RowIndex];
            if (!(row.Tag is AttributeMetadata a)) return;

            if (_colGrid.Columns[e.ColumnIndex].Name == "sel")
            {
                var isChecked = Convert.ToBoolean(row.Cells["sel"].Value);
                if (isChecked) AddColumn(a);
                else RemoveColumn(a);
                _colCount.Text = $"{_config.Columns.Count} {I18n.T("ed.selected")}";
                UpdateTabTitles();
            }
            else if (_colGrid.Columns[e.ColumnIndex].Name == "usage")
            {
                var col = _config.Columns.FirstOrDefault(c => c.Attribute == a.LogicalName);
                if (col != null && row.Cells["usage"].Value is ColumnUsage u) col.Usage = u;
            }
        }

        private void AddColumn(AttributeMetadata a)
        {
            if (_config.Columns.Any(c => c.Attribute == a.LogicalName)) return;
            _config.Columns.Add(ConfigBuilder.ColumnFromAttribute(a, _config.Columns.Count));
            if (a.Kind == AttributeKind.Lookup && !_config.Lookups.Any(l => l.LookupAttribute == a.LogicalName))
                _config.Lookups.Add(ConfigBuilder.LookupFromAttribute(a));
        }

        private void RemoveColumn(AttributeMetadata a)
        {
            _config.Columns.RemoveAll(c => c.Attribute == a.LogicalName);
            _config.Lookups.RemoveAll(l => l.LookupAttribute == a.LogicalName);
        }

        // ── Tab 4 ────────────────────────────────────────────────
        private void BuildTab4()
        {
            _tab4.Text = "4 · " + I18n.T("ed.tabLookups");
            _tab4.Padding = new Padding(8);

            var intro = new Label
            {
                Dock = DockStyle.Top,
                Height = 132,
                Font = UiTheme.Small,
                Text = I18n.T("ed.lookupIntroTitle") + ":\n" +
                       "1) " + I18n.T("ed.match1") + "\n" +
                       "2) " + I18n.T("ed.match2") + "\n" +
                       "3) " + I18n.T("ed.match3") + "\n" +
                       "⚠ " + I18n.T("ed.matchConflict")
            };
            _tab4.Controls.Add(_lookupCards);
            _tab4.Controls.Add(intro);
        }

        private void RefreshLookupCards()
        {
            _lookupCards.Controls.Clear();
            if (_config.Lookups.Count == 0)
            {
                _lookupCards.Controls.Add(new Label { Text = I18n.T("ed.selectLookupHint"), AutoSize = true, ForeColor = UiTheme.Muted, Font = UiTheme.Body, Margin = new Padding(4) });
                return;
            }
            foreach (var lk in _config.Lookups)
                _lookupCards.Controls.Add(new LookupCardControl(Host, _entity, lk));
        }

        // ── shared ──────────────────────────────────────────────
        private void UpdateTabState()
        {
            var hasEntity = !string.IsNullOrEmpty(_config.TargetEntity) && _entity != null;
            _tab2.Enabled = hasEntity;
            _tab3.Enabled = hasEntity;
            _tab4.Enabled = hasEntity;
            UpdateTabTitles();
        }

        private void UpdateTabTitles()
        {
            _tab3.Text = $"3 · {I18n.T("ed.tabColumns")} ({_config.Columns.Count})";
            _tab4.Text = $"4 · {I18n.T("ed.tabLookups")} ({_config.Lookups.Count})";
        }

        private void OpenPreview()
        {
            if (_entity == null) return;
            CommitGeneral();
            using (var modal = new DataPreviewModal(Host, _config))
                modal.ShowDialog(this);
        }

        /// <summary>Export needs at least one selected column; the record key alone is useless.</summary>
        private bool EnsureHasColumns()
        {
            if (_config.Columns.Count > 0) return true;
            MessageBox.Show(I18n.T("ed.needColumns"));
            return false;
        }

        private void ExportTemplate()
        {
            if (!EnsureHasColumns()) return;
            using (var dlg = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", FileName = (_config.Name ?? "Template") + "_Template.xlsx" })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try { System.IO.File.WriteAllBytes(dlg.FileName, Host.Container.Template.Build(_config)); }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        private void ExportData()
        {
            if (_entity == null) return;
            if (!EnsureHasColumns()) return;
            CommitGeneral();
            string path;
            using (var dlg = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", FileName = (_config.Name ?? "Export") + "_Export.xlsx" })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                path = dlg.FileName;
            }

            byte[] bytes = null;
            var config = _config;
            Host.ExecuteWork(new WorkAsyncInfo
            {
                Message = I18n.T("preview.loading"),
                Work = (w, e) =>
                {
                    string viewXml = null;
                    if (config.ExportSource.Kind == ExportSourceKind.SavedView && !string.IsNullOrEmpty(config.ExportSource.Reference))
                        viewXml = Host.Container.Views.ListViews(config.TargetEntity).FirstOrDefault(v => v.Id == config.ExportSource.Reference)?.FetchXml;
                    var rows = Host.Container.Export.FetchSchemaRows(config, 5000, viewXml);
                    bytes = Host.Container.Template.Build(config, rows.Cast<IDictionary<string, object>>().ToList());
                },
                PostWorkCallBack = e =>
                {
                    if (e.Error != null) { MessageBox.Show(e.Error.Message); return; }
                    try { System.IO.File.WriteAllBytes(path, bytes); } catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            });
        }

        private static Label Lbl(string text, int x, int y)
            => new Label { Text = text, AutoSize = true, Location = new Point(x, y + 3), Font = UiTheme.Small };

        private static Control WithLabel(string text, Control c)
        {
            var p = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            p.Controls.Add(new Label { Text = text, AutoSize = true, Padding = new Padding(0, 6, 4, 0), Font = UiTheme.Small });
            p.Controls.Add(c);
            return p;
        }
    }
}
