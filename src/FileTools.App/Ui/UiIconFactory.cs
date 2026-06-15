using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace FileTools;

internal enum UiIconKind
{
    Add,
    FolderAdd,
    Remove,
    Clear,
    MoveUp,
    MoveDown,
    Rename,
    Wrap,
    Unwrap,
    ArchiveMerge,
    Compare,
    Relocate,
    RemoveStep,
    Settings,
    Info,
    Exit,
    Play,
    Stop
}

internal static class UiIconFactory
{
    private const int DefaultIconSize = 24;
    private const int LogicalCanvasSize = 24;
    private const int LogicalContentInset = 2;
    private const int LogicalContentSize = 20;
    private static readonly Dictionary<(UiIconKind Kind, int Size), Image> IconCache = [];
    private static readonly Dictionary<UiIconKind, IconDefinition> Definitions = new()
    {
        [UiIconKind.Add] = new(DrawAdd, Color.FromArgb(32, 123, 67)),
        [UiIconKind.FolderAdd] = new(DrawFolderAdd, Color.FromArgb(39, 99, 164)),
        [UiIconKind.Remove] = new(DrawRemove, Color.FromArgb(170, 59, 48)),
        [UiIconKind.Clear] = new(DrawClear, Color.FromArgb(122, 75, 40)),
        [UiIconKind.MoveUp] = new(DrawMoveUp, Color.FromArgb(70, 83, 102)),
        [UiIconKind.MoveDown] = new(DrawMoveDown, Color.FromArgb(70, 83, 102)),
        [UiIconKind.Rename] = new(DrawRename, Color.FromArgb(41, 99, 163)),
        [UiIconKind.Wrap] = new(DrawWrap, Color.FromArgb(32, 123, 67)),
        [UiIconKind.Unwrap] = new(DrawUnwrap, Color.FromArgb(152, 84, 33)),
        [UiIconKind.ArchiveMerge] = new(DrawArchiveMerge, Color.FromArgb(20, 116, 148)),
        [UiIconKind.Compare] = new(DrawCompare, Color.FromArgb(59, 130, 246)),
        [UiIconKind.Relocate] = new(DrawRelocate, Color.FromArgb(96, 80, 170)),
        [UiIconKind.RemoveStep] = new(DrawRemoveStep, Color.FromArgb(170, 59, 48)),
        [UiIconKind.Settings] = new(DrawSettings, Color.FromArgb(70, 83, 102)),
        [UiIconKind.Info] = new(DrawInfo, Color.FromArgb(41, 99, 163)),
        [UiIconKind.Exit] = new(DrawExit, Color.FromArgb(70, 83, 102)),
        [UiIconKind.Play] = new(DrawPlay, Color.FromArgb(32, 123, 67)),
        [UiIconKind.Stop] = new(DrawStop, Color.FromArgb(170, 59, 48))
    };

    public static Image Add => GetIcon(UiIconKind.Add);
    public static Image FolderAdd => GetIcon(UiIconKind.FolderAdd);
    public static Image Remove => GetIcon(UiIconKind.Remove);
    public static Image Clear => GetIcon(UiIconKind.Clear);
    public static Image MoveUp => GetIcon(UiIconKind.MoveUp);
    public static Image MoveDown => GetIcon(UiIconKind.MoveDown);
    public static Image Rename => GetIcon(UiIconKind.Rename);
    public static Image Wrap => GetIcon(UiIconKind.Wrap);
    public static Image Unwrap => GetIcon(UiIconKind.Unwrap);
    public static Image ArchiveMerge => GetIcon(UiIconKind.ArchiveMerge);
    public static Image Compare => GetIcon(UiIconKind.Compare);
    public static Image Relocate => GetIcon(UiIconKind.Relocate);
    public static Image RemoveStep => GetIcon(UiIconKind.RemoveStep);
    public static Image Settings => GetIcon(UiIconKind.Settings);
    public static Image Info => GetIcon(UiIconKind.Info);
    public static Image Exit => GetIcon(UiIconKind.Exit);
    public static Image Play => GetIcon(UiIconKind.Play);
    public static Image Stop => GetIcon(UiIconKind.Stop);

    public static Image GetIcon(UiIconKind kind, int imageSize = DefaultIconSize)
    {
        var normalizedSize = Math.Clamp(imageSize, 16, 128);
        var key = (kind, normalizedSize);
        if (IconCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var definition = Definitions[kind];
        var icon = CreateIcon(definition.Draw, definition.Color, normalizedSize);
        IconCache[key] = icon;
        return icon;
    }

    private static Bitmap CreateIcon(Action<Graphics, Rectangle, Color> draw, Color color, int imageSize)
    {
        var bitmap = new Bitmap(imageSize, imageSize, PixelFormat.Format32bppPArgb);
        bitmap.SetResolution(96, 96);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.ScaleTransform(imageSize / (float)LogicalCanvasSize, imageSize / (float)LogicalCanvasSize);
        draw(graphics, new Rectangle(LogicalContentInset, LogicalContentInset, LogicalContentSize, LogicalContentSize), color);
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

    private static void DrawExit(Graphics graphics, Rectangle bounds, Color color)
    {
        using var pen = new Pen(color, 2.2F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        var door = new Rectangle(bounds.Left + 3, bounds.Top + 4, 9, bounds.Height - 8);
        graphics.DrawRectangle(pen, door);
        graphics.DrawLine(pen, bounds.Left + 12, bounds.Top + 8, bounds.Right - 5, bounds.Top + 8);
        graphics.DrawLine(pen, bounds.Right - 8, bounds.Top + 5, bounds.Right - 4, bounds.Top + 8);
        graphics.DrawLine(pen, bounds.Right - 8, bounds.Top + 11, bounds.Right - 4, bounds.Top + 8);
        graphics.DrawLine(pen, bounds.Left + 9, bounds.Top + 12, bounds.Left + 9, bounds.Top + 12);
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

    private sealed record IconDefinition(Action<Graphics, Rectangle, Color> Draw, Color Color);
}
