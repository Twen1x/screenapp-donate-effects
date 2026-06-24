using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using ScreenApp.Interop;

namespace ScreenApp.Overlay;

/// <summary>
/// Окно оверлея донатов: borderless, прозрачное, topmost, не активируемое и «сквозное»
/// (click-through), расположенное внизу по центру основного экрана (R5.1).
///
/// Показывает ОДНУ подпись за раз с плавным появлением/исчезновением (R5.4):
/// fade in → удержание → fade out, после чего вызывает колбэк завершения.
/// Очередь и отсутствие наложения обеспечивает <see cref="DonationOverlayManager"/> (R5.5).
///
/// Все публичные методы должны вызываться в UI-потоке.
/// </summary>
public partial class DonationOverlay : Window, IOverlayView
{
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(400);

    private Action? _onFinished;
    private bool _animating;

    public DonationOverlay()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyClickThrough();
        Reposition();
    }

    /// <summary>
    /// Поднять оверлей поверх окон эффектов и исключить его из захвата экрана, чтобы
    /// capture-эффекты (зеркало/поворот/диско) его «не съедали» и не переворачивали.
    /// </summary>
    private void EnsureOnTopAndExcludedFromCapture()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        // Не попадаем в CopyFromScreen эффектов → оверлей не дублируется/не переворачивается.
        NativeMethods.SetWindowDisplayAffinity(hwnd, NativeMethods.WDA_EXCLUDEFROMCAPTURE);

        // Перетолкнуть окно на самый верх z-порядка (поверх topmost-окон эффектов),
        // не активируя и не воруя фокус.
        Topmost = false;
        Topmost = true;
    }

    /// <summary>
    /// Показать подпись: задаёт текст, перепозиционирует окно по центру снизу и
    /// запускает анимацию fade in → hold(holdSeconds) → fade out. По завершении
    /// вызывает <paramref name="onFinished"/> (в UI-потоке).
    /// </summary>
    public void ShowMessage(string text, double holdSeconds, Action onFinished)
    {
        _onFinished = onFinished;
        MessageText.Text = text;

        if (!IsVisible)
        {
            Show();
        }

        // Каждый показ поднимаем оверлей поверх окон эффектов и исключаем из захвата.
        EnsureOnTopAndExcludedFromCapture();

        // Пересчитать позицию после обновления размера под новый текст.
        Dispatcher.BeginInvoke(new Action(Reposition),
            System.Windows.Threading.DispatcherPriority.Loaded);

        StartAnimation(Math.Max(0.5, holdSeconds));
    }

    private void StartAnimation(double holdSeconds)
    {
        _animating = true;

        var storyboard = new Storyboard();

        // Fade in.
        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(FadeDuration),
            BeginTime = TimeSpan.Zero,
        };

        // Fade out — после удержания.
        var fadeOut = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = new Duration(FadeDuration),
            BeginTime = FadeDuration + TimeSpan.FromSeconds(holdSeconds),
        };

        Storyboard.SetTarget(fadeIn, Plate);
        Storyboard.SetTargetProperty(fadeIn, new PropertyPath(OpacityProperty));
        Storyboard.SetTarget(fadeOut, Plate);
        Storyboard.SetTargetProperty(fadeOut, new PropertyPath(OpacityProperty));

        storyboard.Children.Add(fadeIn);
        storyboard.Children.Add(fadeOut);

        storyboard.Completed += OnStoryboardCompleted;
        storyboard.Begin();
    }

    private void OnStoryboardCompleted(object? sender, EventArgs e)
    {
        _animating = false;
        Plate.Opacity = 0;

        var cb = _onFinished;
        _onFinished = null;
        cb?.Invoke();
    }

    /// <summary>Расположить окно внизу по центру выбранного монитора эффектов.</summary>
    private void Reposition()
    {
        var (left, top, screenW, screenH) = ScreenApp.Effects.EffectTarget.GetDipBounds();
        double width = ActualWidth > 0 ? ActualWidth : Width;
        double height = ActualHeight > 0 ? ActualHeight : Height;

        Left = left + (screenW - width) / 2;
        // Небольшой отступ снизу.
        Top = top + screenH - height - 80;
    }

    /// <summary>
    /// Сделать окно «сквозным» для мыши и не активируемым: добавляем стили
    /// WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE, чтобы оверлей не перехватывал
    /// клики и не воровал фокус у полноэкранного контента.
    /// </summary>
    private void ApplyClickThrough()
    {
        var helper = new WindowInteropHelper(this);
        IntPtr hwnd = helper.Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        exStyle |= NativeMethods.WS_EX_TRANSPARENT
                   | NativeMethods.WS_EX_LAYERED
                   | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);
    }

    /// <summary>True, пока проигрывается анимация показа (для диагностики/тестов).</summary>
    public bool IsAnimating => _animating;
}
