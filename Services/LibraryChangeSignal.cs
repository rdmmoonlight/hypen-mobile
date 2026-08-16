namespace HypenMaui.Services;

/// <summary>
/// Sinyal ringan antar halaman: menandai bahwa Library perlu di-scan ulang (mis. setelah
/// metadata lagu diedit) tanpa perlu dependency injection atau messenger penuh.
/// </summary>
public static class LibraryChangeSignal
{
    public static bool Pending { get; set; }
}
