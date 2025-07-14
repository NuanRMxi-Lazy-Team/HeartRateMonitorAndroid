namespace HeartRateMonitorAndroid.Services.Platform
{
    /// <summary>
    /// Windows平台通知服务实现
    /// </summary>
    public class WindowsNotificationService : INotificationService
    {
        /// <summary>
        /// 初始化通知服务
        /// </summary>
        public void Initialize()
        {
            // Windows平台不需要特殊初始化
        }

        /// <summary>
        /// 显示心率通知
        /// </summary>
        public void ShowHeartRateNotification(int currentHeartRate, double avgHeartRate, int minHeartRate, int maxHeartRate, TimeSpan duration)
        {
            string title = "心率监测";
            string content = $"当前心率: {currentHeartRate} bpm    平均: {avgHeartRate:0} bpm";

#if WINDOWS
            Platforms.Windows.WindowsNotificationHelper.ShowNotification(title, content);
#endif
        }

        /// <summary>
        /// 取消通知
        /// </summary>
        public void CancelNotification()
        {
#if WINDOWS
            Platforms.Windows.WindowsNotificationHelper.CancelNotification();
#endif
        }

        /// <summary>
        /// 显示重连通知
        /// </summary>
        public void ShowReconnectionNotification(string title, string message, int attemptCount)
        {
#if WINDOWS
            Platforms.Windows.WindowsNotificationHelper.ShowNotification(title, message);
#endif
        }
    }
}
