using HypenMaui.Pages.Home;
using HypenMaui.Pages.Library;
using HypenMaui.Pages.LibraryFusion;
using HypenMaui.Pages.Metadata;
using HypenMaui.Pages.NowPlaying;
using HypenMaui.Pages.Settings;
using Microsoft.Maui.Controls;

namespace HypenMaui.Navigation;

public partial class BottomTabBar : TabBar
{
    public BottomTabBar()
    {
        InitializeComponent();

        // Registrasi seluruh route halaman aplikasi.
        // Halaman yang tampil di tab bar maupun yang diakses lewat navigasi
        // detail (mis. EditMetadataPage, LibraryPage) didaftarkan di sini
        // agar satu titik referensi saja.
        Routing.RegisterRoute(nameof(MainPage), typeof(MainPage));
        Routing.RegisterRoute(nameof(LibraryPage), typeof(LibraryPage));
        Routing.RegisterRoute(nameof(LibraryFusionPage), typeof(LibraryFusionPage));
        Routing.RegisterRoute(nameof(NowPlayingPage), typeof(NowPlayingPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
        Routing.RegisterRoute(nameof(EditMetadataPage), typeof(EditMetadataPage));
    }
}
