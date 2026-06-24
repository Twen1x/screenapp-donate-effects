using System.Runtime.InteropServices;

namespace ScreenApp.Interop;

/// <summary>
/// P/Invoke-заготовки WinAPI, нужные действиям приложения (задачи 12–15):
///  - ChangeDisplaySettingsEx — поворот экрана (RotateAction).
///  - SetWindowsHookEx / UnhookWindowsHookEx / CallNextHookEx — блокировки мыши/клавиатуры.
///  - RegisterHotKey / UnregisterHotKey — аварийная горячая клавиша (стоп-кран).
///  - SetWindowLong / GetWindowLong — layered window для затемнения.
/// Реализация действий не входит в каркас; здесь только сигнатуры и константы.
/// </summary>
internal static class NativeMethods
{
    // ---------------------------------------------------------------------
    // Дисплей: поворот / зеркало через ChangeDisplaySettingsEx
    // ---------------------------------------------------------------------

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int ChangeDisplaySettingsEx(
        string? lpszDeviceName,
        ref DEVMODE lpDevMode,
        IntPtr hwnd,
        uint dwflags,
        IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool EnumDisplaySettings(
        string? lpszDeviceName,
        int iModeNum,
        ref DEVMODE lpDevMode);

    // dwflags для ChangeDisplaySettingsEx
    public const uint CDS_UPDATEREGISTRY = 0x00000001;
    public const uint CDS_TEST = 0x00000002;

    // Результаты ChangeDisplaySettingsEx
    public const int DISP_CHANGE_SUCCESSFUL = 0;
    public const int DISP_CHANGE_RESTART = 1;
    public const int DISP_CHANGE_FAILED = -1;

    // dmFields
    public const int DM_DISPLAYORIENTATION = 0x00000080;

    // dmDisplayOrientation
    public const int DMDO_DEFAULT = 0;
    public const int DMDO_90 = 1;
    public const int DMDO_180 = 2;
    public const int DMDO_270 = 3;

    public const int ENUM_CURRENT_SETTINGS = -1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DEVMODE
    {
        private const int CCHDEVICENAME = 32;
        private const int CCHFORMNAME = 32;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
        public string dmDeviceName;
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;

        public int dmPositionX;
        public int dmPositionY;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;

        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHFORMNAME)]
        public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }

    // ---------------------------------------------------------------------
    // Низкоуровневые хуки ввода: блокировка мыши/клавиатуры
    // ---------------------------------------------------------------------

    public const int WH_KEYBOARD_LL = 13;
    public const int WH_MOUSE_LL = 14;

    public delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelHookProc lpfn,
        IntPtr hMod,
        uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    // ---------------------------------------------------------------------
    // Глобальная горячая клавиша: аварийный стоп-кран
    // ---------------------------------------------------------------------

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Модификаторы для RegisterHotKey
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    public const int WM_HOTKEY = 0x0312;

    // Сообщения клавиатуры (wParam в WH_KEYBOARD_LL)
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP = 0x0105;

    /// <summary>Структура данных низкоуровневого хука клавиатуры (lParam в WH_KEYBOARD_LL).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    // Виртуальные коды клавиш-модификаторов (для проверки состояния в хуке клавиатуры).
    public const int VK_CONTROL = 0x11;
    public const int VK_MENU = 0x12;    // Alt
    public const int VK_SHIFT = 0x10;
    public const int VK_LWIN = 0x5B;
    public const int VK_RWIN = 0x5C;

    // ---------------------------------------------------------------------
    // Layered window: затемнение (DimAction)
    // ---------------------------------------------------------------------

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

    // ---------------------------------------------------------------------
    // Исключение окна из захвата экрана (Windows 10 2004+):
    //  - зеркало/поворот/диско захватывают экран через CopyFromScreen; чтобы их
    //    собственное окно не попадало в захват (петля «мерцания»), помечаем его
    //    WDA_EXCLUDEFROMCAPTURE;
    //  - оверлей донатов тоже исключаем, чтобы capture-эффекты его не «съедали».
    // ---------------------------------------------------------------------
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    public const uint WDA_NONE = 0x00000000;
    public const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    // ---------------------------------------------------------------------
    // Системные действия: смена кнопок мыши, mute через эмуляцию клавиш.
    // ---------------------------------------------------------------------
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SwapMouseButton(bool fSwap);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    // Позиция курсора (для огромного курсора, следящего за мышью).
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    public const byte VK_VOLUME_MUTE = 0xAD;
    public const uint KEYEVENTF_KEYUP = 0x0002;

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const uint LWA_ALPHA = 0x00000002;
    public const uint LWA_COLORKEY = 0x00000001;
}
