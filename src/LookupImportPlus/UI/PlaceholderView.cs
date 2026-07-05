using System.Drawing;
using System.Windows.Forms;

namespace LookupImportPlus.UI
{
    /// <summary>
    /// Builds a simple titled placeholder panel used by the screen scaffolds
    /// until each screen is implemented. Remove usages as screens are built out.
    /// </summary>
    internal static class PlaceholderView
    {
        public static Control Build(string title, string description)
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(24)
            };
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var heading = new Label
            {
                Text = title,
                AutoSize = true,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 8)
            };

            var body = new Label
            {
                Text = description,
                AutoSize = true,
                MaximumSize = new Size(720, 0),
                ForeColor = SystemColors.GrayText,
                Font = new Font("Segoe UI", 9.75F)
            };

            panel.Controls.Add(heading, 0, 0);
            panel.Controls.Add(body, 0, 1);
            return panel;
        }
    }
}
