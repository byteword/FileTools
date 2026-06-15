using System.Windows.Forms;

namespace FileTools;

internal static class DialogButtonPanelFactory
{
    private const int ButtonGap = 8;
    private const int TopPadding = 8;

    public static TableLayoutPanel CreateRightAligned(params Button[] buttons)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = buttons.Length + 1,
            RowCount = 1,
            Padding = new Padding(0, TopPadding, 0, 0)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        for (var index = 0; index < buttons.Length; index++)
        {
            var button = buttons[index];
            var leftMargin = index == 0 ? 0 : ButtonGap;
            button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button.Margin = new Padding(leftMargin, 0, 0, 0);
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, button.Width + leftMargin));
            panel.Controls.Add(button, index + 1, 0);
        }

        return panel;
    }
}
