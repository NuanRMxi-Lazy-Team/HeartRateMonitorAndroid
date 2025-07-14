namespace HeartRateMonitorAndroid.Services.Platform
{
    /// <summary>
    /// iOS平台通知服务实现
    /// </summary>
    public class IosNotificationService : INotificationService
    {
        /// <summary>
        /// 初始化通知服务
        /// </summary>
        public void Initialize()
        {
#if IOS
            // 请求iOS通知权限
            Platforms.iOS.IosNotificationHelper.RequestNotificationPermission().ConfigureAwait(false);
#endif
        }

        /// <summary>
        /// 显示心率通知
        /// </summary>
        public void ShowHeartRateNotification(int currentHeartRate, double avgHeartRate, int minHeartRate, int maxHeartRate, TimeSpan duration)
        {
            string title = "心率监测";
            string content = $"当前心率: {currentHeartRate} bpm    平均: {avgHeartRate:0} bpm";

#if IOS
            Platforms.iOS.IosNotificationHelper.ShowNotification(title, content);
#endif
        }

        /// <summary>
        /// 取消通知
        /// </summary>
        public void CancelNotification()
        {
#if IOS
            Platforms.iOS.IosNotificationHelper.CancelAllNotifications();
#endif
        }

        /// <summary>
        /// 显示重连通知
        /// </summary>
        public void ShowReconnectionNotification(string title, string message, int attemptCount)
        {
#if IOS
            Platforms.iOS.IosNotificationHelper.ShowNotification(title, message);
#endif
        }
    }
}
