namespace HypenMaui.Models;

/// <summary>Playlist yang dibuat manual oleh pengguna, berisi kumpulan ID lagu dari Library lokal.</summary>
public class PlaylistModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public List<long> SongIds { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
