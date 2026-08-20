using System.Collections.ObjectModel;
using HypenMaui.Models;
using HypenMaui.Pages.Metadata;
using HypenMaui.Pages.NowPlaying;
using HypenMaui.Services;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;

namespace HypenMaui.Pages.Home;

public partial class MainPage : ContentPage
{
    private List<SongModel> _allSongs = [];
    public ObservableCollection<SongModel> DisplayedSongs { get; set; } = [];

    private readonly PlayerService _player = PlayerService.Current;
    private string? _selectedFolder;
    private bool _isSelectMode;

    public MainPage()
    {
        InitializeComponent();
        SongsCollectionView.ItemsSource = DisplayedSongs;

        // MediaElement fisik hidup di halaman ini (tab default, persisten selama app hidup)
        // dan ditempel sekali ke PlayerService singleton.
        _player.AttachPlayer(AudioPlayer);
        _player.PropertyChanged += OnPlayerStateChanged;
        RefreshMiniBar();

        _ = LoadLibraryAsync();

        SongsCollectionView.SelectionChanged += OnSongsSelectionChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (LibraryChangeSignal.Pending)
        {
            LibraryChangeSignal.Pending = false;
            _ = LoadLibraryAsync();
        }
    }

    private void OnPlayerStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlayerService.CurrentSong) or nameof(PlayerService.IsPlaying))
        {
            MainThread.BeginInvokeOnMainThread(RefreshMiniBar);
        }
    }

    private void RefreshMiniBar()
    {
        var song = _player.CurrentSong;
        if (song == null)
        {
            MiniPlayerBar.IsVisible = false;
            return;
        }

        MiniPlayerBar.IsVisible = true;
        MiniTitle.Text = song.Title;
        MiniArtist.Text = song.Artist;

        string? coverSource = !string.IsNullOrWhiteSpace(song.EnrichedCoverPath) ? song.EnrichedCoverPath
                             : !string.IsNullOrWhiteSpace(song.Cover) ? song.Cover
                             : null;

        if (coverSource != null)
        {
            MiniCover.Source = coverSource;
            MiniCover.BackgroundColor = Colors.Transparent;
        }
        else
        {
            MiniCover.Source = null;
            MiniCover.BackgroundColor = PlaceholderArt.ColorFor(song.Artist, song.Title);
        }

        MiniPlayPauseButton.Text = _player.IsPlaying ? "⏸" : "▶";
    }

    // Memindai file Library yang sudah ada di penyimpanan perangkat (offline, tanpa backend)
    private async Task LoadLibraryAsync()
    {
        try
        {
            // Mengatur font tebal secara tegas pada label status
            StatusLabel.FontAttributes = FontAttributes.Bold;
            StatusLabel.Text = "Memeriksa izin akses Library...";

            var status = await Permissions.RequestAsync<MediaAudioPermission>();
            if (status != PermissionStatus.Granted)
            {
                StatusLabel.Text = "Izin akses Library ditolak. Buka Pengaturan untuk mengaktifkan.";
                return;
            }

            StatusLabel.Text = "Memindai Library di perangkat...";

            var context = Android.App.Application.Context;
            var localSongs = await Task.Run(() => LocalMusicService.GetAllAudioFiles(context));

            _allSongs = localSongs.Select(s => new SongModel
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
                FolderPath = Path.GetDirectoryName(s.FilePath) ?? "",
                IsFavorite = _player.IsFavorite(s.Id)
            }).ToList();

            UpdateHeaderStats();
            FilterAndRenderSongs();
            StatusLabel.Text = _allSongs.Count == 0 ? "Tidak ada file musik ditemukan di Library." : "";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
        }
        finally
        {
            RefreshControl.IsRefreshing = false;
        }
    }

    private void UpdateHeaderStats()
    {
        TotalTracksLabel.Text = $"{_allSongs.Count} Tracks";
    }

    private void FilterAndRenderSongs()
    {
        var query = SearchInput.Text?.ToLower() ?? "";
        DisplayedSongs.Clear();

        foreach (var song in _allSongs)
        {
            bool matchesSearch = string.IsNullOrEmpty(query) ||
                song.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                song.Artist.Contains(query, StringComparison.CurrentCultureIgnoreCase);

            bool matchesFolder = _selectedFolder == null || song.FolderName == _selectedFolder;

            if (matchesSearch && matchesFolder)
            {
                DisplayedSongs.Add(song);
            }
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e) => FilterAndRenderSongs();

    private async void OnRefreshTriggered(object? sender, EventArgs e) => await LoadLibraryAsync();

    // Rescan penuh Library lokal
    private async void OnRescanClicked(object? sender, EventArgs e) => await LoadLibraryAsync();

    // Handler untuk event OnFilterCategoryClicked dari MainPage.xaml
    private void OnFilterCategoryClicked(object? sender, EventArgs e)
    {
        _selectedFolder = null;
        FilterAllBtn.BackgroundColor = Color.FromArgb("#00E5FF");
        FilterAllBtn.TextColor = Colors.Black;
        FolderFilterBtn.BackgroundColor = Color.FromArgb("#1E1E1E");
        FolderFilterBtn.TextColor = Colors.White;

        FilterAndRenderSongs();
    }

    // Menampilkan daftar folder tempat lagu-lagu tersimpan, agar bisa difilter per folder
    private async void OnFolderFilterClicked(object? sender, EventArgs e)
    {
        var folders = _allSongs
            .Select(s => s.FolderName)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct()
            .OrderBy(f => f)
            .ToArray();

        if (folders.Length == 0)
        {
            await DisplayAlertAsync("Folder", "Tidak ada folder terdeteksi di Library.", "OK");
            return;
        }

        var choice = await DisplayActionSheetAsync("Pilih Folder", "Batal", null, folders);
        if (string.IsNullOrEmpty(choice) || choice == "Batal") return;

        _selectedFolder = choice;
        FilterAllBtn.BackgroundColor = Color.FromArgb("#1E1E1E");
        FilterAllBtn.TextColor = Colors.White;
        FolderFilterBtn.BackgroundColor = Color.FromArgb("#00E5FF");
        FolderFilterBtn.TextColor = Colors.Black;

        FilterAndRenderSongs();
    }

    // Mekanisme membuat playlist manual: minta nama, lalu simpan playlist kosong secara lokal
    private async void OnAddPlaylistClicked(object? sender, EventArgs e)
    {
        var name = await DisplayPromptAsync("Playlist Baru", "Masukkan nama playlist:", "Buat", "Batal", "Contoh: Lagu Santai");
        if (string.IsNullOrWhiteSpace(name)) return;

        PlaylistService.Create(name);
        await DisplayAlertAsync("Playlist Dibuat", $"Playlist \"{name.Trim()}\" berhasil dibuat.", "OK");
    }

    // Handler untuk event OnSongItemTapped (TapGestureRecognizer di item list)
    private async void OnSongItemTapped(object? sender, TappedEventArgs e)
    {
        if (_isSelectMode) return; // biarkan CollectionView yang menangani seleksi

        if (e.Parameter is SongModel song || (sender is BindableObject bindable && bindable.BindingContext is SongModel songContext && (song = songContext) != null))
        {
            var startIndex = DisplayedSongs.IndexOf(song);
            _player.SetQueueAndPlay(DisplayedSongs, startIndex < 0 ? 0 : startIndex);
            await Shell.Current.GoToAsync(nameof(NowPlayingPage));
        }
    }

    // Play lagu yang di-tap -> queue-nya adalah seluruh list yang sedang ditampilkan
    private async void OnPlaySingleClicked(object? sender, EventArgs e)
    {
        if (_isSelectMode) return;

        if (sender is Button btn && btn.CommandParameter is SongModel song)
        {
            var startIndex = DisplayedSongs.IndexOf(song);
            _player.SetQueueAndPlay(DisplayedSongs, startIndex < 0 ? 0 : startIndex);
            await Shell.Current.GoToAsync(nameof(NowPlayingPage));
        }
    }

    // Buka Edit Metadata untuk satu lagu
    private async void OnEditSingleClicked(object? sender, EventArgs e)
    {
        if (_isSelectMode) return;

        if (sender is Button btn && btn.CommandParameter is SongModel song)
        {
            await Shell.Current.GoToAsync(nameof(EditMetadataPage), new Dictionary<string, object>
            {
                { "Songs", new List<SongModel> { song } }
            });
        }
    }

    // Toggle mode Pilih (multi-select) untuk edit metadata batch
    private void OnToggleSelectModeClicked(object? sender, EventArgs e)
    {
        _isSelectMode = !_isSelectMode;

        SongsCollectionView.SelectionMode = _isSelectMode ? SelectionMode.Multiple : SelectionMode.None;
        SelectModeBtn.Text = _isSelectMode ? "Batal" : "Pilih";
        SelectModeBtn.BackgroundColor = _isSelectMode ? Color.FromArgb("#8A5CF5") : Color.FromArgb("#1E1E1E");
        SelectionBar.IsVisible = _isSelectMode;

        if (!_isSelectMode)
        {
            SongsCollectionView.SelectedItems?.Clear();
        }
    }

    private void OnSongsSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        int count = SongsCollectionView.SelectedItems?.Count ?? 0;
        SelectionCountLabel.Text = $"{count} lagu dipilih";
    }

    // Buka Edit Metadata untuk semua lagu yang sedang dipilih
    private async void OnBatchEditClicked(object? sender, EventArgs e)
    {
        var selected = SongsCollectionView.SelectedItems?.OfType<SongModel>().ToList() ?? [];
        if (selected.Count == 0)
        {
            await DisplayAlertAsync("Belum Ada Lagu", "Pilih minimal satu lagu dulu.", "OK");
            return;
        }

        await Shell.Current.GoToAsync(nameof(EditMetadataPage), new Dictionary<string, object>
        {
            { "Songs", selected }
        });
    }

    private async void OnMiniBarTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(nameof(NowPlayingPage));

    private void OnMiniPlayPauseClicked(object? sender, EventArgs e) => _player.TogglePlayPause();

    private void OnMiniNextClicked(object? sender, EventArgs e) => _player.Next();
}
