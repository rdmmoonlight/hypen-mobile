using IOFile = System.IO.File;
using TagFile = TagLib.File;

namespace HypenMaui.Services;

public record AudioTagData(string Title, string Artist, string Album, string Year);

/// <summary>
/// Membaca dan menulis tag metadata (ID3v2 untuk MP3, tag native untuk FLAC/M4A/OGG, dst.
/// lewat TagLib#) LANGSUNG ke file audio di penyimpanan perangkat. Perubahan permanen di
/// file itu sendiri — bukan cuma cache internal aplikasi — supaya tetap konsisten kalau
/// file dibuka lewat pemutar musik lain atau disalin ke perangkat lain.
/// </summary>
public static class AudioTagService
{
    public static AudioTagData? ReadTags(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !IOFile.Exists(filePath)) return null;

        try
        {
            using var file = TagFile.Create(filePath);
            var tag = file.Tag;
            return new AudioTagData(
                tag.Title ?? "",
                tag.FirstPerformer ?? tag.FirstAlbumArtist ?? "",
                tag.Album ?? "",
                tag.Year > 0 ? tag.Year.ToString() : "");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Menulis tag ke file. Parameter yang bernilai null berarti "jangan diubah" — dipakai
    /// untuk mode edit batch, di mana hanya field yang diisi pengguna yang diterapkan.
    /// String kosong ("") tetap dianggap nilai valid (mengosongkan field tersebut).
    /// </summary>
    public static bool WriteTags(string filePath, string? title, string? artist, string? album, string? year)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !IOFile.Exists(filePath)) return false;

        try
        {
            using var file = TagFile.Create(filePath);
            var tag = file.Tag;

            if (title != null) tag.Title = title;
            if (artist != null) tag.Performers = string.IsNullOrWhiteSpace(artist) ? [] : [artist];
            if (album != null) tag.Album = album;
            if (year != null) tag.Year = uint.TryParse(year, out var y) ? y : 0;

            file.Save();

            // Minta Android menyegarkan indeks MediaStore untuk file ini supaya aplikasi lain
            // (termasuk pemutar musik lain) langsung melihat tag yang baru.
            try
            {
                Android.Media.MediaScannerConnection.ScanFile(Android.App.Application.Context, [filePath], null, null);
            }
            catch
            {
                // Rescan gagal bukan hal fatal — tag di file sudah tersimpan dengan benar.
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
