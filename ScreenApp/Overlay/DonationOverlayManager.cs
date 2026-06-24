using System.Diagnostics;
using ScreenApp.Donations;
using ScreenApp.Effects;

namespace ScreenApp.Overlay;

/// <summary>
/// Очередь подписей оверлея: показывает сообщения по одному, без визуального наложения (R5.5).
///
/// Новые подписи добавляются в очередь (потокобезопасно из любого потока); показ всегда
/// идёт в UI-потоке через <see cref="IUiInvoker"/>. Пока проигрывается текущая подпись,
/// следующая ждёт; по завершении анимации (fade out) показывается следующая из очереди.
///
/// Окно (<see cref="IOverlayView"/>, по умолчанию <see cref="DonationOverlay"/>) создаётся
/// лениво при первом показе через фабрику и переиспользуется. Фабрика инъецируется,
/// чтобы логику очереди можно было покрыть unit-тестами без живого WPF-окна.
/// </summary>
public sealed class DonationOverlayManager : IDisposable
{
    private readonly IUiInvoker _ui;
    private readonly Func<IOverlayView> _viewFactory;
    private readonly double _holdSeconds;
    private readonly object _gate = new();
    private readonly Queue<OverlayMessage> _queue = new();

    private IOverlayView? _view;
    private bool _showing;
    private bool _disposed;

    /// <param name="ui">Маршалинг в UI-поток (Dispatcher).</param>
    /// <param name="holdSeconds">Сколько секунд держать подпись на экране между fade in и fade out.</param>
    public DonationOverlayManager(IUiInvoker ui, double holdSeconds = 4.0)
        : this(ui, () => new DonationOverlay(), holdSeconds)
    {
    }

    /// <param name="ui">Маршалинг в UI-поток (Dispatcher).</param>
    /// <param name="viewFactory">Фабрика окна оверлея (для тестов — заглушка).</param>
    /// <param name="holdSeconds">Время удержания подписи между fade in и fade out.</param>
    public DonationOverlayManager(IUiInvoker ui, Func<IOverlayView> viewFactory, double holdSeconds = 4.0)
    {
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _viewFactory = viewFactory ?? throw new ArgumentNullException(nameof(viewFactory));
        _holdSeconds = holdSeconds;
    }

    /// <summary>Количество подписей, ожидающих показа (для диагностики/тестов).</summary>
    public int QueuedCount
    {
        get
        {
            lock (_gate)
            {
                return _queue.Count;
            }
        }
    }

    /// <summary>Поставить подпись доната в очередь показа.</summary>
    public void ShowDonation(Donation donation, string actionLabel)
    {
        ArgumentNullException.ThrowIfNull(donation);
        Enqueue(OverlayMessage.FromDonation(donation, actionLabel));
    }

    /// <summary>Поставить произвольную подпись «ник — сумма — действие» в очередь.</summary>
    public void Enqueue(string viewer, string amountText, string shortLabel)
    {
        string line = OverlayFormatter.FormatLine(viewer, amountText, shortLabel);
        Enqueue(new OverlayMessage(line));
    }

    private void Enqueue(OverlayMessage message)
    {
        if (_disposed)
        {
            return;
        }

        lock (_gate)
        {
            _queue.Enqueue(message);
            if (_showing)
            {
                return; // текущий показ сам заберёт следующий по завершении
            }

            _showing = true;
        }

        _ui.Invoke(ShowNext);
    }

    /// <summary>Показать следующую подпись из очереди (выполняется в UI-потоке).</summary>
    private void ShowNext()
    {
        OverlayMessage? next;
        lock (_gate)
        {
            if (_queue.Count == 0)
            {
                _showing = false;
                return;
            }

            next = _queue.Dequeue();
        }

        try
        {
            _view ??= _viewFactory();
            _view.ShowMessage(next!.Text, _holdSeconds, OnMessageFinished);
        }
        catch (Exception ex)
        {
            // Сбой показа не должен останавливать очередь — двигаемся дальше.
            Debug.WriteLine($"[DonationOverlayManager] show failed: {ex.Message}");
            OnMessageFinished();
        }
    }

    /// <summary>Колбэк завершения анимации одной подписи: показать следующую или остановиться.</summary>
    private void OnMessageFinished()
    {
        bool hasMore;
        lock (_gate)
        {
            hasMore = _queue.Count > 0;
            if (!hasMore)
            {
                _showing = false;
            }
        }

        if (hasMore)
        {
            // Уже в UI-потоке (колбэк storyboard.Completed), но Invoke безопасен.
            _ui.Invoke(ShowNext);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        lock (_gate)
        {
            _queue.Clear();
            _showing = false;
        }

        if (_view is not null)
        {
            var view = _view;
            _view = null;
            _ui.Invoke(() =>
            {
                try
                {
                    view.Close();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DonationOverlayManager] close failed: {ex.Message}");
                }
            });
        }
    }
}
