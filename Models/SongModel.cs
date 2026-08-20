namespace HypenMaui.Models;

public enum CloudProvider
{
    Local,
    YouTube,
    MusicBrainz,
    GoogleDrive,
    TeraBox
}

/// <summary>
/// Model Lagu MAUI - Diselaraskan dengan Master Global SSOT (Hypen.Web.Models.SongModel)
/// </summary>
public class SongModel
{
    // =========================================================================
    // 1. PRIMARY KEY & RELASI
    // =========================================================================
    public long Id { get; set; }
    public long? RawId { get; set; }

    // =========================================================================
    // 2. EXTERNAL IDENTIFIERS
    // =========================================================================
    public string? YoutubeVideoId { get; set; }
    public string? MusicBrainzId { get; set; }

    // =========================================================================
    // 3. METADATA LAGU BASE (SSOT)
    // =========================================================================
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string? Album { get; set; } = "Single";
    public int? ReleaseYear { get; set; }
    public string? Country { get; set; } = "Unknown";
    public string? AlbumCoverUrl { get; set; }
    public string? AudioUrl { get; set; }
    public int? DurationSeconds { get; set; }

    // =========================================================================
    // 4. STATUS, PROVIDER & TRACKING
    // =========================================================================
    public string Status { get; set; } = "PENDING";
    public bool IsDownloaded { get; set; } = false;
    public bool IsComplete { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public CloudProvider Provider { get; set; } = CloudProvider.Local;
    public bool IsSelected { get; set; }
    public bool IsFavorite { get; set; }

    // =========================================================================
    // 5. METADATA KHUSUS LOKAL & AUDIOPHILE (MAUI Local Storage)
    // =========================================================================
    public string FilePath { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public int BitrateKbps { get; set; }
    public bool MetadataLoaded { get; set; }
    public string? EnrichedCoverPath { get; set; }
    public string? LyricsSourceUrl { get; set; }
    public string SizeFormatted { get; set; } = string.Empty;

    // =========================================================================
    // 6. HELPER PROPERTIES & COMPATIBILITY ALIASES
    // =========================================================================
    public string Year
    {
        get => ReleaseYear?.ToString() ?? string.Empty;
        set => ReleaseYear = int.TryParse(value, out var y) ? y : null;
    }

    public string Cover
    {
        get => AlbumCoverUrl ?? string.Empty;
        set => AlbumCoverUrl = value;
    }

    public long DurationMs
    {
        get => (DurationSeconds ?? 0) * 1000L;
        set => DurationSeconds = (int)(value / 1000);
    }

    public string DurationText => TimeSpan.FromMilliseconds(DurationMs < 0 ? 0 : DurationMs)
        .ToString(DurationMs >= 3600000 ? @"h\:mm\:ss" : @"mm\:ss");

    public string FolderName => string.IsNullOrWhiteSpace(FolderPath)
        ? "Tidak Diketahui"
        : (Path.GetFileName(FolderPath.TrimEnd('/', '\\')) is { Length: > 0 } name ? name : FolderPath);

    public string? YoutubeId
    {
        get => YoutubeVideoId;
        set => YoutubeVideoId = value;
    }

    public string? Mbid
    {
        get => MusicBrainzId;
        set => MusicBrainzId = value;
    }

    private string? _streamUrl;
    public string StreamUrl
    {
        get => string.IsNullOrEmpty(_streamUrl) ? (AudioUrl ?? string.Empty) : _streamUrl;
        set => _streamUrl = value;
    }
}
