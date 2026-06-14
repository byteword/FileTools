using System.Drawing;
using System.Drawing.Drawing2D;

namespace FileTools;

internal static class UiIconFactory
{
    public static Image Add { get; } = CreateIcon(DrawAdd, Color.FromArgb(32, 123, 67));
    public static Image FolderAdd { get; } = CreateIcon(DrawFolderAdd, Color.FromArgb(39, 99, 164));
    public static Image Remove { get; } = CreateIcon(DrawRemove, Color.FromArgb(170, 59, 48));
    public static Image Clear { get; } = CreateIcon(DrawClear, Color.FromArgb(122, 75, 40));
    public static Image MoveUp { get; } = CreateIcon(DrawMoveUp, Color.FromArgb(70, 83, 102));
    public static Image MoveDown { get; } = CreateIcon(DrawMoveDown, Color.FromArgb(70, 83, 102));
    public static Image Rename { get; } = CreateIcon(DrawRename, Color.FromArgb(41, 99, 163));
    public static Image Wrap { get; } = CreateIcon(DrawWrap, Color.FromArgb(32, 123, 67));
    public static Image Unwrap { get; } = CreateIcon(DrawUnwrap, Color.FromArgb(152, 84, 33));
    public static Image ArchiveMerge { get; } = CreateIcon(DrawArchiveMerge, Color.FromArgb(20, 116, 148));
    public static Image Compare { get; } = CreateIcon(DrawCompare, Color.FromArgb(59, 130, 246));
    public static Image Relocate { get; } = CreateIcon(DrawRelocate, Color.FromArgb(96, 80, 170));
    public static Image RemoveStep { get; } = CreateIcon(DrawRemoveStep, Color.FromArgb(170, 59, 48));
    public static Image Settings { get; } = CreateIcon(DrawSettings, Color.FromArgb(70, 83, 102));
    public static Image Info { get; } = CreateIcon(DrawInfo, Color.FromArgb(41, 99, 163));
    public static Image Play { get; } = CreateIcon(DrawPlay, Color.FromArgb(32, 123, 67));
    public static Image Stop { get; } = CreateIcon(DrawStop, Color.FromArgb(170, 59, 48));

    private static Bitmap CreateIcon(Action<Graphics, Rectangle, Color> draw, Color color)
    {
        var bitmap = new Bitmap(24, 24);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        draw(graphics, new Rectangle(2, 2, 20, 20), color);
        return bitmap;
    }

    private static void DrawAdd(Graphics graphics, Rectangle bounds, Color color)
    {
        using var pen = new Pen(color, 3F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        var centerX = bounds.Left + bounds.Width / 2;
        var centerY = bounds.Top + bounds.Height / 2;
        graphics.DrawLine(pen, centerX, bounds.Top + 4, centerX, bounds.Bottom - 4);
        graphics.DrawLine(pen, bounds.Left + 4, centerY, bounds.Right - 4, centerY);
    }

    private static void DrawFolderAdd(Graphics graphics, Rectangle bounds, Color color)
    {
        using var brush = new SolidBrush(Color.FromArgb(54, 134, 209));
        using var pen = new Pen(color, 1.6F);
        var folder = new Rectangle(bounds.Left + 1, bounds.Top + 7, bounds.Width - 2, bounds.Height - 7);
        graphics.FillRectangle(brush, folder);
        graphics.DrawRectangle(pen, folder);
        graphics.FillRectangle(brush, bounds.Left + 2, bounds.Top + 4, 8, 5);
        DrawAdd(graphics, new Rectangle(bounds.Left + 7, bounds.Top + 7, 12, 12), Color.White);
    }

    private static void DrawRemove(Graphics graphics, Rectangle bounds, Color color)
    {
        using var pen = new Pen(color, 3F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        var centerY = bounds.Top + bounds.Height / 2;
        graphics.DrawLine(pen, bounds.Left + 4, centerY, bounds.Right - 4, centerY);
    }

    private static void DrawClear(Graphics graphics, Rectangle bounds, Color color)
    {
        using var pen = new Pen(color, 2.4F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        graphics.DrawLine(pen, bounds.Left + 5, bounds.Top + 5, bounds.Right - 5, bounds.Bottom - 5);
        graphics.DrawLine(pen, bounds.Right - 5, bounds.Top + 5, bounds.Left + 5, bounds.Bottom - 5);
    }

    private static void DrawMoveUp(Graphics graphics, Rectangle bounds, Color color)
    {
        using var brush = new SolidBrush(color);
        var points = new[]
        {
            new Point(bounds.Left + bounds.Width / 2, bounds.Top + 4),
            new Point(bounds.Left + 4, bounds.Top + 13),
            new Point(bounds.Left + 9, bounds.Top + 13),
            new Point(bounds.Left + 9, bounds.Bottom - 4),
            new Point(bounds.Right - 9, bounds.Bottom - 4),
            new Point(bounds.Right - 9, bounds.Top + 13),
            new Point(bounds.Right - 4, bounds.Top + 13)
        };
        graphics.FillPolygon(brush, points);
    }

    private static void DrawMoveDown(Graphics graphics, Rectangle bounds, Color color)
    {
        using var brush = new SolidBrush(color);
        var points = new[]
        {
            new Point(bounds.Left + bounds.Width / 2, bounds.Bottom - 4),
            new Point(bounds.Left + 4, bounds.Bottom - 13),
            new Point(bounds.Left + 9, bounds.Bottom - 13),
            new Point(bounds.Left + 9, bounds.Top + 4),
            new Point(bounds.Right - 9, bounds.Top + 4),
            new Point(bounds.Right - 9, bounds.Bottom - 13),
            new Point(bounds.Right - 4, bounds.Bottom - 13)
        };
        graphics.FillPolygon(brush, points);
    }

    private static void DrawRename(Graphics graphics, Rectangle bounds, Color color)
    {
        using var pen = new Pen(color, 2.4F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        graphics.DrawLine(pen, bounds.Left + 5, bounds.Bottom - 5, bounds.Right - 5, bounds.Top + 5);
        using var textPen = new Pen(Color.FromArgb(90, 90, 90), 1.4F);
        graphics.DrawLine(textPen, bounds.Left + 4, bounds.Top + 6, bounds.Left + 12, bounds.Top + 6);
        graphics.DrawLine(textPen, bounds.Left + 4, bounds.Top + 11, bounds.Left + 10, bounds.Top + 11);
        graphics.DrawLine(textPen, bounds.Left + 4, bounds.Top + 16, bounds.Left + 14, bounds.Top + 16);
    }

    private static void DrawWrap(Graphics graphics, Rectangle bounds, Color color)
    {
        using var pen = new Pen(color, 2F);
        var box = new Rectangle(bounds.Left + 4, bounds.Top + 5, bounds.Width - 8, bounds.Height - 8);
        graphics.DrawRectangle(pen, box);
        using var brush = new SolidBrush(color);
        graphics.FillRectangle(brush, bounds.Left + 8, bounds.Bottom - 7, bounds.Width - 16, 3);
        graphics.FillPolygon(brush, new[]
        {
            new Point(bounds.Left + bounds.Width / 2, bounds.Top + 8),
            new Point(bounds.Left + 8, bounds.Top + 14),
            new Point(bounds.Right - 8, bounds.Top + 14)
        });
    }

    private static void DrawUnwrap(Graphics graphics, Rectangle bounds, Color color)
    {
        using var pen = new Pen(color, 2F);
        var box = new Rectangle(bounds.Left + 4, bounds.Top + 7, bounds.Width - 8, bounds.Height - 8);
        graphics.DrawRectangle(pen, box);
        using var brush = new SolidBrush(color);
        graphics.FillRectangle(brush, bounds.Left + 9, bounds.Top + 6, bounds.Width - 18, 8);
        graphics.FillPolygon(brush, new[]
        {
            new Point(bounds.Left + bounds.Width / 2, bounds.Top + 3),
            new Point(bounds.Left + 7, bounds.Top + 10),
            new Point(bounds.Right - 7, bounds.Top + 10)
        });
    }

    private static void DrawArchiveMerge(Graphics graphics, Rectangle bounds, Color color)
    {
        using var pen = new Pen(color, 1.7F);
        using var brush = new SolidBrush(Color.FromArgb(232, 247, 250));
        var left = new Rectangle(bounds.Left + 2, bounds.Top + 3, 8, 10);
        var right = new Rectangle(bounds.Right - 10, bounds.Top + 3, 8, 10);
        var output = new Rectangle(bounds.Left + 6, bounds.Bottom - 9, bounds.Width - 12, 7);
        graphics.FillRectangle(brush, left);
        graphics.FillRectangle(brush, right);
        graphics.FillRectangle(brush, output);
        graphics.DrawRectangle(pen, left);
        graphics.DrawRectangle(pen, right);
        graphics.DrawRectangle(pen, output);
        using var arrowPen = new Pen(color, 2F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        graphics.DrawLine(arrowPen, bounds.Left + 7, bounds.Top + 14, bounds.Left + 10, bounds.Bottom - 10);
        graphics.DrawLine(arrowPen, bounds.Right - 7, bounds.Top + 14, bounds.Right - 10, bounds.Bottom - 10);
        graphics.DrawLine(arrowPen, bounds.Left + 10, bounds.Bottom - 10, bounds.Right - 10, bounds.Bottom - 10);
    }

    private static void DrawRelocate(Graphics graphics, Rectangle bounds, Color color)
    {
        using var pen = new Pen(color, 2.2F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        graphics.DrawLine(pen, bounds.Left + 5, bounds.Top + 7, bounds.Right - 7, bounds.Top + 7);
        graphics.DrawLine(pen, bounds.Right - 9, bounds.Top + 4, bounds.Right - 5, bounds.Top + 7);
        graphics.DrawLine(pen, bounds.Right - 9, bounds.Top + 10, bounds.Right - 5, bounds.Top + 7);
        graphics.DrawLine(pen, bounds.Right - 5, bounds.Bottom - 7, bounds.Left + 7, bounds.Bottom - 7);
        graphics.DrawLine(pen, bounds.Left + 9, bounds.Bottom - 4, bounds.Left + 5, bounds.Bottom - 7);
        graphics.DrawLine(pen, bounds.Left + 9, bounds.Bottom - 10, bounds.Left + 5, bounds.Bottom - 7);
    }

    private static void DrawCompare(Graphics graphics, Rectangle bounds, Color color)
    {
        using var pen = new Pen(color, 1.8F);
        using var brush = new SolidBrush(Color.FromArgb(239, 246, 255));
        var left = new Rectangle(bounds.Left + 2, bounds.Top + 3, 8, 12);
        var right = new Rectangle(bounds.Right - 11, bounds.Top + 3, 8, 12);
        graphics.FillRectangle(brush, left);
        graphics.FillRectangle(brush, right);
        graphics.DrawRectangle(pen, left);
        graphics.DrawRectangle(pen, right);

        using var glassPen = new Pen(color, 2F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        graphics.DrawEllipse(glassPen, bounds.Left + 7, bounds.Bottom - 12, 8, 8);
        graphics.DrawLine(glassPen, bounds.Left + 14, bounds.Bottom - 5, bounds.Right - 3, bounds.Bottom - 2);
    }

    private static void DrawRemoveStep(Graphics graphics, Rectangle bounds, Color color)
    {
        using var linePen = new Pen(Color.FromArgb(90, 90, 90), 1.6F);
        graphics.DrawLine(linePen, bounds.Left + 4, bounds.Top + 6, bounds.Right - 6, bounds.Top + 6);
        graphics.DrawLine(linePen, bounds.Left + 4, bounds.Top + 11, bounds.Right - 6, bounds.Top + 11);
        graphics.DrawLine(linePen, bounds.Left + 4, bounds.Top + 16, bounds.Right - 6, bounds.Top + 16);
        DrawClear(graphics, new Rectangle(bounds.Right - 11, bounds.Bottom - 11, 10, 10), color);
    }

    private static void DrawSettings(Graphics graphics, Rectangle bounds, Color color)
    {
        using var pen = new Pen(color, 2F);
        graphics.DrawEllipse(pen, bounds.Left + 4, bounds.Top + 4, bounds.Width - 8, bounds.Height - 8);
        graphics.DrawEllipse(pen, bounds.Left + 8, bounds.Top + 8, bounds.Width - 16, bounds.Height - 16);
        graphics.DrawLine(pen, bounds.Left + bounds.Width / 2, bounds.Top + 1, bounds.Left + bounds.Width / 2, bounds.Top + 5);
        graphics.DrawLine(pen, bounds.Left + bounds.Width / 2, bounds.Bottom - 5, bounds.Left + bounds.Width / 2, bounds.Bottom - 1);
        graphics.DrawLine(pen, bounds.Left + 1, bounds.Top + bounds.Height / 2, bounds.Left + 5, bounds.Top + bounds.Height / 2);
        graphics.DrawLine(pen, bounds.Right - 5, bounds.Top + bounds.Height / 2, bounds.Right - 1, bounds.Top + bounds.Height / 2);
    }

    private static void DrawInfo(Graphics graphics, Rectangle bounds, Color color)
    {
        using var pen = new Pen(color, 2F);
        using var brush = new SolidBrush(color);
        graphics.DrawEllipse(pen, bounds.Left + 3, bounds.Top + 3, bounds.Width - 6, bounds.Height - 6);
        graphics.FillEllipse(brush, bounds.Left + bounds.Width / 2 - 1, bounds.Top + 6, 3, 3);
        graphics.FillRectangle(brush, bounds.Left + bounds.Width / 2 - 1, bounds.Top + 11, 3, 7);
    }

    private static void DrawPlay(Graphics graphics, Rectangle bounds, Color color)
    {
        using var brush = new SolidBrush(color);
        graphics.FillPolygon(brush, new[]
        {
            new Point(bounds.Left + 6, bounds.Top + 4),
            new Point(bounds.Left + 6, bounds.Bottom - 4),
            new Point(bounds.Right - 4, bounds.Top + bounds.Height / 2)
        });
    }

    private static void DrawStop(Graphics graphics, Rectangle bounds, Color color)
    {
        using var brush = new SolidBrush(color);
        graphics.FillRectangle(brush, bounds.Left + 5, bounds.Top + 5, bounds.Width - 10, bounds.Height - 10);
    }
}
