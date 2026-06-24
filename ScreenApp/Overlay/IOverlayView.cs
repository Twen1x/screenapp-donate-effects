namespace ScreenApp.Overlay;

/// <summary>
/// Абстракция окна оверлея для <see cref="DonationOverlayManager"/>.
/// Позволяет тестировать логику очереди (R5.5) без живого WPF-окна.
/// Реальная реализация — <see cref="DonationOverlay"/>.
/// </summary>
public interface IOverlayView
{
    /// <summary>
    /// Показать подпись: fade in → удержание holdSeconds → fade out. По завершении
    /// анимации вызвать <paramref name="onFinished"/> (в UI-потоке).
    /// </summary>
    void ShowMessage(string text, double holdSeconds, Action onFinished);

    /// <summary>Закрыть окно и освободить ресурсы.</summary>
    void Close();
}
