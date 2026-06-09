using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace AttentionHub.Client;

public partial class MainWindow
{
    private readonly ObservableCollection<MockAttentionEvent> _events;
    private readonly DispatcherTimer _pollTimer;
    private readonly SyncClient _syncClient = new();

    public MainWindow()
    {
        InitializeComponent();

        _events = new ObservableCollection<MockAttentionEvent>(MockEventFactory.CreateInitialEvents());
        EventsGrid.ItemsSource = _events;
        EventsInboxGrid.ItemsSource = _events;
        AgendaGrid.ItemsSource = MockEventFactory.CreateAgendaItems(_events);
        RulesGrid.ItemsSource = MockEventFactory.CreateRules();
        StatusGrid.ItemsSource = MockEventFactory.CreateSourceStatuses(_events);
        RefreshSummary();

        _pollTimer = new DispatcherTimer();
        _pollTimer.Tick += async (_, _) => await SyncAsync();

        NavigateTo("Agora");
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string pageName)
        {
            NavigateTo(pageName);
        }
    }

    private async void SyncNowButton_Click(object sender, RoutedEventArgs e)
    {
        await SyncAsync();
    }

    private void TogglePollingButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pollTimer.IsEnabled)
        {
            _pollTimer.Stop();
            TogglePollingButton.Content = "Iniciar polling";
            SetStatus("Polling pausado", "#98A2B3");
            return;
        }

        if (!TryGetPollInterval(out var interval))
        {
            MessageBox.Show("Informe um intervalo de polling valido, em segundos.", "Attention Hub", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _pollTimer.Interval = interval;
        _pollTimer.Start();
        TogglePollingButton.Content = "Pausar polling";
        SetStatus($"Polling ativo a cada {(int)interval.TotalSeconds}s", "#12B76A");
    }

    private async Task SyncAsync()
    {
        SyncNowButton.IsEnabled = false;
        SetStatus("Sincronizando...", "#F79009");

        try
        {
            var request = SyncRequestFactory.Create(_events);
            var response = await _syncClient.PostSyncAsync(BackendUrlTextBox.Text.Trim(), request);

            ResponseTextBox.Text = JsonSerializer.Serialize(response, JsonOptions.Pretty);
            SetStatus($"Sync OK: {response.AcceptedEventIds.Count} aceitos", "#12B76A");
            FooterText.Text = $"Ultima sincronizacao: {DateTimeOffset.Now:HH:mm:ss}. Cursor: {response.NextSyncCursor}";
        }
        catch (Exception ex)
        {
            ResponseTextBox.Text = ex.ToString();
            SetStatus("Falha na sincronizacao", "#F04438");
        }
        finally
        {
            SyncNowButton.IsEnabled = true;
        }
    }

    private bool TryGetPollInterval(out TimeSpan interval)
    {
        interval = TimeSpan.Zero;
        if (!int.TryParse(PollIntervalTextBox.Text.Trim(), out var seconds))
        {
            return false;
        }

        if (seconds < 30)
        {
            return false;
        }

        interval = TimeSpan.FromSeconds(seconds);
        return true;
    }

    private void NavigateTo(string pageName)
    {
        NowView.Visibility = pageName == "Agora" ? Visibility.Visible : Visibility.Collapsed;
        EventsView.Visibility = pageName == "Eventos" ? Visibility.Visible : Visibility.Collapsed;
        AgendaView.Visibility = pageName == "Agenda" ? Visibility.Visible : Visibility.Collapsed;
        RulesView.Visibility = pageName == "Regras" ? Visibility.Visible : Visibility.Collapsed;
        StatusView.Visibility = pageName == "Status" ? Visibility.Visible : Visibility.Collapsed;

        PageTitleText.Text = pageName;
        PageSubtitleText.Text = pageName switch
        {
            "Agora" => "Resumo operacional dos eventos mockados",
            "Eventos" => "Inbox unificada com sinais de atencao",
            "Agenda" => "Blocos mascarados que seriam enviados ao calendario central",
            "Regras" => "Configuracoes iniciais sem integracoes reais",
            "Status" => "Saude mockada das fontes e do polling",
            _ => "Attention Hub"
        };

        SetNavState(NowNavButton, pageName == "Agora");
        SetNavState(EventsNavButton, pageName == "Eventos");
        SetNavState(AgendaNavButton, pageName == "Agenda");
        SetNavState(RulesNavButton, pageName == "Regras");
        SetNavState(StatusNavButton, pageName == "Status");
    }

    private static void SetNavState(Button button, bool isSelected)
    {
        button.Background = SolidBrush(isSelected ? "#EAF2FF" : "#FFFFFF");
        button.Foreground = SolidBrush(isSelected ? "#0B5CAD" : "#344054");
        button.BorderBrush = SolidBrush(isSelected ? "#EAF2FF" : "#FFFFFF");
        button.FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal;
    }

    private void SetStatus(string text, string color)
    {
        SyncStatusText.Text = text;
        StatusDot.Fill = SolidBrush(color);
    }

    private void RefreshSummary()
    {
        TotalEventsText.Text = _events.Count.ToString();
        HighPriorityText.Text = _events.Count(item => item.Priority is "high" or "urgent").ToString();
        SourcesText.Text = _events.Select(item => item.SourceId).Distinct().Count().ToString();
    }

    private static SolidColorBrush SolidBrush(string color)
    {
        return (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
    }
}
