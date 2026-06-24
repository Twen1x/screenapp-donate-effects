using Screen = System.Windows.Forms.Screen;

namespace ScreenApp.UI;

/// <summary>
/// Описание монитора для выпадающего списка в настройках: понятное имя и DeviceName
/// (его и сохраняем в настройки как целевой монитор для эффектов).
/// </summary>
public sealed class MonitorInfo
{
    public required string DeviceName { get; init; }
    public required string DisplayName { get; init; }

    public override string ToString() => DisplayName;

    /// <summary>Построить список всех подключённых мониторов.</summary>
    public static List<MonitorInfo> All()
    {
        var list = new List<MonitorInfo>();
        var screens = Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            var s = screens[i];
            string primary = s.Primary ? " — основной" : "";
            list.Add(new MonitorInfo
            {
                DeviceName = s.DeviceName,
                DisplayName = $"Монитор {i + 1}: {s.Bounds.Width}×{s.Bounds.Height}{primary}",
            });
        }
        return list;
    }
}
