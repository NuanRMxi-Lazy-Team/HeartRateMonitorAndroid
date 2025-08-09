using HeartRateMonitorAndroid.Models;

namespace HeartRateMonitorAndroid.Services
{
    /// <summary>
    /// 心率数据服务接口，用于前端与后台服务通信
    /// </summary>
    public interface IHeartRateDataService
    {
        /// <summary>
        /// 心率数据更新事件
        /// </summary>
        event Action<int> HeartRateDataReceived;

        /// <summary>
        /// 服务状态更新事件
        /// </summary>
        event Action<ServiceStatus> ServiceStatusChanged;

        /// <summary>
        /// 设备连接状态更新事件
        /// </summary>
        event Action<DeviceConnectionStatus> DeviceStatusChanged;

        /// <summary>
        /// 获取当前心率数据
        /// </summary>
        HeartRateSessionData GetCurrentSessionData();

        /// <summary>
        /// 获取服务状态
        /// </summary>
        ServiceStatus GetServiceStatus();

        /// <summary>
        /// 获取设备连接状态
        /// </summary>
        DeviceConnectionStatus GetDeviceStatus();

        /// <summary>
        /// 启动后台服务
        /// </summary>
        Task StartBackgroundServiceAsync();

        /// <summary>
        /// 停止后台服务
        /// </summary>
        Task StopBackgroundServiceAsync();
    }

    /// <summary>
    /// 服务状态
    /// </summary>
    public class ServiceStatus
    {
        public bool IsRunning { get; set; }
        public string StatusMessage { get; set; } = "";
        public bool IsWebSocketConnected { get; set; }
        public string WebSocketUrl { get; set; } = "";
        public DateTime LastUpdateTime { get; set; }
    }

    /// <summary>
    /// 设备连接状态
    /// </summary>
    public class DeviceConnectionStatus
    {
        public bool IsConnected { get; set; }
        public string DeviceName { get; set; } = "";
        public string ConnectionMessage { get; set; } = "";
        public DateTime LastConnectionTime { get; set; }
        public int SignalStrength { get; set; } // 信号强度，如果支持的话
    }
}
