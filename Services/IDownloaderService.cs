namespace HypenMaui.Services;

public interface IDownloaderService
{
    /// <summary>
    /// Streaming log real-time dari proses ekstraksi yt-dlp di server (SSE),
    /// mendukung link video tunggal maupun playlist YouTube.
    /// </summary>
    IAsyncEnumerable<string> StreamDownloadAsync(string youtubeUrl, CancellationToken ct = default);

    /// <summary>
    /// Mengunduh file MP3 hasil ekstraksi dari server ke folder Music lokal
    /// perangkat, lalu memicu MediaStore scan agar langsung muncul di library.
    /// Mengembalikan path file lokal jika berhasil, null jika gagal.
    /// </summary>
    Task<string?> SaveDownloadedFileAsync(string relativePath, CancellationToken ct = default);
}
