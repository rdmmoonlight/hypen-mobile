using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using HypenMaui.Services;

namespace HypenMaui.Pages.Downloader;

public partial class DownloaderPage : ContentPage
{
    private readonly IDownloaderService _downloaderService;
    private readonly ObservableCollection<string> _logs = [];
    private readonly List<string> _extractedRelativePaths = [];

    private CancellationTokenSource? _cts;
    private bool _isRunning;

    public DownloaderPage(IDownloaderService downloaderService)
    {
        InitializeComponent();
        _downloaderService = downloaderService;
        LogCollectionView.ItemsSource = _logs;
    }

    private async void OnExtractSingleClicked(object? sender, EventArgs e) =>
        await RunExtractionAsync(SingleUrlEntry.Text);

    private async void OnExtractPlaylistClicked(object? sender, EventArgs e) =>
        await RunExtractionAsync(PlaylistUrlEntry.Text);

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        if (!_isRunning) return;

        _cts?.Cancel();
        AppendLog("[KILLED] Membatalkan proses ekstraksi secara paksa.");
        SetStatus("Proses ekstraksi dibatalkan.", isError: true);
        _isRunning = false;
    }

    private async Task RunExtractionAsync(string? url)
    {
        if (_isRunning) return;
        if (string.IsNullOrWhiteSpace(url))
        {
            SetStatus("URL tidak boleh kosong.", isError: true);
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _isRunning = true;
        _extractedRelativePaths.Clear();
        _logs.Clear();
        TerminalFrame.IsVisible = true;
        AppendLog($"[INIT] Memulai ekstraksi untuk: {url}");
        SetStatus("Mengekstraksi audio di server...", isError: false);

        try
        {
            await foreach (var logLine in _downloaderService.StreamDownloadAsync(url, ct))
            {
                AppendLog(logLine);
                TrackExtractedFile(logLine);

                if (logLine.Contains("[COMPLETED]"))
                {
                    await SaveExtractedFilesAsync(ct);
                }
                else if (logLine.Contains("[ERROR]") || logLine.Contains("[CANCELLED]"))
                {
                    SetStatus("Ekstraksi gagal atau dihentikan di server.", isError: true);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Dibatalkan lewat tombol Batalkan; status sudah diset di OnCancelClicked.
        }
        catch (Exception ex)
        {
            AppendLog($"[ERROR] Gagal terhubung ke server: {ex.Message}");
            SetStatus($"Error: {ex.Message}", isError: true);
        }
        finally
        {
            _isRunning = false;
        }
    }

    private async Task SaveExtractedFilesAsync(CancellationToken ct)
    {
        if (_extractedRelativePaths.Count == 0)
        {
            SetStatus("Ekstraksi selesai, tapi tidak ada file yang terdeteksi.", isError: true);
            return;
        }

        SetStatus($"Menyimpan {_extractedRelativePaths.Count} file ke perangkat...", isError: false);

        var savedCount = 0;
        foreach (var relativePath in _extractedRelativePaths)
        {
            var localPath = await _downloaderService.SaveDownloadedFileAsync(relativePath, ct);
            if (localPath is not null)
            {
                savedCount++;
                AppendLog($"[SAVED] {Path.GetFileName(localPath)}");
            }
            else
            {
                AppendLog($"[ERROR] Gagal menyimpan: {relativePath}");
            }
        }

        if (savedCount == _extractedRelativePaths.Count)
        {
            SetStatus($"Selesai! {savedCount} lagu tersimpan di Music/Hypen.", isError: false);
            SingleUrlEntry.Text = "";
            PlaylistUrlEntry.Text = "";
        }
        else
        {
            SetStatus($"Selesai sebagian: {savedCount}/{_extractedRelativePaths.Count} lagu tersimpan.", isError: true);
        }
    }

    private void TrackExtractedFile(string logLine)
    {
        if (!logLine.Contains("Destination:") || !logLine.Contains(".mp3")) return;

        var match = Regex.Match(logLine, @"Destination:\s*(.+)$");
        if (!match.Success) return;

        var fullPath = match.Groups[1].Value.Trim();
        var relativePath = fullPath;

        if (fullPath.Contains("/downloads/"))
        {
            relativePath = fullPath.Split("/downloads/")[1];
        }
        else if (fullPath.Contains("\\downloads\\"))
        {
            relativePath = fullPath.Split("\\downloads\\")[1];
        }

        relativePath = relativePath.Replace('\\', '/');

        if (!_extractedRelativePaths.Contains(relativePath))
        {
            _extractedRelativePaths.Add(relativePath);
        }
    }

    private void AppendLog(string line)
    {
        MainThread.BeginInvokeOnMainThread(() => _logs.Add(line));
    }

    private void SetStatus(string message, bool isError)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusLabel.Text = message;
            StatusLabel.TextColor = isError ? Color.FromArgb("#FF5252") : Color.FromArgb("#4CC9F0");
            StatusLabel.IsVisible = true;
        });
    }
}
