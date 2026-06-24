using System.IO;
using System.Text.Json;

namespace ScreenApp.Settings;

/// <summary>
/// Загрузка и сохранение <see cref="AppSettings"/> в JSON-файл в каталоге пользователя
/// (%AppData%\ScreenApp\settings.json). Всё хранится локально на ПК, где запущено
/// приложение — никакого сайта.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private readonly string _path;

    /// <summary>Путь к файлу настроек по умолчанию.</summary>
    public static string DefaultPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ScreenApp",
        "settings.json");

    public SettingsStore(string? path = null)
    {
        _path = path ?? DefaultPath;
    }

    /// <summary>Полный путь к файлу настроек.</summary>
    public string Path => _path;

    /// <summary>
    /// Загрузить настройки. Если файла нет или он повреждён — вернуть значения
    /// по умолчанию (приложение всегда запускается, настройки правятся в окне).
    /// </summary>
    public AppSettings Load()
    {
        AppSettings settings;
        try
        {
            if (File.Exists(_path))
            {
                string json = File.ReadAllText(_path);
                settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                           ?? new AppSettings();
            }
            else
            {
                settings = new AppSettings();
            }
        }
        catch (Exception)
        {
            settings = new AppSettings();
        }

        settings.SyncWithCatalog();
        return settings;
    }

    /// <summary>Сохранить настройки на диск (создаёт каталог при необходимости).</summary>
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string dir = System.IO.Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(dir);

        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_path, json);
    }
}
