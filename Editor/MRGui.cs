using System.Numerics;
using Raylib_cs;

namespace MissingRed.Editor;

/// <summary>
/// Immediate-Mode GUI Framework super ringan khusus untuk MissingRed Studio Editor.
/// Bebas overhead memori dan terintegrasi langsung dengan Raylib Batch Renderer.
/// </summary>
public static class MRGui
{
    public static readonly Color BgDark = new(17, 19, 24, 255);
    public static readonly Color PanelBg = new(25, 28, 36, 255);
    public static readonly Color PanelHeader = new(34, 38, 49, 255);
    public static readonly Color BorderCol = new(52, 58, 74, 255);
    public static readonly Color AccentRed = new(220, 52, 68, 255);
    public static readonly Color AccentGreen = new(46, 175, 95, 255);
    public static readonly Color TextLight = new(230, 234, 242, 255);
    public static readonly Color TextMuted = new(145, 152, 170, 255);

    public static void DrawPanel(Rectangle rect, string title)
    {
        Raylib.DrawRectangleRec(rect, PanelBg);
        Rectangle headerRect = new(rect.X, rect.Y, rect.Width, 26);
        Raylib.DrawRectangleRec(headerRect, PanelHeader);
        Raylib.DrawRectangle((int)rect.X, (int)rect.Y, 3, 26, AccentRed);
        Raylib.DrawText(title, (int)rect.X + 10, (int)rect.Y + 6, 14, TextLight);
        Raylib.DrawRectangleLinesEx(rect, 1, BorderCol);
    }

    public static bool Button(Rectangle rect, string label, Color? customColor = null)
    {
        Vector2 mouse = Raylib.GetMousePosition();
        bool hovered = Raylib.CheckCollisionPointRec(mouse, rect);
        bool clicked = hovered && Raylib.IsMouseButtonPressed(MouseButton.Left);

        Color baseCol = customColor ?? new Color(44, 49, 62, 255);
        if (hovered)
        {
            baseCol = new Color(
                (byte)Math.Min(255, baseCol.R + 25),
                (byte)Math.Min(255, baseCol.G + 25),
                (byte)Math.Min(255, baseCol.B + 25),
                (byte)255);
        }

        Raylib.DrawRectangleRec(rect, baseCol);
        Raylib.DrawRectangleLinesEx(rect, 1, hovered ? AccentRed : BorderCol);

        int textW = Raylib.MeasureText(label, 13);
        int tx = (int)(rect.X + (rect.Width - textW) * 0.5f);
        int ty = (int)(rect.Y + (rect.Height - 13) * 0.5f);
        Raylib.DrawText(label, tx, ty, 13, TextLight);

        return clicked;
    }

    public static bool ToggleButton(Rectangle rect, string label, ref bool state)
    {
        Color col = state ? new Color(165, 42, 56, 255) : new Color(38, 42, 52, 255);
        if (Button(rect, state ? $"[ON] {label}" : $"[OFF] {label}", col))
        {
            state = !state;
            return true;
        }
        return false;
    }

    public static float SliderFloat(Rectangle rect, string label, float value, float min, float max, Color barColor)
    {
        Vector2 mouse = Raylib.GetMousePosition();
        bool hovered = Raylib.CheckCollisionPointRec(mouse, rect);

        if (hovered && Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            float t = Math.Clamp((mouse.X - rect.X) / rect.Width, 0f, 1f);
            value = min + t * (max - min);
        }

        // Scroll halus dengan roda mouse saat hover di atas slider
        if (hovered)
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (MathF.Abs(wheel) > 0.01f)
            {
                float step = (max - min) * 0.02f;
                value = Math.Clamp(value + wheel * step, min, max);
            }
        }

        Raylib.DrawRectangleRec(rect, new Color(18, 20, 26, 255));
        float fillRatio = Math.Clamp((value - min) / MathF.Max(0.0001f, max - min), 0f, 1f);
        Raylib.DrawRectangle((int)rect.X, (int)rect.Y, (int)(rect.Width * fillRatio), (int)rect.Height, barColor);
        Raylib.DrawRectangleLinesEx(rect, 1, hovered ? TextLight : BorderCol);

        string txt = $"{label}: {value:0.00}";
        Raylib.DrawText(txt, (int)rect.X + 6, (int)rect.Y + 4, 12, TextLight);

        return value;
    }

    public static byte SliderByte(Rectangle rect, string label, byte value, Color barColor)
    {
        float v = SliderFloat(rect, label, value, 0f, 255f, barColor);
        return (byte)Math.Clamp((int)MathF.Round(v), 0, 255);
    }
}
