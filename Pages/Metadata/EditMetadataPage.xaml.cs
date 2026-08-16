using HypenMaui.Models;
using HypenMaui.Services;

namespace HypenMaui.Pages.Metadata;

/// <summary>
/// Halaman edit metadata — dipakai untuk dua mode:
///   - Satu lagu: semua field (Judul, Artis, Album, Tahun) langsung ditulis ke file.
///   - Banyak lagu (batch): Judul disembunyikan (tiap lagu unik), field lain yang diisi
///     diterapkan ke SEMUA lagu terpilih; field kosong berarti "jangan diubah".
///
/// Menerima daftar lagu lewat parameter navigasi Shell "Songs" (lihat MainPage). Sengaja
/// pakai IQueryAttributable (bukan [QueryProperty]) karena [QueryProperty] internalnya
/// selalu memanggil Convert.ChangeType — yang crash (InvalidCastException: IConvertible)
/// kalau nilainya objek kompleks seperti List&lt;SongModel&gt; alih-alih tipe primitif.
/// </summary>
public partial class EditMetadataPage : ContentPage, IQueryAttributable
{
    private List<SongModel> _songs = [];
    private readonly MusicBrainzService _musicBrainz = new();

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Songs", out var value) && value is List<SongModel> songs)
        {
            _songs = songs;
            LoadForm();
        }
    }

    public EditMetadataPage()
    {
        InitializeComponent();
    }

    private void LoadForm()
    {
        if (_songs.Count == 0) return;

        if (_songs.Count == 1)
        {
            var song = _songs[0];
            HeaderLabel.Text = "Edit Metadata Lagu";
            SubHeaderLabel.Text = song.Title;
            TitleFieldGroup.IsVisible = true;
            BatchHintLabel.IsVisible = false;
            BatchListGroup.IsVisible = false;

            TitleEntry.Text = song.Title;
            ArtistEntry.Text = song.Artist;
            AlbumEntry.Text = song.Album;
            YearEntry.Text = song.Year;
        }
        else
        {
            HeaderLabel.Text = "Edit Metadata Batch";
            SubHeaderLabel.Text = $"{_songs.Count} lagu dipilih";
            TitleFieldGroup.IsVisible = false;
            BatchHintLabel.IsVisible = true;
            BatchListGroup.IsVisible = true;
            BatchSongListLabel.Text = string.Join(", ", _songs.Select(s => s.Title));

            ArtistEntry.Text = "";
            AlbumEntry.Text = "";
            YearEntry.Text = "";
        }
    }

    // Untuk 1 lagu: cari sekali pakai Judul/Artis yang sudah diisi, lalu lengkapi field kosong di form.
    // Untuk banyak lagu: proses satu per satu pakai data lagu masing-masing, langsung ditulis ke file.
    private async void OnAutoFillClicked(object sender, EventArgs e)
    {
        if (_songs.Count == 0) return;
        AutoFillButton.IsEnabled = false;

        try
        {
            if (_songs.Count == 1)
            {
                await AutoFillSingleAsync();
            }
            else
            {
                await AutoFillBatchAsync();
            }
        }
        finally
        {
            AutoFillButton.IsEnabled = true;
        }
    }

    private async Task AutoFillSingleAsync()
    {
        var seedArtist = ArtistEntry.Text?.Trim() ?? "";
        var seedTitle = TitleEntry.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(seedArtist) && string.IsNullOrWhiteSpace(seedTitle))
        {
            StatusLabel.Text = "Isi minimal Judul atau Artis dulu supaya bisa dicari.";
            return;
        }

        StatusLabel.Text = "Mencari di MusicBrainz...";
        var candidate = await _musicBrainz.SearchCandidateAsync(seedArtist, seedTitle);

        if (candidate == null)
        {
            StatusLabel.Text = "Tidak ditemukan di MusicBrainz. Coba lengkapi Judul/Artis manual dulu, lalu cari lagi.";
            return;
        }

        if (string.IsNullOrWhiteSpace(TitleEntry.Text) && !string.IsNullOrWhiteSpace(candidate.Title)) TitleEntry.Text = candidate.Title;
        if (string.IsNullOrWhiteSpace(ArtistEntry.Text) && !string.IsNullOrWhiteSpace(candidate.Artist)) ArtistEntry.Text = candidate.Artist;
        if (string.IsNullOrWhiteSpace(AlbumEntry.Text) && !string.IsNullOrWhiteSpace(candidate.Album)) AlbumEntry.Text = candidate.Album;
        if (string.IsNullOrWhiteSpace(YearEntry.Text) && !string.IsNullOrWhiteSpace(candidate.Year)) YearEntry.Text = candidate.Year;

        StatusLabel.Text = "Field yang kosong sudah dilengkapi dari MusicBrainz. Periksa lalu tekan Simpan.";
    }

    private async Task AutoFillBatchAsync()
    {
        int done = 0, filled = 0;

        foreach (var song in _songs)
        {
            done++;
            StatusLabel.Text = $"Memproses {done} dari {_songs.Count}: {song.Title}";

            bool needsArtist = string.IsNullOrWhiteSpace(song.Artist);
            bool needsAlbum = string.IsNullOrWhiteSpace(song.Album);
            bool needsYear = string.IsNullOrWhiteSpace(song.Year);
            if (!needsArtist && !needsAlbum && !needsYear) continue;

            var candidate = await _musicBrainz.SearchCandidateAsync(song.Artist, song.Title);
            if (candidate == null) continue;

            string? newArtist = needsArtist && !string.IsNullOrWhiteSpace(candidate.Artist) ? candidate.Artist : null;
            string? newAlbum = needsAlbum && !string.IsNullOrWhiteSpace(candidate.Album) ? candidate.Album : null;
            string? newYear = needsYear && !string.IsNullOrWhiteSpace(candidate.Year) ? candidate.Year : null;
            if (newArtist == null && newAlbum == null && newYear == null) continue;

            if (AudioTagService.WriteTags(song.FilePath, null, newArtist, newAlbum, newYear))
            {
                if (newArtist != null) song.Artist = newArtist;
                if (newAlbum != null) song.Album = newAlbum;
                if (newYear != null) song.Year = newYear;
                filled++;
            }
        }

        LibraryChangeSignal.Pending = true;
        StatusLabel.Text = $"Selesai. {filled} dari {_songs.Count} lagu berhasil dilengkapi otomatis.";
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        SaveButton.IsEnabled = false;
        try
        {
            if (_songs.Count == 1)
            {
                await SaveSingleAsync();
            }
            else
            {
                await SaveBatchAsync();
            }
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private async Task SaveSingleAsync()
    {
        var song = _songs[0];
        string title = TitleEntry.Text?.Trim() ?? "";
        string artist = ArtistEntry.Text?.Trim() ?? "";
        string album = AlbumEntry.Text?.Trim() ?? "";
        string year = YearEntry.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(title))
        {
            await DisplayAlert("Judul Kosong", "Judul lagu tidak boleh kosong.", "OK");
            return;
        }

        bool ok = AudioTagService.WriteTags(song.FilePath, title, artist, album, year);
        if (!ok)
        {
            await DisplayAlert("Gagal", "Gagal menulis metadata ke file. Pastikan izin penyimpanan aktif.", "OK");
            return;
        }

        song.Title = title;
        song.Artist = artist;
        song.Album = album;
        song.Year = year;

        LibraryChangeSignal.Pending = true;
        await DisplayAlert("Tersimpan", "Metadata berhasil diperbarui.", "OK");
        await Shell.Current.GoToAsync("..");
    }

    private async Task SaveBatchAsync()
    {
        string? artist = string.IsNullOrWhiteSpace(ArtistEntry.Text) ? null : ArtistEntry.Text.Trim();
        string? album = string.IsNullOrWhiteSpace(AlbumEntry.Text) ? null : AlbumEntry.Text.Trim();
        string? year = string.IsNullOrWhiteSpace(YearEntry.Text) ? null : YearEntry.Text.Trim();

        if (artist == null && album == null && year == null)
        {
            await DisplayAlert("Tidak Ada Perubahan", "Isi minimal satu field (Artis/Album/Tahun) untuk diterapkan ke semua lagu terpilih.", "OK");
            return;
        }

        int success = 0;
        foreach (var song in _songs)
        {
            if (AudioTagService.WriteTags(song.FilePath, null, artist, album, year))
            {
                if (artist != null) song.Artist = artist;
                if (album != null) song.Album = album;
                if (year != null) song.Year = year;
                success++;
            }
        }

        LibraryChangeSignal.Pending = true;
        await DisplayAlert("Tersimpan", $"{success} dari {_songs.Count} lagu berhasil diperbarui.", "OK");
        await Shell.Current.GoToAsync("..");
    }
}
