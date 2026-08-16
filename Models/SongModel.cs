namespace HypenMaui.Models;

/// <summary>
/// Representasi satu lagu di library lokal, dipakai bersama oleh Library Page,
/// Now Playing Page, dan PlayerService (queue).
/// </summary>
public class SongModel
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";
    public string Year { get; set; } = "";
    public string Cover { get; set; } = "";
    public string AudioUrl { get; set; } = "";
    public long DurationMs { get; set; }

    /// <summary>Path folder tempat file lagu disimpan di penyimpanan perangkat, untuk filter berdasarkan folder.</summary>
    public string FolderPath { get; set; } = "";
    public string FolderName => string.IsNullOrWhiteSpace(FolderPath) ? "Tidak Diketahui" : (Path.GetFileName(FolderPath.TrimEnd('/')) is { Length: > 0 } name ? name : FolderPath);

    /// <summary>Durasi dalam format mm:ss untuk ditampilkan di daftar Library.</summary>
    public string DurationText => TimeSpan.FromMilliseconds(DurationMs < 0 ? 0 : DurationMs).ToString(DurationMs >= 3600000 ? @"h\:mm\:ss" : @"mm\:ss");

    // Metadata audiophile — diisi lazy (saat lagu mulai diputar), bukan saat scan awal,
    // supaya scan library tetap cepat untuk koleksi besar.
    public string Format { get; set; } = "";
    public int BitrateKbps { get; set; }
    public bool MetadataLoaded { get; set; }

    // Diisi belakangan oleh MetadataEnrichmentService (Last.fm/MusicBrainz/TheAudioDB) kalau
    // ditemukan cover resolusi lebih tinggi daripada thumbnail lokal dari MediaStore.
    public string? EnrichedCoverPath { get; set; }
    public string? LyricsSourceUrl { get; set; } // dipakai kalau lirik penuh cuma tersedia sbg link (Genius)

    public bool IsFavorite { get; set; }
}
