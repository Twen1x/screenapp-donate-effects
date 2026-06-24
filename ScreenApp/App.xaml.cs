using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using ScreenApp.Actions;
using ScreenApp.Diagnostics;
using ScreenApp.Donations;
using ScreenApp.Effects;
using ScreenApp.Overlay;
using ScreenApp.Safety;
using ScreenApp.Settings;
using ScreenApp.UI;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace ScreenApp;

/// <summary>
/// Точка входа: single instance (Mutex), трей-иконка, окно настроек и прямой слушатель
/// DonationAlerts. Полученные донаты подбираются по цене (<see cref="DonationRouter"/>)
/// и выполняются как эффекты с автоснятием по таймеру и подписью оверлея.
/// </summary>
public partial class App : Application
{
    private const string MutexName = "Global\\ScreenApp_SingleInstance_8E0F7A12";

    private Mutex? _instanceMutex;
    private System.Windows.Forms.NotifyIcon? _trayIcon;

    private AppSettings? _settings;
    private SettingsStore? _store;
    private EventJournal? _journal;
    private DaApi? _api;
    private DonateXApi? _donateXApi;
    private DonationAlertsClient? _client;
    private DonateXClient? _donateX;
    private ActionRegistry? _actionRegistry;
    private ActiveEffectManager? _effectManager;
    private DonationOverlayManager? _overlay;
    private DonationRouter? _router;
    private EffectCoordinator? _coordinator;
    private PanicHotkey? _panicHotkey;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceMutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("ScreenApp уже запущен.", "ScreenApp",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // ---- Настройки (локальные, без сайта) ----
        _store = new SettingsStore();
        _settings = _store.Load();
        _journal = new EventJournal();

        // Целевой монитор для эффектов (пусто — основной).
        EffectTarget.DeviceName = string.IsNullOrEmpty(_settings.TargetMonitorDevice)
            ? null
            : _settings.TargetMonitorDevice;

        // ---- Эффекты, оверлей, маршрутизатор ----
        _actionRegistry = new ActionRegistry();
        _effectManager = new ActiveEffectManager(new DispatcherUiInvoker(Dispatcher));
        _overlay = new DonationOverlayManager(new DispatcherUiInvoker(Dispatcher));
        _router = new DonationRouter(_settings);
        _coordinator = new EffectCoordinator(_actionRegistry, _effectManager, _overlay, _router, _settings,
            s => _journal!.Append(s));

        // ---- Слушатели донатов (DonationAlerts + DonateX) ----
        _api = new DaApi();
        _donateXApi = new DonateXApi();

        _client = new DonationAlertsClient(_settings, _journal, _api, s => _store!.Save(s));
        _client.DonationReceived += OnDonationReceived;
        _client.StatusChanged += (st, m) => OnClientStatus("DonationAlerts", st, m);

        _donateX = new DonateXClient(_settings, _journal, _donateXApi);
        _donateX.DonationReceived += OnDonationReceived;
        _donateX.StatusChanged += (st, m) => OnClientStatus("DonateX", st, m);

        // ---- Аварийный стоп-кран ----
        _panicHotkey = new PanicHotkey(_settings.PanicHotkey, () => _effectManager!.RevertAll());

        // ---- Трей ----
        SetupTrayIcon();
        WarnIfNotElevated();

        // ---- Автозапуск слушателей ----
        if (_settings.AutoStart)
        {
            if (_settings.DaEnabled && !string.IsNullOrWhiteSpace(_settings.DaAccessToken))
            {
                _client.Start();
            }
            if (_settings.DonateXEnabled && !string.IsNullOrWhiteSpace(_settings.DonateXToken))
            {
                _donateX.Start();
            }
        }

        // Первый запуск без единого токена — открываем настройки.
        if (string.IsNullOrWhiteSpace(_settings.DaAccessToken) &&
            string.IsNullOrWhiteSpace(_settings.DonateXToken))
        {
            ShowSettings();
        }
    }

    private void OnDonationReceived(Donation donation)
    {
        // Слушатель в фоне; coordinator сам маршалит работу в UI-поток.
        _coordinator?.HandleDonation(donation);
    }

    private void OnClientStatus(string provider, DaConnectionStatus status, string message)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_trayIcon is null)
            {
                return;
            }

            string text = status switch
            {
                DaConnectionStatus.Connected => $"ScreenApp — {provider}: слушаем донаты",
                DaConnectionStatus.Connecting => $"ScreenApp — {provider}: подключение...",
                DaConnectionStatus.AuthError => $"ScreenApp — {provider}: ошибка авторизации",
                DaConnectionStatus.Error => $"ScreenApp — {provider}: нет связи",
                _ => $"ScreenApp — {provider}: остановлен",
            };
            _trayIcon.Text = text.Length > 63 ? text[..63] : text;
        });
    }

    private void SetupTrayIcon()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Настройки", null, (_, _) => ShowSettings());
        menu.Items.Add("Диагностика и помощь", null, (_, _) => ShowDiagnostics());
        menu.Items.Add("Снять все эффекты", null, (_, _) => _effectManager?.RevertAll());
        menu.Items.Add("Поддержать", null, (_, _) => ShowSupport());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => Shutdown());

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = AppIcon.LoadTrayIcon(),
            Visible = true,
            Text = "ScreenApp",
            ContextMenuStrip = menu,
        };
        _trayIcon.DoubleClick += (_, _) => ShowSettings();
    }

    private void ShowSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings!, _store!, _coordinator!, _client!, _donateX!, _api!, _donateXApi!, _journal!);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private DiagnosticsWindow? _diagnosticsWindow;

    private void ShowDiagnostics()
    {
        if (_diagnosticsWindow is { IsLoaded: true })
        {
            _diagnosticsWindow.Activate();
            return;
        }

        _diagnosticsWindow = new DiagnosticsWindow(_journal!);
        _diagnosticsWindow.Closed += (_, _) => _diagnosticsWindow = null;
        _diagnosticsWindow.Show();
        _diagnosticsWindow.Activate();
    }

    private SupportWindow? _supportWindow;

    private void ShowSupport()
    {
        if (_supportWindow is { IsLoaded: true })
        {
            _supportWindow.Activate();
            return;
        }

        _supportWindow = new SupportWindow();
        _supportWindow.Closed += (_, _) => _supportWindow = null;
        _supportWindow.Show();
        _supportWindow.Activate();
    }

    /// <summary>
    /// Предупредить (не прерывая запуск), если процесс не повышен: блокировки ввода
    /// надёжно работают только с правами администратора.
    /// </summary>
    private void WarnIfNotElevated()
    {
        bool elevated;
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] не удалось определить уровень прав: {ex.Message}");
            return;
        }

        if (elevated || _trayIcon is null)
        {
            return;
        }

        _trayIcon.BalloonTipTitle = "ScreenApp — нужны права администратора";
        _trayIcon.BalloonTipText =
            "Блокировки мыши/клавиатуры могут не работать. Для полной функциональности " +
            "запустите приложение от имени администратора.";
        _trayIcon.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Warning;
        _trayIcon.ShowBalloonTip(8000);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_client is not null)
        {
            await _client.StopAsync().ConfigureAwait(false);
            await _client.DisposeAsync().ConfigureAwait(false);
        }

        if (_donateX is not null)
        {
            await _donateX.StopAsync().ConfigureAwait(false);
            await _donateX.DisposeAsync().ConfigureAwait(false);
        }

        _panicHotkey?.Dispose();
        _overlay?.Dispose();
        _effectManager?.Dispose();

        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();

        base.OnExit(e);
    }
}
