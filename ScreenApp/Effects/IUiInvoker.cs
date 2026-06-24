using System.Windows.Threading;

namespace ScreenApp.Effects;

/// <summary>
/// Абстракция маршалинга вызова в UI-поток. Нужна, чтобы оконные действия
/// (создание/закрытие WPF-окон) всегда выполнялись в потоке Dispatcher,
/// а также чтобы <see cref="ActiveEffectManager"/> можно было тестировать
/// без живого Dispatcher (синхронная заглушка).
/// </summary>
public interface IUiInvoker
{
    /// <summary>Выполнить действие синхронно в UI-потоке (блокирует до завершения).</summary>
    void Invoke(Action action);
}

/// <summary>Реализация поверх WPF <see cref="Dispatcher"/>.</summary>
public sealed class DispatcherUiInvoker : IUiInvoker
{
    private readonly Dispatcher _dispatcher;

    public DispatcherUiInvoker(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.Invoke(action);
        }
    }
}
