namespace HeartRateMonitorAndroid.Services.Platform
{
    /// <summary>
    /// Android平台通知服务实现
    /// </summary>
    public class AndroidNotificationService : INotificationService
    {
        private const string CHANNEL_ID = "HeartRateMonitorChannel";
        private const int NOTIFICATION_ID = 100;
        private const int RECONNECTION_NOTIFICATION_ID = 101;

        /// <summary>
        /// 初始化通知服务
        /// </summary>
        public void Initialize()
        {
#if ANDROID
            Platforms.Android.AndroidNotificationHelper.CreateNotificationChannel(
                CHANNEL_ID, 
                "心率监测", 
                "显示实时心率数据");
#endif
        }

        /// <summary>
        /// 显示心率通知
        /// </summary>
        public void ShowHeartRateNotification(int currentHeartRate, double avgHeartRate, int minHeartRate, int maxHeartRate, TimeSpan duration)
        {
            string title = "心率监测";
            string content = $"当前心率: {currentHeartRate} bpm    平均: {avgHeartRate:0} bpm";
            string bigText = $"当前心率: {currentHeartRate} bpm\n监测时长: {duration.Hours:00}:{duration.Minutes:00}:{duration.Seconds:00}\n最低: {minHeartRate} bpm | 最高: {maxHeartRate} bpm";

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

        /// <summary>
        /// 取消通知
        /// </summary>
        public void CancelNotification()
        {
#if ANDROID
            Platforms.Android.AndroidNotificationHelper.CancelNotification(NOTIFICATION_ID);
#endif
        }

        /// <summary>
        /// 显示重连通知
        /// </summary>
        public void ShowReconnectionNotification(string title, string message, int attemptCount)
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
    }
}
