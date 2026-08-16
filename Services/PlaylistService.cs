using System.Text.Json;
using HypenMaui.Models;

namespace HypenMaui.Services;

/// <summary>
/// Menyimpan playlist buatan manual pengguna secara lokal (file JSON di penyimpanan aplikasi).
/// Tidak menyentuh file audio asli — hanya menyimpan referensi Id lagu dari Library.
/// </summary>
public static class PlaylistService
{
    private static readonly string FilePath = Path.Combine(FileSystem.AppDataDirectory, "playlists.json");
    private static List<PlaylistModel>? _cache;

    public static List<PlaylistModel> GetAll()
    {
        if (_cache != null) return _cache;

        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                _cache = JsonSerializer.Deserialize<List<PlaylistModel>>(json) ?? [];
            }
            else
            {
                _cache = [];
            }
        }
        catch
        {
            _cache = [];
        }

        return _cache;
    }

    public static PlaylistModel Create(string name)
    {
        var playlist = new PlaylistModel { Name = name.Trim() };
        var all = GetAll();
        all.Add(playlist);
        Save();
        return playlist;
    }

    public static void AddSong(string playlistId, long songId)
    {
        var playlist = GetAll().FirstOrDefault(p => p.Id == playlistId);
        if (playlist == null || playlist.SongIds.Contains(songId)) return;

        playlist.SongIds.Add(songId);
        Save();
    }

    private static void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_cache ?? []);
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Gagal simpan (mis. storage penuh) — diabaikan agar tidak mengganggu playback.
        }
    }
}
