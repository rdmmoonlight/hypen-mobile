using System.Runtime.CompilerServices;

namespace HypenMaui.Services;

/// <summary>
/// Klien untuk backend Hypen Web (hypen-web) yang menjalankan yt-dlp di server.
/// Mobile app hanya berperan sebagai downloader/consumer: memicu ekstraksi via
/// SSE stream, lalu menarik file MP3 hasil ekstraksi ke penyimpanan lokal.
/// </summary>
public class DownloaderService : IDownloaderService
{
    private const string BaseUrl = "https://hypen-0s65.onrender.com";
    private const string DataPrefix = "data: ";

    private readonly HttpClient _httpClient;

    public DownloaderService()
    {
        _httpClient = new HttpClient
        {
            // Ekstraksi playlist besar bisa memakan waktu lama di server.
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public async IAsyncEnumerable<string> StreamDownloadAsync(
        string youtubeUrl,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var encodedUrl = Uri.EscapeDataString(youtubeUrl.Trim());
        var requestUri = $"{BaseUrl}/api/convert-stream?url={encodedUrl}";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null)
            {
                yield break;
            }

            if (line.StartsWith(DataPrefix, StringComparison.Ordinal))
            {
                yield return line[DataPrefix.Length..];
            }
        }
    }

    public async Task<string?> SaveDownloadedFileAsync(string relativePath, CancellationToken ct = default)
    {
        try
        {
            var segments = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var encodedSegments = segments.Select(Uri.EscapeDataString);
            var downloadUrl = $"{BaseUrl}/downloads/{string.Join('/', encodedSegments)}";

            using var response = await _httpClient.GetAsync(downloadUrl, ct);
            response.EnsureSuccessStatusCode();

#if ANDROID
            var musicRoot = global::Android.OS.Environment
                .GetExternalStoragePublicDirectory(global::Android.OS.Environment.DirectoryMusic)!
                .AbsolutePath;
            var destDir = Path.Combine(musicRoot, "Hypen");
            Directory.CreateDirectory(destDir);

            var fileName = Path.GetFileName(relativePath.Replace('\\', '/'));
            var destPath = Path.Combine(destDir, fileName);

            await using (var fileStream = File.Create(destPath))
            {
                await response.Content.CopyToAsync(fileStream, ct);
            }

            global::Android.Media.MediaScannerConnection.ScanFile(
                global::Android.App.Application.Context,
                [destPath],
                null,
                null);

            return destPath;
#else
            return null;
#endif
        }
        catch (Exception ex)
        {
            CrashLogService.LogException(ex, "DownloaderService.SaveDownloadedFileAsync");
            return null;
        }
    }
}
