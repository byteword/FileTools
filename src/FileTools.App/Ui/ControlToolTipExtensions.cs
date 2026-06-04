using System.Windows.Forms;

namespace FileTools;

internal static class ControlToolTipExtensions
{
    private static readonly ToolTip SharedToolTip = new()
    {
        AutoPopDelay = 20000,
        InitialDelay = 500,
        ReshowDelay = 100,
        ShowAlways = true
    };

    public static void SetToolTip(this Control control, string text)
    {
        SharedToolTip.SetToolTip(control, text);
    }
}

