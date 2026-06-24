namespace ScreenApp.Diagnostics;

/// <summary>
/// Общий журнал событий приложения: статусы подключений, полученные донаты, запуск
/// эффектов. Пишут сюда оба слушателя (DonationAlerts, DonateX) и координатор эффектов;
/// читает окно «Диагностика и помощь». Потокобезопасен.
/// </summary>
public sealed class EventJournal
{
    private readonly object _gate = new();
    private readonly Queue<string> _lines = new();
    private const int MaxLines = 300;

    /// <summary>Добавлена строка (для живого обновления окна диагностики).</summary>
    public event Action<string>? Appended;

    /// <summary>Добавить строку с отметкой времени.</summary>
    public void Append(string message)
    {
        string line = $"{DateTime.Now:HH:mm:ss}  {message}";
        lock (_gate)
        {
            _lines.Enqueue(line);
            while (_lines.Count > MaxLines)
            {
                _lines.Dequeue();
            }
        }
        Appended?.Invoke(line);
    }

    /// <summary>Снимок журнала.</summary>
    public IReadOnlyList<string> Snapshot()
    {
        lock (_gate)
        {
            return _lines.ToArray();
        }
    }
}
