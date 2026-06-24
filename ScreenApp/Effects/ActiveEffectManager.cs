using System.Diagnostics;
using ScreenApp.Actions;
// UseWindowsForms делает имя Timer неоднозначным (Forms.Timer vs Threading.Timer).
// Здесь нужен потоковый таймер автоснятия эффектов.
using Timer = System.Threading.Timer;

namespace ScreenApp.Effects;

/// <summary>
/// Учёт активных эффектов и гарантия их снятия.
///
/// Обязанности (R4.2, R4.5):
///  - применить действие (Execute) в UI-потоке;
///  - запланировать автоснятие (Revert) по таймеру через duration_seconds;
///  - поддерживать несколько одновременных независимых эффектов, снимая каждый отдельно;
///  - <see cref="RevertAll"/> — мгновенно снять всё (используется аварийным стоп-краном, задача 13).
///
/// Потокобезопасность: словарь активных эффектов защищён локом; Execute/Revert
/// маршалятся в UI-поток через <see cref="IUiInvoker"/>.
/// </summary>
public sealed class ActiveEffectManager : IDisposable
{
    private readonly IUiInvoker _ui;
    private readonly object _gate = new();
    private readonly Dictionary<long, ActiveEffect> _active = new();
    private long _nextId;
    private bool _disposed;

    public ActiveEffectManager(IUiInvoker ui)
    {
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
    }

    /// <summary>Количество активных эффектов (для диагностики/тестов).</summary>
    public int ActiveCount
    {
        get
        {
            lock (_gate)
            {
                return _active.Count;
            }
        }
    }

    /// <summary>
    /// Применить действие и зарегистрировать его. Если durationSeconds &gt; 0 —
    /// запланировать автоснятие по таймеру. Возвращает идентификатор активного эффекта.
    /// Исключения из Execute пробрасываются вызывающему (он пометит задание ошибкой).
    /// </summary>
    public long Activate(IScreenAction action, int durationSeconds)
    {
        ArgumentNullException.ThrowIfNull(action);
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Применяем эффект в UI-потоке. Ошибку прокидываем наружу до регистрации.
        InvokeOnUi(action.Execute);

        long id = Interlocked.Increment(ref _nextId);
        var effect = new ActiveEffect(id, action);

        lock (_gate)
        {
            _active[id] = effect;
        }

        if (durationSeconds > 0)
        {
            // Timer callback приходит в пуле потоков; снятие маршалится в UI внутри RevertById.
            effect.Timer = new Timer(
                _ => RevertById(id),
                null,
                TimeSpan.FromSeconds(durationSeconds),
                Timeout.InfiniteTimeSpan);
        }

        return id;
    }

    /// <summary>Снять конкретный эффект по идентификатору (идемпотентно).</summary>
    public void RevertById(long id)
    {
        ActiveEffect? effect;
        lock (_gate)
        {
            if (!_active.Remove(id, out effect))
            {
                return; // уже снят
            }
        }

        SafeRevert(effect!);
    }

    /// <summary>
    /// Снять все активные эффекты (аварийный стоп-кран, R4.4). Идемпотентно.
    /// </summary>
    public void RevertAll()
    {
        List<ActiveEffect> snapshot;
        lock (_gate)
        {
            snapshot = _active.Values.ToList();
            _active.Clear();
        }

        foreach (var effect in snapshot)
        {
            SafeRevert(effect);
        }
    }

    private void SafeRevert(ActiveEffect effect)
    {
        effect.Timer?.Dispose();
        effect.Timer = null;

        try
        {
            InvokeOnUi(effect.Action.Revert);
        }
        catch (Exception ex)
        {
            // Снятие не должно валить процесс — логируем и продолжаем.
            Debug.WriteLine($"[ActiveEffectManager] Revert '{effect.Action.ActionId}' failed: {ex.Message}");
        }
    }

    private void InvokeOnUi(Action action) => _ui.Invoke(action);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        RevertAll();
    }

    /// <summary>Внутреннее состояние одного активного эффекта.</summary>
    private sealed class ActiveEffect
    {
        public ActiveEffect(long id, IScreenAction action)
        {
            Id = id;
            Action = action;
        }

        public long Id { get; }
        public IScreenAction Action { get; }
        public Timer? Timer { get; set; }
    }
}
