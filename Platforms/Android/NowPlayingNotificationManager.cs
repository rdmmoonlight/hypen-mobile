using Android.App;
using Android.Content;
using AndroidX.Core.App;

namespace HypenMaui.Platforms.Android;

/// <summary>
/// Notifikasi "Now Playing" milik aplikasi. Menimpa notifikasi bawaan
/// CommunityToolkit.Maui.MediaElement (id=1, channel "1") supaya perilaku
/// dismiss bisa dikontrol sesuai status playback:
/// - Sedang play  -> ongoing, tidak bisa di-swipe. Satu-satunya cara menutup
///                    adalah tombol "Force Close" yang mematikan aplikasi.
/// - Tidak play   -> bisa langsung di-swipe/dismiss dari status bar.
/// </summary>
public static class NowPlayingNotificationManager
{
    private const string ChannelId = "1"; // sama dengan channel bawaan MediaElement
    private const int NotificationId = 1; // sama dengan id bawaan MediaElement

    public static void Update(bool isPlaying, string title, string artist)
    {
        var context = Platform.AppContext;
        if (context == null) return;

        EnsureChannel(context);

        var builder = new NotificationCompat.Builder(context, ChannelId)
            .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)
            .SetContentTitle(string.IsNullOrWhiteSpace(title) ? "Hypen Vault" : title)
            .SetContentText(artist ?? string.Empty)
            .SetOnlyAlertOnce(true)
            .SetVisibility(NotificationCompat.VisibilityPublic);

        var contentIntent = CreateContentIntent(context);
        if (contentIntent != null)
        {
            builder.SetContentIntent(contentIntent);
        }

        if (isPlaying)
        {
            // Sedang memutar lagu: tidak bisa di-swipe, hanya lewat tombol Force Close.
            builder.SetOngoing(true);
            builder.SetAutoCancel(false);
            builder.AddAction(BuildForceCloseAction(context));
        }
        else
        {
            // Tidak sedang memutar lagu: bisa langsung di-swipe/dismiss.
            builder.SetOngoing(false);
            builder.SetAutoCancel(true);
            builder.SetDeleteIntent(BuildBroadcastPendingIntent(context, "ACTION_DISMISS_NOTIFICATION", 2));
        }

        NotificationManagerCompat.From(context).Notify(NotificationId, builder.Build());
    }

    public static void Cancel()
    {
        var context = Platform.AppContext;
        if (context == null) return;

        NotificationManagerCompat.From(context).Cancel(NotificationId);
    }

    private static void EnsureChannel(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;

        if (context.GetSystemService(Context.NotificationService) is NotificationManager manager
            && manager.GetNotificationChannel(ChannelId) == null)
        {
            var channel = new NotificationChannel(ChannelId, "Now Playing", NotificationImportance.Low);
            manager.CreateNotificationChannel(channel);
        }
    }

    private static PendingIntent? CreateContentIntent(Context context)
    {
        var packageName = context.PackageName;
        if (packageName == null) return null;

        var launchIntent = context.PackageManager?.GetLaunchIntentForPackage(packageName);
        if (launchIntent == null) return null;

        launchIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);

        return PendingIntent.GetActivity(
            context,
            0,
            launchIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
    }

    private static NotificationCompat.Action BuildForceCloseAction(Context context)
    {
        var pendingIntent = BuildBroadcastPendingIntent(context, "ACTION_FORCE_CLOSE", 1);

        return new NotificationCompat.Action.Builder(
            global::Android.Resource.Drawable.IcMenuCloseClearCancel,
            "Force Close",
            pendingIntent).Build();
    }

    private static PendingIntent BuildBroadcastPendingIntent(Context context, string action, int requestCode)
    {
        var intent = new Intent(context, typeof(MediaNotificationReceiver));
        intent.SetAction(action);

        return PendingIntent.GetBroadcast(
            context,
            requestCode,
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }
}
