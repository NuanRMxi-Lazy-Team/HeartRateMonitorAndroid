using HeartRateMonitorAndroid.Services.Platform;

namespace HeartRateMonitorAndroid.Services
{
    /// <summary>
    /// 通知服务工厂
    /// </summary>
    public static class NotificationService
    {
        private static readonly INotificationService _instance;

        /// <summary>
        /// 静态构造函数，根据平台创建对应的通知服务实现
        /// </summary>
        static NotificationService()
        {
            if (DeviceInfo.Platform == DevicePlatform.Android)
                _instance = new AndroidNotificationService();
            else if (DeviceInfo.Platform == DevicePlatform.iOS)
                _instance = new IosNotificationService();
            else if (DeviceInfo.Platform == DevicePlatform.WinUI)
                _instance = new WindowsNotificationService();
            else _instance = new NullNotificationService();
            // 初始化通知服务
            _instance.Initialize();
        }

        /// <summary>
        /// 获取当前平台的通知服务实例
        /// </summary>
        public static INotificationService Current => _instance;

        #region 便捷方法

        /// <summary>
        /// 显示心率通知
        /// </summary>
        public static void ShowHeartRateNotification(int currentHeartRate, double avgHeartRate, int minHeartRate, int maxHeartRate, TimeSpan duration)
        {
            _instance.ShowHeartRateNotification(currentHeartRate, avgHeartRate, minHeartRate, maxHeartRate, duration);
        }

        /// <summary>
        /// 取消通知
        /// </summary>
        public static void CancelNotification()
        {
            _instance.CancelNotification();
        }

        /// <summary>
        /// 显示重连通知
        /// </summary>
        public static void ShowReconnectionNotification(string title, string message, int attemptCount)
        {
            _instance.ShowReconnectionNotification(title, message, attemptCount);
        }

        #endregion

        /// <summary>
        /// 空实现，用于不支持的平台
        /// </summary>
        private class NullNotificationService : INotificationService
        {
            public void Initialize() { }

            public void ShowHeartRateNotification(int currentHeartRate, double avgHeartRate, int minHeartRate, int maxHeartRate, TimeSpan duration)
            {
                // 空实现
            }

            public void CancelNotification()
            {
                // 空实现
            }

            public void ShowReconnectionNotification(string title, string message, int attemptCount)
            {
                // 空实现
            }
        }
    }
}
