using System.Windows;
using System.Windows.Controls;
using ScreenApp.Donations;
using ScreenApp.Effects;
using ScreenApp.Settings;
using Brushes = System.Windows.Media.Brushes;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace ScreenApp.UI;

/// <summary>
/// Окно настроек: подключение к DonationAlerts (OAuth, проверка), список действий с
/// ценами/длительностью (правится в реальном времени) и эмуляция тестового доната.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsStore _store;
    private readonly EffectCoordinator _coordinator;
    private readonly DonationAlertsClient _client;
    private readonly DonateXClient _donateX;
    private readonly DaApi _api;
    private readonly DonateXApi _donateXApi;
    private readonly ScreenApp.Diagnostics.EventJournal _journal;

    public SettingsWindow(
        AppSettings settings,
        SettingsStore store,
        EffectCoordinator coordinator,
        DonationAlertsClient client,
        DonateXClient donateX,
        DaApi api,
        DonateXApi donateXApi,
        ScreenApp.Diagnostics.EventJournal journal)
    {
        _settings = settings;
        _store = store;
        _coordinator = coordinator;
        _client = client;
        _donateX = donateX;
        _api = api;
        _donateXApi = donateXApi;
        _journal = journal;

        InitializeComponent();
        Icon = AppIcon.WindowIcon;

        LoadIntoUi();

        _client.StatusChanged += OnDaStatus;
        _donateX.StatusChanged += OnDonateXStatus;
        Closed += (_, _) =>
        {
            _client.StatusChanged -= OnDaStatus;
            _donateX.StatusChanged -= OnDonateXStatus;
        };
    }

    private void OnDaStatus(DaConnectionStatus s, string m) => ShowProviderStatus("DonationAlerts", s, m);
    private void OnDonateXStatus(DaConnectionStatus s, string m) => ShowProviderStatus("DonateX", s, m);

    private bool _loading;

    private void LoadIntoUi()
    {
        _loading = true;

        ClientIdBox.Text = _settings.DaClientId;
        ClientSecretBox.Password = _settings.DaClientSecret;
        AccessTokenBox.Password = _settings.DaAccessToken;
        RefreshTokenBox.Password = _settings.DaRefreshToken;
        DonateXTokenBox.Password = _settings.DonateXToken;
        RedirectUriBox.Text = OAuthFlow.RedirectUri;
        HotkeyBox.Text = _settings.PanicHotkey;
        AutoStartCheck.IsChecked = _settings.AutoStart;
        DaEnabledCheck.IsChecked = _settings.DaEnabled;
        DonateXEnabledCheck.IsChecked = _settings.DonateXEnabled;
        ShowOverlayCheck.IsChecked = _settings.ShowOverlay;

        // Список мониторов и выбор целевого.
        var monitors = MonitorInfo.All();
        MonitorCombo.ItemsSource = monitors;
        MonitorCombo.SelectedItem =
            monitors.FirstOrDefault(m => m.DeviceName == _settings.TargetMonitorDevice)
            ?? monitors.FirstOrDefault();

        // Грид правит те же объекты ActionSetting, что читает маршрутизатор → real-time.
        ActionsGrid.ItemsSource = _settings.Actions;
        ActionsGrid.CellEditEnding += (_, _) => Dispatcher.BeginInvoke(SaveAll);

        UpdateStatusUi(_client.IsRunning || _donateX.IsRunning ? DaConnectionStatus.Connecting : DaConnectionStatus.Disconnected,
            _client.IsRunning || _donateX.IsRunning ? "Запущен" : "Слушатель остановлен");

        _loading = false;
    }

    /// <summary>Скопировать поля подключения в настройки и сохранить на диск.</summary>
    private void SaveAll()
    {
        _settings.DaClientId = ClientIdBox.Text.Trim();
        _settings.DaClientSecret = ClientSecretBox.Password.Trim();
        _settings.DaAccessToken = AccessTokenBox.Password.Trim();
        _settings.DaRefreshToken = RefreshTokenBox.Password.Trim();
        _settings.DonateXToken = DonateXTokenBox.Password.Trim();
        _settings.PanicHotkey = string.IsNullOrWhiteSpace(HotkeyBox.Text) ? "Ctrl+Alt+End" : HotkeyBox.Text.Trim();
        _settings.AutoStart = AutoStartCheck.IsChecked == true;
        _settings.DaEnabled = DaEnabledCheck.IsChecked == true;
        _settings.DonateXEnabled = DonateXEnabledCheck.IsChecked == true;
        _settings.ShowOverlay = ShowOverlayCheck.IsChecked == true;

        try
        {
            _store.Save(_settings);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось сохранить настройки: {ex.Message}", "ScreenApp",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        SaveAll();
        base.OnClosing(e);
    }

    // ─── Подключение ──────────────────────────────────────────────────────────

    private async void OnAuthorizeClick(object sender, RoutedEventArgs e)
    {
        SaveAll();
        AuthButton.IsEnabled = false;
        CheckResult.Foreground = Brushes.Khaki;
        CheckResult.Text = "Открываю браузер для авторизации...";

        try
        {
            var flow = new OAuthFlow(_api);
            var tokens = await flow.AuthorizeAsync(_settings.DaClientId, _settings.DaClientSecret);

            _settings.DaAccessToken = tokens.AccessToken;
            _settings.DaRefreshToken = tokens.RefreshToken;
            _settings.DaTokenExpiresAt = tokens.ExpiresAt;
            AccessTokenBox.Password = tokens.AccessToken;
            RefreshTokenBox.Password = tokens.RefreshToken;
            _store.Save(_settings);

            CheckResult.Foreground = Brushes.LightGreen;
            CheckResult.Text = "Авторизация успешна. Токены сохранены.";
        }
        catch (Exception ex)
        {
            CheckResult.Foreground = Brushes.IndianRed;
            CheckResult.Text = ex.Message;
        }
        finally
        {
            AuthButton.IsEnabled = true;
        }
    }

    private async void OnCheckClick(object sender, RoutedEventArgs e)
    {
        SaveAll();
        CheckButton.IsEnabled = false;
        CheckResult.Foreground = Brushes.Khaki;
        CheckResult.Text = "Проверяю...";

        try
        {
            var user = await _api.GetUserAsync(_settings.DaAccessToken);
            CheckResult.Foreground = Brushes.LightGreen;
            CheckResult.Text = $"OK — авторизован как {user.Name} (ID {user.Id}).";
        }
        catch (Exception ex)
        {
            CheckResult.Foreground = Brushes.IndianRed;
            CheckResult.Text = ex.Message;
        }
        finally
        {
            CheckButton.IsEnabled = true;
        }
    }

    private void OnStartClick(object sender, RoutedEventArgs e)
    {
        SaveAll();

        bool startDa = _settings.DaEnabled && !string.IsNullOrWhiteSpace(_settings.DaAccessToken);
        bool startDx = _settings.DonateXEnabled && !string.IsNullOrWhiteSpace(_settings.DonateXToken);

        if (startDa)
        {
            _client.Start();
        }
        else if (_client.IsRunning)
        {
            _ = _client.StopAsync();
        }

        if (startDx)
        {
            _donateX.Start();
        }
        else if (_donateX.IsRunning)
        {
            _ = _donateX.StopAsync();
        }

        if (!startDa && !startDx)
        {
            UpdateStatusUi(DaConnectionStatus.AuthError,
                "Нечего слушать: включите сервис галочкой и задайте токен.");
            return;
        }

        UpdateStatusUi(DaConnectionStatus.Connecting, "Подключение...");
    }

    private async void OnStopClick(object sender, RoutedEventArgs e)
    {
        await _client.StopAsync();
        await _donateX.StopAsync();
        UpdateStatusUi(DaConnectionStatus.Disconnected, "Слушатель остановлен");
    }

    private async void OnCheckDonateXClick(object sender, RoutedEventArgs e)
    {
        SaveAll();
        CheckDonateXButton.IsEnabled = false;
        DonateXResult.Foreground = Brushes.Khaki;
        DonateXResult.Text = "Проверяю...";

        try
        {
            string username = await _donateXApi.GetUsernameAsync(_settings.DonateXToken);
            DonateXResult.Foreground = Brushes.LightGreen;
            DonateXResult.Text = $"OK — токен принадлежит {username}.";
        }
        catch (Exception ex)
        {
            DonateXResult.Foreground = Brushes.IndianRed;
            DonateXResult.Text = ex.Message;
        }
        finally
        {
            CheckDonateXButton.IsEnabled = true;
        }
    }

    private DiagnosticsWindow? _diagnosticsWindow;

    private void OnDiagnosticsClick(object sender, RoutedEventArgs e)
    {
        if (_diagnosticsWindow is { IsLoaded: true })
        {
            _diagnosticsWindow.Activate();
            return;
        }

        _diagnosticsWindow = new DiagnosticsWindow(_journal) { Owner = this };
        _diagnosticsWindow.Closed += (_, _) => _diagnosticsWindow = null;
        _diagnosticsWindow.Show();
    }

    // ─── Монитор для эффектов ────────────────────────────────────────────────

    private void OnMonitorChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || MonitorCombo.SelectedItem is not MonitorInfo monitor)
        {
            return;
        }

        _settings.TargetMonitorDevice = monitor.DeviceName;
        EffectTarget.DeviceName = monitor.DeviceName;
        SaveAll();
    }

    private void OnIdentifyMonitorsClick(object sender, RoutedEventArgs e) =>
        MonitorIdentifier.Flash(TimeSpan.FromSeconds(2));

    private SupportWindow? _supportWindow;

    private void OnSupportClick(object sender, RoutedEventArgs e)
    {
        if (_supportWindow is { IsLoaded: true })
        {
            _supportWindow.Activate();
            return;
        }

        _supportWindow = new SupportWindow { Owner = this };
        _supportWindow.Closed += (_, _) => _supportWindow = null;
        _supportWindow.Show();
    }

    // ─── Действия ─────────────────────────────────────────────────────────────

    private void OnTestActionClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ActionSetting setting })
        {
            string? text = ActionCatalog.Find(setting.ActionId)?.TakesText == true
                ? "Тестовый текст"
                : null;
            _coordinator.TestAction(setting.ActionId, setting.DurationSeconds, text);
        }
    }

    // ─── Тестовый донат ─────────────────────────────────────────────────────────

    private void OnTestDonationClick(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(TestAmountBox.Text.Trim(), out var amount) || amount <= 0)
        {
            TestResult.Foreground = Brushes.IndianRed;
            TestResult.Text = "Введите корректную сумму.";
            return;
        }

        var donation = new Donation
        {
            Amount = amount,
            Username = string.IsNullOrWhiteSpace(TestUserBox.Text) ? null : TestUserBox.Text.Trim(),
            Message = string.IsNullOrWhiteSpace(TestMessageBox.Text) ? null : TestMessageBox.Text.Trim(),
            IsTest = true,
        };

        bool matched = _coordinator.HandleDonation(donation);
        if (matched)
        {
            TestResult.Foreground = Brushes.LightGreen;
            TestResult.Text = $"Запущено действие для доната {amount} ₽.";
        }
        else
        {
            TestResult.Foreground = Brushes.Khaki;
            TestResult.Text = $"Нет включённого действия с ценой {amount} ₽. Задайте такую цену во вкладке «Действия и цены».";
        }
    }

    // ─── Статус подключения ─────────────────────────────────────────────────────

    private void ShowProviderStatus(string provider, DaConnectionStatus status, string message) =>
        Dispatcher.BeginInvoke(() => UpdateStatusUi(status, $"{provider}: {message}"));

    private void UpdateStatusUi(DaConnectionStatus status, string message)
    {
        StatusDot.Fill = status switch
        {
            DaConnectionStatus.Connected => Brushes.LimeGreen,
            DaConnectionStatus.Connecting => Brushes.Khaki,
            DaConnectionStatus.AuthError => Brushes.OrangeRed,
            DaConnectionStatus.Error => Brushes.IndianRed,
            _ => Brushes.Gray,
        };
        StatusText.Text = message;

        bool anyRunning = _client.IsRunning || _donateX.IsRunning;
        StartButton.IsEnabled = !anyRunning;
        StopButton.IsEnabled = anyRunning;
    }
}
