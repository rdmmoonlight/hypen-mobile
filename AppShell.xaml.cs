namespace HypenMaui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Registrasi route dipindahkan ke Navigation/BottomTabBar.xaml.cs
        // agar bottom bar dan daftar halaman berada dalam satu file khusus.
    }
}
