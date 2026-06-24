using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ScreenApp.Effects;
using ScreenApp.Interop;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingRectangle = System.Drawing.Rectangle;
using WpfImage = System.Windows.Controls.Image;

namespace ScreenApp.Actions;

/// <summary>
/// Базовое действие «живой захват экрана с трансформацией»: полноэкранное topmost
/// borderless-окно, которое ~30 раз в секунду снимает первичный экран и показывает его
/// с заданной трансформацией (зеркало, поворот, диско).
///
/// === Почему окно исключается из захвата (фикс мерцания) ===
/// CopyFromScreen снимает весь экран, включая наше же окно эффекта. Если этого не
/// предотвратить — образуется петля «отзеркалил → снял уже зеркальный кадр → отзеркалил
/// обратно», экран мигает туда-сюда. Решение: SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)
/// (Windows 10 2004+) — окно эффекта не попадает в захват, поэтому каждый кадр берётся
/// чистый рабочий стол, а трансформация применяется стабильно (1 раз).
///
/// === Почему поворот тоже здесь, а не через ChangeDisplaySettingsEx ===
/// Аппаратный поворот монитора крутит ВЕСЬ кадр, включая оверлей доната (он оказывался
/// вверх ногами). Программный поворот через захват рисует перевёрнутую картинку в окне,
/// а оверлей (исключённый из захвата и поднятый поверх) остаётся ровным.
///
/// Все операции с окном/таймером выполняются в UI-потоке (гарантирует ActiveEffectManager).
/// </summary>
public abstract class CaptureEffectAction : IScreenAction
{
    /// <summary>Частота обновления кадра (~30 fps).</summary>
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(33);

    private Window? _window;
    private WpfImage? _image;
    private DispatcherTimer? _timer;

    private Bitmap? _captureBitmap;
    private DrawingGraphics? _captureGraphics;
    private WriteableBitmap? _writeable;

    private int _captureX;
    private int _captureY;
    private int _captureWidth;
    private int _captureHeight;
    private long _frame;

    public abstract string ActionId { get; }

    /// <summary>Изображение, к которому применяется трансформация (доступно подклассам, напр. диско).</summary>
    protected WpfImage? Image => _image;

    /// <summary>Построить начальную трансформацию кадра. По умолчанию — без изменений.</summary>
    protected virtual Transform BuildTransform() => Transform.Identity;

    /// <summary>
    /// Вызывается каждый кадр (после захвата). Номер кадра растёт на 1 за тик (~33 мс).
    /// Подклассы (диско) переопределяют, чтобы менять трансформацию во времени.
    /// </summary>
    protected virtual void OnFrame(long frame) { }

    public void Execute()
    {
        if (_window is not null)
        {
            return;
        }

        var bounds = EffectTarget.PhysicalBounds;
        _captureX = bounds.X;
        _captureY = bounds.Y;
        _captureWidth = Math.Max(1, bounds.Width);
        _captureHeight = Math.Max(1, bounds.Height);

        _captureBitmap = new Bitmap(_captureWidth, _captureHeight, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        _captureGraphics = DrawingGraphics.FromImage(_captureBitmap);
        _writeable = new WriteableBitmap(_captureWidth, _captureHeight, 96, 96, PixelFormats.Pbgra32, null);

        _image = new WpfImage
        {
            Source = _writeable,
            Stretch = Stretch.Fill,
            RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
            RenderTransform = BuildTransform(),
        };

        var (left, top, width, height) = EffectTarget.GetDipBounds();
        _window = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = false,
            Background = System.Windows.Media.Brushes.Black,
            Topmost = true,
            ShowInTaskbar = false,
            ShowActivated = false,
            Content = _image,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = left,
            Top = top,
            Width = width,
            Height = height,
        };

        _window.SourceInitialized += OnSourceInitialized;
        _window.Show();

        _frame = 0;
        CaptureFrame();

        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = FrameInterval };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        // Исключаем окно эффекта из захвата — иначе петля и мерцание.
        NativeMethods.SetWindowDisplayAffinity(hwnd, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        CaptureFrame();
        _frame++;
        OnFrame(_frame);
    }

    private void CaptureFrame()
    {
        if (_captureGraphics is null || _captureBitmap is null || _writeable is null)
        {
            return;
        }

        _captureGraphics.CopyFromScreen(
            _captureX, _captureY, 0, 0,
            new System.Drawing.Size(_captureWidth, _captureHeight),
            CopyPixelOperation.SourceCopy);

        var rect = new DrawingRectangle(0, 0, _captureWidth, _captureHeight);
        BitmapData? data = null;
        try
        {
            data = _captureBitmap.LockBits(rect, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            _writeable.Lock();
            _writeable.WritePixels(
                new Int32Rect(0, 0, _captureWidth, _captureHeight),
                data.Scan0, data.Stride * _captureHeight, data.Stride);
            _writeable.Unlock();
        }
        finally
        {
            if (data is not null)
            {
                _captureBitmap.UnlockBits(data);
            }
        }
    }

    public void Revert()
    {
        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
            _timer = null;
        }

        if (_window is not null)
        {
            _window.SourceInitialized -= OnSourceInitialized;
            _window.Close();
            _window = null;
        }

        _image = null;
        _writeable = null;

        _captureGraphics?.Dispose();
        _captureGraphics = null;

        _captureBitmap?.Dispose();
        _captureBitmap = null;
    }
}
