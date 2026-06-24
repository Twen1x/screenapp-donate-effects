using System.Collections.ObjectModel;
using System.Windows;
using ScreenApp.Diagnostics;

namespace ScreenApp.UI;

/// <summary>
/// Окно «Диагностика и помощь»: живая проверка доступности серверов донат-сервисов,
/// общий журнал событий и каталог известных ошибок с решениями.
/// </summary>
public partial class DiagnosticsWindow : Window
{
    private readonly EventJournal _journal;
    private readonly ObservableCollection<DiagnosticCheck> _checks = new();
    private readonly ObservableCollection<string> _log = new();

    public DiagnosticsWindow(EventJournal journal)
    {
        _journal = journal;

        InitializeComponent();
        Icon = AppIcon.WindowIcon;

        ChecksList.ItemsSource = _checks;
        LogList.ItemsSource = _log;
        IssuesList.ItemsSource = KnownIssues.All;

        foreach (var line in _journal.Snapshot())
        {
            _log.Add(line);
        }

        _journal.Appended += OnLogAppended;
        Closed += (_, _) => _journal.Appended -= OnLogAppended;

        // Сразу прогоняем проверку при открытии.
        Loaded += async (_, _) => await RunChecksAsync();
    }

    private void OnLogAppended(string line)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _log.Add(line);
            if (_log.Count > 200)
            {
                _log.RemoveAt(0);
            }
            if (LogList.Items.Count > 0)
            {
                LogList.ScrollIntoView(LogList.Items[^1]);
            }
        });
    }

    private async void OnRunCheckClick(object sender, RoutedEventArgs e) => await RunChecksAsync();

    private async Task RunChecksAsync()
    {
        RunCheckButton.IsEnabled = false;
        CheckHint.Text = "Проверяю...";
        _checks.Clear();

        try
        {
            var results = await NetworkDiagnostics.RunAsync();
            foreach (var r in results)
            {
                _checks.Add(r);
            }

            bool allOk = results.All(r => r.Ok);
            CheckHint.Text = allOk
                ? "Связь со всеми серверами в порядке."
                : "Есть недоступные серверы — см. красные строки ниже (часто лечится VPN).";
        }
        catch (Exception ex)
        {
            CheckHint.Text = $"Ошибка проверки: {ex.Message}";
        }
        finally
        {
            RunCheckButton.IsEnabled = true;
        }
    }
}
