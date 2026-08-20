using HypenMaui.Models;

namespace HypenMaui.Services;

public interface ISongService
{
    Task<List<SongModel>> GetSongsAsync();
    Task DownloadSongAsync(string? url, string fileName);
    Task<bool> DeleteSongAsync(long id);
    Task<bool> DeleteBatchSongsAsync(long[] ids);
}
