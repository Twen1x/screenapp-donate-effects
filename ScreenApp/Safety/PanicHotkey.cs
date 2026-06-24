using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using ScreenApp.Interop;
using WinFormsKeys = System.Windows.Forms.Keys;

namespace ScreenApp.Safety;

/// <summary>
/// Аварийный стоп-кран (R4.4): глобальная горячая клавиша, которая мгновенно снимает
/// ВСЕ активные эффекты, даже когда заблокированы мышь и клавиатура. Системный
/// RegisterHotKey работает на уровне ОС и продолжает срабатывать, пока установлены
/// низкоуровневые хуки ввода (они не получают комбинацию первыми).
///
/// Реализация: создаём скрытое message-only окно через <see cref="HwndSource"/>,
/// регистрируем на его дескрипторе хоткей и слушаем WM_HOTKEY в перехватчике WndProc.
/// При срабатывании вызываем переданный callback (обычно ActiveEffectManager.RevertAll).
///
/// ВАЖНО: создавать экземпляр нужно в UI-потоке WPF — <see cref="HwndSource"/> и насос
/// сообщений живут в нём. Dispose снимает регистрацию и освобождает окно.
/// </summary>
public sealed class PanicHotkey : IDisposable
{
    // Произвольный идентификатор хоткея в пределах окна.
    private const int HotKeyId = 0xB001;

    private readonly Action _onPanic;
    private readonly HwndSource _source;
    private readonly IntPtr _hwnd;
    private bool _registered;
    private bool _disposed;

    /// <summary>
    /// Зарегистрировать аварийную горячую клавишу.
    /// </summary>
    /// <param name="hotkey">Строка комбинации, например "Ctrl+Alt+End".</param>
    /// <param name="onPanic">Колбэк, вызываемый при нажатии комбинации.</param>
    public PanicHotkey(string? hotkey, Action onPanic)
    {
        _onPanic = onPanic ?? throw new ArgumentNullException(nameof(onPanic));

        (uint modifiers, uint vk) = Parse(hotkey);

        // Скрытое message-only окно для приёма WM_HOTKEY.
        var parameters = new HwndSourceParameters("ScreenApp.PanicHotkey")
        {
            WindowStyle = 0,
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE
        };
        _source = new HwndSource(parameters);
        _hwnd = _source.Handle;
        _source.AddHook(WndProc);

        _registered = NativeMethods.RegisterHotKey(_hwnd, HotKeyId, modifiers, vk);
        if (!_registered)
        {
            int err = Marshal.GetLastWin32Error();
            Debug.WriteLine(
                $"[PanicHotkey] RegisterHotKey не удался для '{hotkey}'. Win32 error {err}. " +
                "Возможно, комбинация уже занята другим приложением.");
        }
    }

    /// <summary>True, если горячая клавиша успешно зарегистрирована в системе.</summary>
    public bool IsRegistered => _registered;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotKeyId)
        {
            handled = true;
            try
            {
                _onPanic();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PanicHotkey] ошибка в обработчике стоп-крана: {ex.Message}");
            }
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Разобрать строку комбинации (например "Ctrl+Alt+End") в модификаторы и vk-код.
    /// При ошибке возвращает дефолт Ctrl+Alt+End. Всегда добавляет MOD_NOREPEAT,
    /// чтобы удержание не порождало шквал сообщений.
    /// </summary>
    private static (uint Modifiers, uint Vk) Parse(string? hotkey)
    {
        const uint defaultMods = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT;
        const uint defaultVk = (uint)WinFormsKeys.End;

        if (string.IsNullOrWhiteSpace(hotkey))
        {
            return (defaultMods, defaultVk);
        }

        uint modifiers = NativeMethods.MOD_NOREPEAT;
        uint vk = 0;

        string[] parts = hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string raw in parts)
        {
            string part = raw.Trim();
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= NativeMethods.MOD_CONTROL;
                    break;
                case "alt":
                    modifiers |= NativeMethods.MOD_ALT;
                    break;
                case "shift":
                    modifiers |= NativeMethods.MOD_SHIFT;
                    break;
                case "win":
                case "windows":
                case "meta":
                    modifiers |= NativeMethods.MOD_WIN;
                    break;
                default:
                    // Клавиша (не модификатор) — пытаемся распознать как vk-код.
                    if (TryParseKey(part, out uint parsedVk))
                    {
                        vk = parsedVk;
                    }
                    break;
            }
        }

        // Без основной клавиши или без модификаторов комбинация бессмысленна — дефолт.
        bool hasModifier = (modifiers & ~NativeMethods.MOD_NOREPEAT) != 0;
        if (vk == 0 || !hasModifier)
        {
            return (defaultMods, defaultVk);
        }

        return (modifiers, vk);
    }

    /// <summary>
    /// Преобразовать имя клавиши в виртуальный код. Опираемся на
    /// <see cref="System.Windows.Forms.Keys"/> (числовое значение enum совпадает с vk-кодом),
    /// с поддержкой одиночных букв/цифр и распространённых псевдонимов.
    /// </summary>
    private static bool TryParseKey(string name, out uint vk)
    {
        vk = 0;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        // Одиночная цифра 0-9 → Keys.D0..Keys.D9.
        if (name.Length == 1 && char.IsDigit(name[0]))
        {
            vk = (uint)(WinFormsKeys.D0 + (name[0] - '0'));
            return true;
        }

        // Одиночная буква A-Z.
        if (name.Length == 1 && char.IsLetter(name[0]))
        {
            vk = (uint)(WinFormsKeys.A + (char.ToUpperInvariant(name[0]) - 'A'));
            return true;
        }

        // Распространённые псевдонимы.
        switch (name.ToLowerInvariant())
        {
            case "esc":
                vk = (uint)WinFormsKeys.Escape;
                return true;
            case "del":
                vk = (uint)WinFormsKeys.Delete;
                return true;
            case "ins":
                vk = (uint)WinFormsKeys.Insert;
                return true;
            case "pgup":
                vk = (uint)WinFormsKeys.PageUp;
                return true;
            case "pgdn":
            case "pgdown":
                vk = (uint)WinFormsKeys.PageDown;
                return true;
            case "break":
                vk = (uint)WinFormsKeys.Pause;
                return true;
        }

        // Остальное (End, Home, F1..F12, Space, Enter, Tab, и т.п.) — через Enum.TryParse.
        if (Enum.TryParse<WinFormsKeys>(name, ignoreCase: true, out var key))
        {
            vk = (uint)key;
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_registered)
        {
            NativeMethods.UnregisterHotKey(_hwnd, HotKeyId);
            _registered = false;
        }

        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
