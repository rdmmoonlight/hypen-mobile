using HypenMaui.Models;
using Microsoft.Maui.Storage;

namespace HypenMaui.Services;

/// <summary>
/// Implementasi ISongService untuk mode local-only player.
/// Membaca lagu dari penyimpanan perangkat (MediaStore) dan mengelola
/// penghapusan/penyalinan file secara lokal. Tidak ada dependensi jaringan.
/// </summary>
public class SongService : ISongService
{
    public async Task<List<SongModel>> GetSongsAsync()
    {
        var status = await Permissions.RequestAsync<MediaAudioPermission>();
        if (status != PermissionStatus.Granted)
        {
            return [];
        }

#if ANDROID
        var context = global::Android.App.Application.Context;
        var localSongs = await Task.Run(() => LocalMusicService.GetAllAudioFiles(context));

        return localSongs.Select(s => new SongModel
        {
            Id = s.Id,
            Title = s.Title,
            Artist = s.Artist,
            Album = s.Album,
            Year = s.Year,
            Cover = s.AlbumArtUri,
            AudioUrl = s.ContentUri,
            DurationMs = s.DurationMs,
            FilePath = s.FilePath,
            FolderPath = Path.GetDirectoryName(s.FilePath) ?? ""
        }).ToList();
#else
        return await Task.FromResult(new List<SongModel>());
#endif
    }

    public async Task DownloadSongAsync(string? url, string fileName)
    {
        // Mode local-only: "unduh" berarti menyalin file lagu lokal ke folder
        // Music milik aplikasi (mis. untuk backup), bukan mengambil dari internet.
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            var musicDir = Path.Combine(FileSystem.AppDataDirectory, "Music");
            Directory.CreateDirectory(musicDir);

            var safeFileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
            var destPath = Path.Combine(musicDir, $"{safeFileName}.mp3");

            Stream? sourceStream = null;
#if ANDROID
            if (url.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
            {
                var context = global::Android.App.Application.Context;
                var androidUri = global::Android.Net.Uri.Parse(url);
                sourceStream = context.ContentResolver?.OpenInputStream(androidUri!);
            }
#endif
            sourceStream ??= File.Exists(url) ? File.OpenRead(url) : null;
            if (sourceStream is null)
            {
                return;
            }

            using (sourceStream)
            using (var destStream = File.Create(destPath))
            {
                await sourceStream.CopyToAsync(destStream);
            }
        }
        catch (Exception ex)
        {
            CrashLogService.LogException(ex, "SongService.DownloadSongAsync");
        }
    }

    public async Task<bool> DeleteSongAsync(long id)
    {
        try
        {
            var songs = await GetSongsAsync();
            var song = songs.FirstOrDefault(s => s.Id == id);
            if (song is null || string.IsNullOrWhiteSpace(song.FilePath))
            {
                return false;
            }

            if (File.Exists(song.FilePath))
            {
                File.Delete(song.FilePath);
            }

#if ANDROID
            var context = global::Android.App.Application.Context;
            var collection = global::Android.Provider.MediaStore.Audio.Media.ExternalContentUri;
            var itemUri = global::Android.Net.Uri.WithAppendedPath(collection, id.ToString());
            context.ContentResolver?.Delete(itemUri!, null, null);
#endif

            return true;
        }
        catch (Exception ex)
        {
            CrashLogService.LogException(ex, "SongService.DeleteSongAsync");
            return false;
        }
    }

    public async Task<bool> DeleteBatchSongsAsync(long[] ids)
    {
        var allSucceeded = true;
        foreach (var id in ids)
        {
            var ok = await DeleteSongAsync(id);
            allSucceeded &= ok;
        }

        return allSucceeded;
    }
}
