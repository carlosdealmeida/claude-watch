using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using ClaudeWatch.Core;

namespace ClaudeWatch.Tray;

public static class IconRenderer
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Render(Meter worst)
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; // ClearType não renderiza sobre alpha

        var color = ColorTranslator.FromHtml(ZoneColors.Hex(worst.Zone));
        var text = worst.Pct.ToString();
        using var font = new Font("Segoe UI", worst.Pct >= 100 ? 14f : 19f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        var size = g.MeasureString(text, font);
        g.DrawString(text, font, brush, (32 - size.Width) / 2f, (32 - size.Height) / 2f);

        var hIcon = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(hIcon);
            return (Icon)tmp.Clone(); // cópia gerenciada independente do handle nativo
        }
        finally
        {
            DestroyIcon(hIcon); // sem isso: +1 GDI handle por refresh → morte em dias
        }
    }
}
