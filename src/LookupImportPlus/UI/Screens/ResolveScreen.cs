using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LookupImportPlus.App;
using LookupImportPlus.Domain;

namespace LookupImportPlus.UI.Screens
{
    /// <summary>
    /// 3.7 Resolve conflict (detail). Shows the query used incl. time anchor, the
    /// candidate list (radio, with a deep-link), an "apply to all n rows"
    /// checkbox. Apply writes the decision back AND logs it (audit).
    /// </summary>
    public sealed class ResolveScreen : ScreenControlBase
    {
        private ConflictKey _key;
        private LookupResolution _resolution;
        private List<int> _affectedRows = new List<int>();

        private readonly Panel _body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(4) };
        private readonly List<RadioButton> _radios = new List<RadioButton>();
        private CheckBox _applyAll;

        public ResolveScreen()
        {
            Controls.Add(_body);
            var back = UiTheme.Button(I18n.T("common.toList"));
            back.Click += (s, e) => Host.Navigate(ScreenName.Conflicts);
            Controls.Add(UiTheme.PageHead(I18n.T("res.title"), null, back));
        }

        public override void OnActivated(object parameters)
        {
            _key = parameters as ConflictKey;
            _body.Controls.Clear();
            _radios.Clear();

            var job = Host.Container?.ActiveJob;
            if (job == null || _key == null) { AddLabel(I18n.T("res.notOpen"), UiTheme.Muted); return; }

            _resolution = null;
            _affectedRows = new List<int>();
            foreach (var row in job.Rows)
            {
                var l = row.Lookups.FirstOrDefault(x => x.LookupAttribute == _key.LookupAttribute && x.SourceValue == _key.SourceValue
                    && (x.Status == LookupResolutionStatus.Ambiguous || x.Status == LookupResolutionStatus.NotFound));
                if (l != null)
                {
                    if (_resolution == null) _resolution = l;
                    _affectedRows.Add(row.RowNumber);
                }
            }

            if (_resolution == null) { AddLabel(I18n.T("res.notOpen"), UiTheme.Muted); return; }
            Render();
        }

        private void Render()
        {
            int y = 4;

            AddHeader($"{I18n.T("res.sourceValue")}: {_key.SourceValue ?? "—"}    ·    {I18n.T("res.targetField")}: {_key.LookupAttribute}", ref y);

            // Warn box.
            var reason = _resolution.Status == LookupResolutionStatus.NotFound ? I18n.T("res.noTarget") : I18n.T("res.notUnique");
            AddWrapped($"⚠ {reason} — {_affectedRows.Count} {I18n.T("res.affected")}", UiTheme.Warning, ref y);

            // Query display.
            if (!string.IsNullOrEmpty(_resolution.AppliedFilter))
            {
                AddWrapped($"{I18n.T("res.query")}: {_resolution.AppliedFilter}", UiTheme.Muted, ref y);
                if (_resolution.ResolvedTimeAnchors != null)
                    foreach (var kv in _resolution.ResolvedTimeAnchors)
                        AddWrapped($"{I18n.T("res.timeAnchor")}: {kv.Key} = {kv.Value}", UiTheme.Muted, ref y);
            }

            // Candidates.
            var candidates = _resolution.Candidates ?? new List<LookupCandidate>();
            if (candidates.Count == 0)
            {
                AddWrapped(I18n.T("res.noCandidates"), UiTheme.Muted, ref y);
            }
            else
            {
                AddHeader(I18n.T("res.chooseCandidate"), ref y);
                foreach (var cand in candidates)
                {
                    var radio = new RadioButton
                    {
                        Text = FormatCandidate(cand),
                        AutoSize = true,
                        Location = new Point(8, y),
                        Tag = cand
                    };
                    _radios.Add(radio);
                    _body.Controls.Add(radio);

                    if (!string.IsNullOrEmpty(cand.RecordUrl))
                    {
                        var link = new LinkLabel { Text = I18n.T("res.open"), AutoSize = true, Location = new Point(radio.Right + 400, y) };
                        var url = cand.RecordUrl;
                        link.Click += (s, e) => { try { Process.Start(url); } catch { } };
                        // Place link at a fixed x to avoid layout races.
                        link.Location = new Point(560, y);
                        _body.Controls.Add(link);
                    }
                    y += 26;
                }
                if (_radios.Count > 0) _radios[0].Checked = true;
            }

            y += 6;
            _applyAll = new CheckBox { Text = I18n.T("res.applyAll", "n", _affectedRows.Count), AutoSize = true, Checked = true, Location = new Point(8, y) };
            _body.Controls.Add(_applyAll);
            y += 30;

            var skip = UiTheme.Button(I18n.T("res.skipRows"));
            skip.Location = new Point(8, y);
            skip.Click += (s, e) => ApplyDecision(null);
            _body.Controls.Add(skip);

            if (candidates.Count > 0)
            {
                var apply = UiTheme.PrimaryButton(I18n.T("res.apply"));
                apply.Location = new Point(skip.Right + 8, y);
                apply.Click += (s, e) =>
                {
                    var chosen = _radios.FirstOrDefault(r => r.Checked)?.Tag as LookupCandidate;
                    if (chosen != null) ApplyDecision(chosen);
                };
                _body.Controls.Add(apply);
            }
        }

        private void ApplyDecision(LookupCandidate chosen)
        {
            var job = Host.Container.ActiveJob;
            var decision = new ResolutionDecision
            {
                RowNumber = _affectedRows.FirstOrDefault(),
                LookupAttribute = _key.LookupAttribute,
                SourceValue = _key.SourceValue,
                Candidates = _resolution.Candidates ?? new List<LookupCandidate>(),
                ChosenId = chosen?.Id,
                ChosenEntity = chosen?.EntityLogicalName,
                AppliedFilter = _resolution.AppliedFilter,
                DecidedBy = SafeWhoAmI(),
                DecidedOn = DateTime.UtcNow.ToString("o"),
                AppliedToAll = _applyAll?.Checked ?? true
            };
            Host.Container.Runner.ApplyDecision(job, decision);
            Host.Navigate(ScreenName.Conflicts);
        }

        private string SafeWhoAmI()
        {
            try { return Host.Container.Ctx.WhoAmI().ToString(); }
            catch { return "unknown"; }
        }

        private static string FormatCandidate(LookupCandidate c)
        {
            var extra = c.Attributes != null && c.Attributes.Count > 0
                ? "  ·  " + string.Join("  ", c.Attributes.Select(kv => $"{kv.Key}={kv.Value}"))
                : "";
            var shortId = c.Id.Length >= 8 ? c.Id.Substring(0, 8) : c.Id;
            return $"{c.PrimaryName}  ·  {c.EntityLogicalName}  ·  {shortId}…{extra}";
        }

        private void AddHeader(string text, ref int y)
        {
            _body.Controls.Add(new Label { Text = text, AutoSize = true, Font = UiTheme.Subheading, Location = new Point(4, y) });
            y += 28;
        }

        private void AddWrapped(string text, Color color, ref int y)
        {
            var l = new Label { Text = text, AutoSize = false, Size = new Size(760, 34), Font = UiTheme.Small, ForeColor = color, Location = new Point(4, y) };
            _body.Controls.Add(l);
            y += 38;
        }

        private void AddLabel(string text, Color color)
        {
            _body.Controls.Add(new Label { Text = text, AutoSize = true, ForeColor = color, Font = UiTheme.Body, Location = new Point(8, 8) });
        }
    }
}
