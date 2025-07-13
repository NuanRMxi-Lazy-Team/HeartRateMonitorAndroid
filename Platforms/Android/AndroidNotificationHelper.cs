using Android.App;
using Android.Content;
using AndroidX.Core.App;
using Application = Android.App.Application;
namespace HeartRateMonitorAndroid.Platforms.Android
{
    // Android平台特定的通知帮助类
    public static class AndroidNotificationHelper
    {
        // 创建通知渠道
        public static void CreateNotificationChannel(string channelId, string channelName, string description)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                var context = Application.Context;
                var channel = new NotificationChannel(channelId, channelName, NotificationImportance.High)
                {
                    Description = description
                };

                var notificationManager = context.GetSystemService(Context.NotificationService) as NotificationManager;
                notificationManager?.CreateNotificationChannel(channel);
            }
        }

        // 显示通知
        public static void ShowNotification(string channelId, int notificationId, string title, string content, int iconResourceId, bool ongoing = true)
        {
            var context = Application.Context;

            // 创建PendingIntent用于点击通知时打开应用
            var intent = context.PackageManager.GetLaunchIntentForPackage(context.PackageName);
            var pendingIntent = PendingIntent.GetActivity(context, 0, intent, PendingIntentFlags.Immutable);

            // 创建通知内容
            var notificationBuilder = new NotificationCompat.Builder(context, channelId)
                .SetContentTitle(title)
                .SetContentText(content)
                .SetSmallIcon(iconResourceId)
                .SetOngoing(ongoing)
                .SetContentIntent(pendingIntent)
                .SetPriority(NotificationCompat.PriorityHigh);

            // 显示通知
            var notificationManager = NotificationManagerCompat.From(context);
            notificationManager.Notify(notificationId, notificationBuilder.Build());
        }
        // 显示普通通知 - 用于重连提示等
        public static void ShowNormalNotification(string channelId, int notificationId, string title, string content, int iconResourceId, bool isForeground)
        {
            var context = Application.Context;
            var intent = context.PackageManager.GetLaunchIntentForPackage(context.PackageName);
            intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);
            var pendingIntent = PendingIntent.GetActivity(context, 0, intent, PendingIntentFlags.Immutable);

            var builder = new NotificationCompat.Builder(context, channelId)
                .SetContentTitle(title)
                .SetContentText(content)
                .SetSmallIcon(iconResourceId)
                .SetContentIntent(pendingIntent)
                .SetAutoCancel(true);

            var notification = builder.Build();
            // 普通通知
            var notificationManager = NotificationManagerCompat.From(context);
            notificationManager.Notify(notificationId, notification);
            
        }

        // 显示带有扩展内容的通知
        public static void ShowBigTextNotification(string channelId, int notificationId, string title, string content, string bigText, int iconResourceId, bool ongoing = true)
        {
            var context = Application.Context;

            // 创建PendingIntent用于点击通知时打开应用
            var intent = context.PackageManager.GetLaunchIntentForPackage(context.PackageName);
            var pendingIntent = PendingIntent.GetActivity(context, 0, intent, PendingIntentFlags.Immutable);

            // 创建通知内容
            var notificationBuilder = new NotificationCompat.Builder(context, channelId)
                .SetContentTitle(title)
                .SetContentText(content)
                .SetSmallIcon(iconResourceId)
                .SetOngoing(ongoing)
                .SetContentIntent(pendingIntent)
                .SetPriority(NotificationCompat.PriorityHigh)
                .SetStyle(new NotificationCompat.BigTextStyle().BigText(bigText));

            // 显示通知
            var notificationManager = NotificationManagerCompat.From(context);
            notificationManager.Notify(notificationId, notificationBuilder.Build());
        }

        // 取消通知
        public static void CancelNotification(int notificationId)
        {
            var context = Application.Context;
            var notificationManager = NotificationManagerCompat.From(context);
            notificationManager.Cancel(notificationId);
        }
    }
}
