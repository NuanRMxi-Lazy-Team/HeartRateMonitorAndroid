namespace HeartRateMonitorAndroid.Services
{
    /// <summary>
    /// 通知服务接口
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// 初始化通知服务
        /// </summary>
        void Initialize();

        /// <summary>
        /// 显示心率通知
        /// </summary>
        /// <param name="currentHeartRate">当前心率</param>
        /// <param name="avgHeartRate">平均心率</param>
        /// <param name="minHeartRate">最低心率</param>
        /// <param name="maxHeartRate">最高心率</param>
        /// <param name="duration">监测时长</param>
        void ShowHeartRateNotification(int currentHeartRate, double avgHeartRate, int minHeartRate, int maxHeartRate, TimeSpan duration);

        /// <summary>
        /// 取消通知
        /// </summary>
        void CancelNotification();

        /// <summary>
        /// 显示重连通知
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="message">消息内容</param>
        /// <param name="attemptCount">尝试次数</param>
        void ShowReconnectionNotification(string title, string message, int attemptCount);
    }
}
