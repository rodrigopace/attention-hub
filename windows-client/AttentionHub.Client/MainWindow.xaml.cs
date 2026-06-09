using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace AttentionHub.Client;

public partial class MainWindow
{
    private readonly ObservableCollection<MockAttentionEvent> _events;
    private readonly DispatcherTimer _pollTimer;
    private readonly SyncClient _syncClient = new();
    private readonly OutlookCalendarConnector _outlookCalendarConnector = new();
    private ICollectionView _inboxView = null!;
    private ClientSettings _settings;

    public MainWindow()
    {
        InitializeComponent();
        _settings = ClientSettingsStore.Load();
        ApplySettingsToUi();

        _events = new ObservableCollection<MockAttentionEvent>(MockEventFactory.CreateInitialEvents());
        EventsGrid.ItemsSource = _events;
        _inboxView = CollectionViewSource.GetDefaultView(_events);
        _inboxView.Filter = FilterInboxEvent;
        EventsInboxGrid.ItemsSource = _inboxView;
        RulesGrid.ItemsSource = MockEventFactory.CreateRules();
        RefreshDerivedViews();
        ConfigureInboxFilters();
        RefreshInboxView();

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
        if (!SaveSettingsFromUi(showConfirmation: true))
        {
            return;
        }

        await SyncAsync();
    }

    private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromUi(showConfirmation: true);
    }

    private async void LoadOutlookCalendarButton_Click(object sender, RoutedEventArgs e)
    {
        LoadOutlookCalendarButton.IsEnabled = false;
        CalendarSourceText.Text = "Lendo Outlook Calendar local...";
        SetStatus("Lendo Outlook Calendar...", "#F79009");

        try
        {
            var result = await _outlookCalendarConnector.ReadUpcomingBusyEventsAsync();
            if (!result.Success || result.Events.Count == 0)
            {
                CalendarSourceText.Text = $"{result.Message} Mantendo fallback mockado.";
                SetStatus("Outlook indisponivel; fallback mockado", "#F79009");
                return;
            }

            ReplaceCalendarEvents(result.Events);
            CalendarSourceText.Text = result.Message;
            SetStatus("Outlook Calendar carregado", "#12B76A");
        }
        finally
        {
            LoadOutlookCalendarButton.IsEnabled = true;
        }
    }

    private void InboxFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        RefreshInboxView();
    }

    private void ClearInboxFiltersButton_Click(object sender, RoutedEventArgs e)
    {
        SourceFilterComboBox.SelectedItem = "Todas";
        TypeFilterComboBox.SelectedItem = "Todos";
        PriorityFilterComboBox.SelectedItem = "Todas";
        StatusFilterComboBox.SelectedItem = "Todos";
        RefreshInboxView();
    }

    private void MarkSeenButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: MockAttentionEvent item })
        {
            item.LocalStatus = "Visto";
            RefreshInboxView();
            RefreshSummary();
        }
    }

    private void SilenceButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: MockAttentionEvent item })
        {
            item.LocalStatus = "Silenciado";
            RefreshInboxView();
            RefreshSummary();
        }
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

        SaveSettingsFromUi(showConfirmation: false);
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
            var request = SyncRequestFactory.Create(_events, _settings);
            var response = await _syncClient.PostSyncAsync(_settings.BackendUrl, request);

            ResponseTextBox.Text = JsonSerializer.Serialize(response, JsonOptions.Pretty);
            SetStatus($"Sync OK: {response.AcceptedEventIds.Count} aceitos, {response.DuplicateEventIds.Count} duplicados", "#12B76A");
            FooterText.Text = $"Ultima sincronizacao: {DateTimeOffset.Now:HH:mm:ss}. Aceitos: {response.AcceptedEventIds.Count}. Duplicados: {response.DuplicateEventIds.Count}. Cursor: {response.NextSyncCursor}";
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

    private void ReplaceCalendarEvents(IReadOnlyList<MockAttentionEvent> calendarEvents)
    {
        var existingCalendarEvents = _events
            .Where(item => item.EventType.StartsWith("calendar.", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var item in existingCalendarEvents)
        {
            _events.Remove(item);
        }

        foreach (var item in calendarEvents.OrderBy(item => item.OccurredAt))
        {
            _events.Add(item);
        }

        ConfigureInboxFilters();
        RefreshDerivedViews();
        RefreshInboxView();
    }

    private void RefreshDerivedViews()
    {
        AgendaGrid.ItemsSource = MockEventFactory.CreateAgendaItems(_events);
        StatusGrid.ItemsSource = MockEventFactory.CreateSourceStatuses(_events);
        RefreshSummary();
    }

    private void ConfigureInboxFilters()
    {
        SourceFilterComboBox.ItemsSource = new[] { "Todas" }
            .Concat(_events.Select(item => item.SourceDisplayName).Distinct().OrderBy(item => item))
            .ToList();
        TypeFilterComboBox.ItemsSource = new[] { "Todos" }
            .Concat(_events.Select(item => item.EventType).Distinct().OrderBy(item => item))
            .ToList();
        PriorityFilterComboBox.ItemsSource = new[] { "Todas", "low", "normal", "high", "urgent" };
        StatusFilterComboBox.ItemsSource = new[] { "Todos", "Novo", "Visto", "Silenciado" };

        SourceFilterComboBox.SelectedIndex = 0;
        TypeFilterComboBox.SelectedIndex = 0;
        PriorityFilterComboBox.SelectedIndex = 0;
        StatusFilterComboBox.SelectedIndex = 0;
    }

    private bool FilterInboxEvent(object item)
    {
        if (item is not MockAttentionEvent inboxEvent)
        {
            return false;
        }

        return MatchesFilter(SourceFilterComboBox, "Todas", inboxEvent.SourceDisplayName)
               && MatchesFilter(TypeFilterComboBox, "Todos", inboxEvent.EventType)
               && MatchesFilter(PriorityFilterComboBox, "Todas", inboxEvent.Priority)
               && MatchesFilter(StatusFilterComboBox, "Todos", inboxEvent.LocalStatus);
    }

    private void RefreshInboxView()
    {
        _inboxView?.Refresh();
        var visibleCount = _inboxView?.Cast<object>().Count() ?? 0;
        InboxCountText.Text = $"{visibleCount} de {_events.Count} eventos";
    }

    private static bool MatchesFilter(ComboBox comboBox, string allValue, string value)
    {
        var selected = comboBox.SelectedItem as string;
        return string.IsNullOrWhiteSpace(selected) || selected == allValue || selected == value;
    }

    private void ApplySettingsToUi()
    {
        BackendUrlTextBox.Text = _settings.BackendUrl;
        PollIntervalTextBox.Text = _settings.PollIntervalSeconds.ToString();
        DeviceIdTextBox.Text = _settings.DeviceId;
        DeviceDisplayNameTextBox.Text = _settings.DeviceDisplayName;
        DeviceText.Text = _settings.DeviceId;
        SettingsPathText.Text = ClientSettingsStore.SettingsPath;
        SettingsStatusText.Text = "Configuracoes carregadas.";
    }

    private bool SaveSettingsFromUi(bool showConfirmation)
    {
        if (!int.TryParse(PollIntervalTextBox.Text.Trim(), out var pollSeconds) || pollSeconds < 30)
        {
            if (showConfirmation)
            {
                MessageBox.Show("Informe um intervalo de polling valido, com no minimo 30 segundos.", "Attention Hub", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return false;
        }

        var backendUrl = BackendUrlTextBox.Text.Trim();
        var deviceId = DeviceIdTextBox.Text.Trim();
        var deviceName = DeviceDisplayNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(backendUrl) || string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(deviceName))
        {
            if (showConfirmation)
            {
                MessageBox.Show("Backend URL, Device ID e Nome do dispositivo sao obrigatorios.", "Attention Hub", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return false;
        }

        _settings = new ClientSettings(
            BackendUrl: backendUrl,
            PollIntervalSeconds: pollSeconds,
            DeviceId: deviceId,
            DeviceDisplayName: deviceName);

        ClientSettingsStore.Save(_settings);
        DeviceText.Text = _settings.DeviceId;
        SettingsStatusText.Text = $"Configuracoes salvas em {DateTimeOffset.Now:HH:mm:ss}.";

        if (showConfirmation)
        {
            SetStatus("Configuracoes salvas", "#12B76A");
        }

        return true;
    }
}
