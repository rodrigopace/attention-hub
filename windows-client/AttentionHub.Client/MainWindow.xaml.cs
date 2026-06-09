using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
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

        _pollTimer = new DispatcherTimer();
        _pollTimer.Tick += async (_, _) => await SyncAsync();
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
            MessageBox.Show("Informe um intervalo de polling válido, em segundos.", "Attention Hub", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            FooterText.Text = $"Última sincronização: {DateTimeOffset.Now:HH:mm:ss}. Cursor: {response.NextSyncCursor}";
        }
        catch (Exception ex)
        {
            ResponseTextBox.Text = ex.ToString();
            SetStatus("Falha na sincronização", "#F04438");
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

    private void SetStatus(string text, string color)
    {
        SyncStatusText.Text = text;
        StatusDot.Fill = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
    }
}

