using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LookupImportPlus.App;
using LookupImportPlus.Domain;
using XrmToolBox.Extensibility;

namespace LookupImportPlus.UI
{
    /// <summary>
    /// Editor card for one lookup (Tab 4). Covers the visible/GUID/business-key
    /// columns, conflict strategy, target tables, and — per checked target — the
    /// search field, business-key attribute and search conditions. Mirrors the
    /// per-target sub-blocks of the Code App's EditorScreen lookup card.
    /// </summary>
    public sealed class LookupCardControl : UserControl
    {
        private readonly IScreenHost _host;
        private readonly EntityMetadata _parent;
        private readonly LookupConfig _lk;
        private readonly FlowLayoutPanel _perTarget = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Width = 680 };

        // Cache of target logical name → its attributes (loaded lazily).
        private readonly Dictionary<string, List<AttributeMetadata>> _targetAttrs =
            new Dictionary<string, List<AttributeMetadata>>(StringComparer.OrdinalIgnoreCase);

        public LookupCardControl(IScreenHost host, EntityMetadata parent, LookupConfig lk)
        {
            _host = host;
            _parent = parent;
            _lk = lk;

            Width = 700;
            AutoSize = true;
            BorderStyle = BorderStyle.FixedSingle;
            Padding = new Padding(10);
            Margin = new Padding(0, 0, 0, 12);
            BackColor = Color.White;

            var layout = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Width = 680 };
            layout.Controls.Add(new Label { Text = $"{_lk.VisibleColumn} → {_lk.LookupAttribute}", AutoSize = true, Font = UiTheme.Subheading, Margin = new Padding(0, 0, 0, 6) });

            layout.Controls.Add(Field(I18n.T("ed.visibleColumn"), _lk.VisibleColumn, v => _lk.VisibleColumn = v));
            layout.Controls.Add(Field(I18n.T("ed.guidColumn"), _lk.GuidColumn, v => _lk.GuidColumn = v));
            layout.Controls.Add(Field(I18n.T("ed.bkColumnLabel"), _lk.BusinessKeyColumn, v =>
            {
                _lk.BusinessKeyColumn = v;
                _lk.Strategy.UseBusinessKey = !string.IsNullOrEmpty(v);
            }));

            var strat = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
            strat.Items.AddRange(new object[] { ConflictStrategy.Escalate, ConflictStrategy.SkipRow, ConflictStrategy.FailRow });
            strat.SelectedItem = _lk.ConflictStrategy;
            strat.SelectedIndexChanged += (s, e) => { if (strat.SelectedItem is ConflictStrategy cs) _lk.ConflictStrategy = cs; };
            layout.Controls.Add(Labeled(I18n.T("ed.conflictStrategy"), strat));

            layout.Controls.Add(new Label { Text = I18n.T("ed.targetEntitiesLabel"), AutoSize = true, Font = UiTheme.Small, Margin = new Padding(0, 6, 0, 2) });
            var targets = new CheckedListBox { Width = 300, Height = 58, CheckOnClick = true };
            foreach (var t in AllTargets())
            {
                var idx = targets.Items.Add(t);
                targets.SetItemChecked(idx, _lk.TargetEntities.Contains(t));
            }
            targets.ItemCheck += (s, e) => BeginInvoke((Action)(() =>
            {
                _lk.TargetEntities = targets.CheckedItems.Cast<object>().Select(o => o.ToString()).ToList();
                RebuildPerTarget();
            }));
            layout.Controls.Add(targets);

            layout.Controls.Add(new Label { Text = I18n.T("ed.perTargetConfig"), AutoSize = true, Font = UiTheme.Small, ForeColor = UiTheme.Muted, Margin = new Padding(0, 8, 0, 2) });
            layout.Controls.Add(_perTarget);

            Controls.Add(layout);
            RebuildPerTarget();
        }

        private List<string> AllTargets()
        {
            var attr = _parent?.Attributes.FirstOrDefault(a => a.LogicalName == _lk.LookupAttribute);
            var fromMeta = attr?.Lookup?.Targets.Select(t => t.LogicalName) ?? Enumerable.Empty<string>();
            return fromMeta.Union(_lk.TargetEntities).Distinct().ToList();
        }

        private LookupTargetOverride OverrideFor(string target, bool create)
        {
            if (_lk.TargetOverrides == null)
            {
                if (!create) return null;
                _lk.TargetOverrides = new Dictionary<string, LookupTargetOverride>();
            }
            if (!_lk.TargetOverrides.TryGetValue(target, out var o) && create)
            {
                o = new LookupTargetOverride();
                _lk.TargetOverrides[target] = o;
            }
            return o;
        }

        private string SearchAttrFor(string target)
        {
            var o = OverrideFor(target, false);
            return !string.IsNullOrEmpty(o?.SearchAttribute) ? o.SearchAttribute : _lk.SearchAttribute;
        }

        private string BkAttrFor(string target)
        {
            var o = OverrideFor(target, false);
            return !string.IsNullOrEmpty(o?.BusinessKeyAttribute) ? o.BusinessKeyAttribute : _lk.BusinessKeyAttribute;
        }

        private ConditionGroup ConditionsFor(string target)
        {
            var o = OverrideFor(target, false);
            return o?.Conditions ?? _lk.Conditions;
        }

        private void RebuildPerTarget()
        {
            _perTarget.Controls.Clear();
            if (_lk.TargetEntities.Count == 0)
            {
                _perTarget.Controls.Add(new Label { Text = I18n.T("ed.selectTargetFirst"), AutoSize = true, ForeColor = UiTheme.Muted, Font = UiTheme.Small });
                return;
            }
            foreach (var target in _lk.TargetEntities)
                _perTarget.Controls.Add(BuildTargetBlock(target));
        }

        private Control BuildTargetBlock(string target)
        {
            var block = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Width = 660, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(6), Margin = new Padding(0, 0, 0, 6) };
            block.Controls.Add(new Label { Text = target, AutoSize = true, Font = UiTheme.Subheading });

            var searchCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 240 };
            searchCombo.Text = SearchAttrFor(target);
            searchCombo.TextChanged += (s, e) => SetSearch(target, searchCombo.Text);
            block.Controls.Add(Labeled(I18n.T("ed.searchFieldLabel"), searchCombo));

            var bkCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 240 };
            bkCombo.Text = BkAttrFor(target) ?? "";
            bkCombo.TextChanged += (s, e) => SetBk(target, bkCombo.Text);
            block.Controls.Add(Labeled(I18n.T("ed.bkFieldLabel"), bkCombo));

            block.Controls.Add(new Label { Text = I18n.T("ed.conditionsLabel"), AutoSize = true, Font = UiTheme.Small, ForeColor = UiTheme.Muted, Margin = new Padding(0, 6, 0, 2) });
            var grid = BuildConditionsGrid(target);
            block.Controls.Add(grid);

            var add = UiTheme.Button(I18n.T("ed.addCondition"));
            add.Click += (s, e) =>
            {
                var group = EnsureConditions(target);
                group.Conditions.Add(new Condition { Attribute = "", Operator = ConditionOperator.Eq, Value = ConditionValue.Literal("") });
                ReloadConditions(grid, target);
            };
            block.Controls.Add(add);

            // Populate attribute combos once the target metadata is available.
            LoadTargetAttrs(target, attrs =>
            {
                var stringAttrs = attrs.Where(a => a.Kind == AttributeKind.String || a.Kind == AttributeKind.Memo).Select(a => a.LogicalName).ToArray();
                searchCombo.Items.AddRange(stringAttrs);
                bkCombo.Items.AddRange(stringAttrs);
            });

            return block;
        }

        private DataGridView BuildConditionsGrid(string target)
        {
            var grid = new DataGridView
            {
                Width = 640,
                Height = 120,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "attr", HeaderText = I18n.T("ed.logicalName") });
            var op = new DataGridViewComboBoxColumn { Name = "op", HeaderText = "Op" };
            op.Items.AddRange(Enum.GetValues(typeof(ConditionOperator)).Cast<object>().ToArray());
            grid.Columns.Add(op);
            var vt = new DataGridViewComboBoxColumn { Name = "vt", HeaderText = I18n.T("ed.type") };
            vt.Items.AddRange(I18n.T("ed.srcLiteral"), I18n.T("ed.srcExcel"), I18n.T("ed.srcRelative"));
            grid.Columns.Add(vt);
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "val", HeaderText = "Wert" });
            var del = new DataGridViewButtonColumn { Name = "del", HeaderText = "", Text = "✕", UseColumnTextForButtonValue = true, FillWeight = 10 };
            grid.Columns.Add(del);

            grid.CurrentCellDirtyStateChanged += (s, e) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            grid.CellValueChanged += (s, e) => { if (e.RowIndex >= 0) WriteConditionRow(grid, target, e.RowIndex); };
            grid.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && grid.Columns[e.ColumnIndex].Name == "del")
                {
                    var group = ConditionsFor(target);
                    if (e.RowIndex < group.Conditions.Count) group.Conditions.RemoveAt(e.RowIndex);
                    ReloadConditions(grid, target);
                }
            };

            ReloadConditions(grid, target);
            return grid;
        }

        private void ReloadConditions(DataGridView grid, string target)
        {
            grid.Rows.Clear();
            var group = ConditionsFor(target);
            foreach (var c in group.Conditions)
            {
                var vtLabel = c.Value?.Kind == ValueSourceKind.ExcelColumn ? I18n.T("ed.srcExcel")
                    : c.Value?.Kind == ValueSourceKind.RelativeDate ? I18n.T("ed.srcRelative")
                    : I18n.T("ed.srcLiteral");
                var valText = c.Value?.Kind == ValueSourceKind.ExcelColumn ? c.Value.Column
                    : c.Value?.Kind == ValueSourceKind.RelativeDate ? c.Value.OffsetDays.ToString()
                    : c.Value?.Value?.ToString() ?? "";
                grid.Rows.Add(c.Attribute, c.Operator, vtLabel, valText, "✕");
            }
        }

        private void WriteConditionRow(DataGridView grid, string target, int rowIndex)
        {
            var group = EnsureConditions(target);
            while (group.Conditions.Count <= rowIndex) group.Conditions.Add(new Condition());
            var c = group.Conditions[rowIndex];
            var row = grid.Rows[rowIndex];
            c.Attribute = row.Cells["attr"].Value?.ToString() ?? "";
            if (row.Cells["op"].Value is ConditionOperator op) c.Operator = op;
            var vt = row.Cells["vt"].Value?.ToString();
            var val = row.Cells["val"].Value?.ToString() ?? "";
            if (vt == I18n.T("ed.srcExcel")) c.Value = ConditionValue.Excel(val);
            else if (vt == I18n.T("ed.srcRelative")) c.Value = ConditionValue.Relative(int.TryParse(val, out var n) ? n : 0);
            else c.Value = ConditionValue.Literal(val);
        }

        private ConditionGroup EnsureConditions(string target)
        {
            // Multi-target lookups store conditions per target override; single-target uses the base group.
            if (_lk.TargetEntities.Count > 1)
            {
                var o = OverrideFor(target, true);
                return o.Conditions ?? (o.Conditions = ConditionGroup.Empty());
            }
            return _lk.Conditions ?? (_lk.Conditions = ConditionGroup.Empty());
        }

        private void SetSearch(string target, string value)
        {
            if (_lk.TargetEntities.Count > 1) OverrideFor(target, true).SearchAttribute = value;
            else _lk.SearchAttribute = value;
            if (!string.IsNullOrEmpty(value) && !_lk.CandidateDisplayAttributes.Contains(value))
                _lk.CandidateDisplayAttributes = new List<string> { value };
        }

        private void SetBk(string target, string value)
        {
            if (_lk.TargetEntities.Count > 1) OverrideFor(target, true).BusinessKeyAttribute = value;
            else _lk.BusinessKeyAttribute = value;
        }

        private void LoadTargetAttrs(string target, Action<List<AttributeMetadata>> onLoaded)
        {
            if (_targetAttrs.TryGetValue(target, out var cached)) { onLoaded(cached); return; }
            _host.ExecuteWork(new WorkAsyncInfo
            {
                Message = I18n.T("ed.loadingMeta"),
                Work = (w, e) => e.Result = _host.Container.Metadata.GetEntity(target).Attributes,
                PostWorkCallBack = e =>
                {
                    if (e.Error != null) return;
                    var attrs = ((IEnumerable<AttributeMetadata>)e.Result).ToList();
                    _targetAttrs[target] = attrs;
                    onLoaded(attrs);
                }
            });
        }

        private Control Field(string label, string value, Action<string> onChange)
        {
            var tb = new TextBox { Text = value, Width = 240 };
            tb.TextChanged += (s, e) => onChange(tb.Text);
            return Labeled(label, tb);
        }

        private static Control Labeled(string label, Control c)
        {
            var p = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, Margin = new Padding(0, 2, 0, 2) };
            p.Controls.Add(new Label { Text = label, AutoSize = true, Width = 180, Font = UiTheme.Small, Padding = new Padding(0, 6, 0, 0) });
            p.Controls.Add(c);
            return p;
        }
    }
}
