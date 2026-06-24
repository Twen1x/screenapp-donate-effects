using System.Windows;
using DrawingRectangle = System.Drawing.Rectangle;
using Screen = System.Windows.Forms.Screen;

namespace ScreenApp.Effects;

/// <summary>
/// Целевой монитор для эффектов. Эффекты-окна должны открываться на ОДНОМ выбранном
/// мониторе, а не на всём виртуальном рабочем столе (иначе перекрывают оба экрана).
///
/// Хранит выбранный монитор по его <see cref="Screen.DeviceName"/> (стабильнее индекса).
/// Отдаёт границы в двух системах координат:
///  - <see cref="PhysicalBounds"/> — физические пиксели (для захвата экрана CopyFromScreen);
///  - <see cref="GetDipBounds"/> — DIP-координаты WPF (для размещения окон).
///
/// Перевод физических пикселей в DIP делается по масштабу основного монитора; для
/// типичного случая (одинаковый масштаб на всех мониторах) это точно.
/// </summary>
public static class EffectTarget
{
    /// <summary>DeviceName выбранного монитора; null/empty — основной монитор.</summary>
    public static string? DeviceName { get; set; }

    /// <summary>Выбранный монитор (или основной, если выбранный недоступен).</summary>
    public static Screen GetScreen()
    {
        var screens = Screen.AllScreens;
        if (!string.IsNullOrEmpty(DeviceName))
        {
            var match = screens.FirstOrDefault(s => s.DeviceName == DeviceName);
            if (match is not null)
            {
                return match;
            }
        }

        return Screen.PrimaryScreen ?? screens[0];
    }

    /// <summary>Физические границы целевого монитора (для захвата экрана).</summary>
    public static DrawingRectangle PhysicalBounds => GetScreen().Bounds;

    /// <summary>Масштаб основного монитора (физ. пиксели / DIP).</summary>
    private static double PrimaryScale()
    {
        var primary = Screen.PrimaryScreen;
        if (primary is null || SystemParameters.PrimaryScreenWidth <= 0)
        {
            return 1.0;
        }

        return primary.Bounds.Width / SystemParameters.PrimaryScreenWidth;
    }

    /// <summary>Границы целевого монитора в DIP-координатах WPF (для Left/Top/Width/Height окна).</summary>
    public static (double Left, double Top, double Width, double Height) GetDipBounds()
    {
        var b = GetScreen().Bounds;
        double scale = PrimaryScale();
        if (scale <= 0)
        {
            scale = 1.0;
        }

        return (b.X / scale, b.Y / scale, b.Width / scale, b.Height / scale);
    }
}
