namespace HeartRateMonitorAndroid.Services
{
    // 跨平台通知服务
    public static class NotificationService
    {
        // 常量定义
        private const string CHANNEL_ID = "HeartRateMonitorChannel";
        private const int NOTIFICATION_ID = 100;

        // 初始化通知服务
        public static void Initialize()
        {
            // 根据平台初始化
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
#if ANDROID
                Platforms.Android.AndroidNotificationHelper.CreateNotificationChannel(
                    CHANNEL_ID, 
                    "心率监测", 
                    "显示实时心率数据");
#endif
            }
            else if (DeviceInfo.Platform == DevicePlatform.iOS)
            {
#if IOS
                // 请求iOS通知权限
                Platforms.iOS.IosNotificationHelper.RequestNotificationPermission().ConfigureAwait(false);
#endif
            }
        }

        // 显示心率通知
        public static void ShowHeartRateNotification(int currentHeartRate, double avgHeartRate, int minHeartRate, int maxHeartRate, TimeSpan duration)
        {
            string title = "心率监测";
            string content = $"当前心率: {currentHeartRate} bpm    平均: {avgHeartRate:0} bpm";
            string bigText = $"当前心率: {currentHeartRate} bpm\n监测时长: {duration.Hours:00}:{duration.Minutes:00}:{duration.Seconds:00}\n最低: {minHeartRate} bpm | 最高: {maxHeartRate} bpm";

            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
#if ANDROID
                Platforms.Android.AndroidNotificationHelper.ShowBigTextNotification(
                    CHANNEL_ID,
                    NOTIFICATION_ID,
                    title,
                    content,
                    bigText,
                    Resource.Drawable.notification_icon_background,
                    true);
#endif
            }
            else if (DeviceInfo.Platform == DevicePlatform.iOS)
            {
#if IOS
                Platforms.iOS.IosNotificationHelper.ShowNotification(title, content);
#endif
            }
            else if (DeviceInfo.Platform == DevicePlatform.WinUI)
            {
#if WINDOWS
                Platforms.Windows.WindowsNotificationHelper.ShowNotification(title, content);
#endif
            }
        }

        // 取消通知
        public static void CancelNotification()
        {
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
#if ANDROID
                Platforms.Android.AndroidNotificationHelper.CancelNotification(NOTIFICATION_ID);
#endif
            }
            else if (DeviceInfo.Platform == DevicePlatform.iOS)
            {
#if IOS
                Platforms.iOS.IosNotificationHelper.CancelAllNotifications();
#endif
            }
            else if (DeviceInfo.Platform == DevicePlatform.WinUI)
            {
#if WINDOWS
                Platforms.Windows.WindowsNotificationHelper.CancelNotification();
#endif
            }
        }

        // 显示重连通知
        public static void ShowReconnectionNotification(string title, string message, int attemptCount)
        {
            const int RECONNECTION_NOTIFICATION_ID = 101; // 使用不同的ID，避免覆盖心率通知

            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
#if ANDROID
                Platforms.Android.AndroidNotificationHelper.ShowNormalNotification(
                    CHANNEL_ID,
                    RECONNECTION_NOTIFICATION_ID,
                    title,
                    message,
                    Resource.Drawable.notification_icon_background,
                    false); // 不使用前台服务，只显示普通通知
#endif
            }
            else if (DeviceInfo.Platform == DevicePlatform.iOS)
            {
#if IOS
                Platforms.iOS.IosNotificationHelper.ShowNotification(title, message);
#endif
            }
            else if (DeviceInfo.Platform == DevicePlatform.WinUI)
            {
#if WINDOWS
                Platforms.Windows.WindowsNotificationHelper.ShowNotification(title, message);
#endif
            }
        }
    }
}
