using System.Drawing;
using System.Windows.Forms;
using LookupImportPlus.Domain;

namespace LookupImportPlus.UI
{
    /// <summary>Shared colors, fonts and small control factories for the screens.</summary>
    internal static class UiTheme
    {
        public static readonly Color Accent = Color.FromArgb(0, 120, 212);
        public static readonly Color Success = Color.FromArgb(16, 124, 16);
        public static readonly Color Warning = Color.FromArgb(157, 93, 0);
        public static readonly Color Error = Color.FromArgb(196, 43, 28);
        public static readonly Color Muted = Color.FromArgb(96, 96, 96);
        public static readonly Color CardBorder = Color.FromArgb(225, 225, 225);
        public static readonly Color CardBack = Color.White;

        public static Font Heading => new Font("Segoe UI", 15F, FontStyle.Bold);
        public static Font Subheading => new Font("Segoe UI", 10.5F, FontStyle.Bold);
        public static Font Body => new Font("Segoe UI", 9.75F);
        public static Font Small => new Font("Segoe UI", 8.5F);

        /// <summary>Foreground/background pair for a row status chip.</summary>
        public static (Color fore, Color back) StatusColors(RowStatus status)
        {
            switch (status)
            {
                case RowStatus.Ready:
                case RowStatus.LookupResolved:
                case RowStatus.Committed:
                    return (Success, Color.FromArgb(223, 246, 221));
                case RowStatus.Warning:
                    return (Warning, Color.FromArgb(255, 244, 206));
                case RowStatus.LookupAmbiguous:
                case RowStatus.DuplicateInFile:
                    return (Warning, Color.FromArgb(255, 236, 204));
                case RowStatus.Skipped:
                    return (Muted, Color.FromArgb(237, 237, 237));
                default:
                    return (Error, Color.FromArgb(253, 231, 233));
            }
        }

        /// <summary>Small rounded-ish label chip.</summary>
        public static Label Chip(string text, Color fore, Color back)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = fore,
                BackColor = back,
                Font = Small,
                Padding = new Padding(6, 2, 6, 2),
                Margin = new Padding(2),
                TextAlign = ContentAlignment.MiddleCenter
            };
        }

        public static Panel Card()
        {
            return new Panel
            {
                BackColor = CardBack,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(14),
                Margin = new Padding(0, 0, 0, 10)
            };
        }

        /// <summary>Title + subtitle + right-aligned action buttons header.</summary>
        public static Panel PageHead(string title, string subtitle, params Control[] actions)
        {
            var head = new Panel { Dock = DockStyle.Top, Height = subtitle != null ? 74 : 52, Padding = new Padding(0, 0, 0, 8) };

            var titleLabel = new Label { Text = title, AutoSize = true, Font = Heading, Location = new Point(0, 0) };
            head.Controls.Add(titleLabel);

            if (subtitle != null)
            {
                head.Controls.Add(new Label
                {
                    Text = subtitle,
                    AutoSize = false,
                    Font = Body,
                    ForeColor = Muted,
                    Location = new Point(0, 30),
                    Size = new Size(700, 40)
                });
            }

            if (actions != null && actions.Length > 0)
            {
                var flow = new FlowLayoutPanel
                {
                    FlowDirection = FlowDirection.RightToLeft,
                    Dock = DockStyle.Right,
                    AutoSize = true,
                    WrapContents = false
                };
                foreach (var a in actions) flow.Controls.Add(a);
                head.Controls.Add(flow);
            }

            return head;
        }

        public static Button PrimaryButton(string text)
        {
            var b = new Button { Text = text, AutoSize = true, FlatStyle = FlatStyle.Flat, BackColor = Accent, ForeColor = Color.White, Padding = new Padding(8, 4, 8, 4) };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        public static Button Button(string text)
        {
            return new Button { Text = text, AutoSize = true, Padding = new Padding(8, 4, 8, 4) };
        }
    }
}
