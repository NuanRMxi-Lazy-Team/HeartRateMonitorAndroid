using HeartRateMonitorAndroid.Models;
using System.Diagnostics;

namespace HeartRateMonitorAndroid.Services
{
    /// <summary>
    /// 心率数据服务实现，作为前端与后台服务的桥梁
    /// </summary>
    public class HeartRateDataService : IHeartRateDataService
    {
        private const string Tag = "HeartRateDataService";
        
        // 静态实例，用于在后台服务和前端之间共享数据
        private static HeartRateDataService _instance;
        private static readonly object _lock = new object();

        // 事件
        public event Action<int> HeartRateDataReceived;
        public event Action<ServiceStatus> ServiceStatusChanged;
        public event Action<DeviceConnectionStatus> DeviceStatusChanged;

        // 数据存储
        private readonly HeartRateSessionData _sessionData = new();
        private ServiceStatus _serviceStatus = new();
        private DeviceConnectionStatus _deviceStatus = new();

        private HeartRateDataService()
        {
            // 私有构造函数，确保单例模式
        }

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static HeartRateDataService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new HeartRateDataService();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 获取当前心率数据
        /// </summary>
        public HeartRateSessionData GetCurrentSessionData()
        {
            return _sessionData;
        }

        /// <summary>
        /// 获取服务状态
        /// </summary>
        public ServiceStatus GetServiceStatus()
        {
            return _serviceStatus;
        }

        /// <summary>
        /// 获取设备连接状态
        /// </summary>
        public DeviceConnectionStatus GetDeviceStatus()
        {
            return _deviceStatus;
        }

        /// <summary>
        /// 启动后台服务（平台特定实现将在调用时提供）
        /// </summary>
        public async Task StartBackgroundServiceAsync()
        {
            try
            {
                // 通过依赖注入获取平台特定的服务启动器
                var serviceStarter = ServiceHelper.Current?.BackgroundServiceStarter;
                if (serviceStarter != null)
                {
                    await serviceStarter.StartServiceAsync();
                    Debug.WriteLine($"{Tag}: 后台服务启动请求已发送");
                }
                else
                {
                    Debug.WriteLine($"{Tag}: 无法获取平台特定的服务启动器");
                    throw new PlatformNotSupportedException("当前平台不支持后台服务");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{Tag}: 启动后台服务失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 停止后台服务
        /// </summary>
        public async Task StopBackgroundServiceAsync()
        {
            try
            {
                var serviceStarter = ServiceHelper.Current?.BackgroundServiceStarter;
                if (serviceStarter != null)
                {
                    await serviceStarter.StopServiceAsync();
                    Debug.WriteLine($"{Tag}: 后台服务停止请求已发送");
                }
                else
                {
                    Debug.WriteLine($"{Tag}: 无法获取平台特定的服务启动器");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{Tag}: 停止后台服务失败: {ex.Message}");
                throw;
            }
        }

        // 以下方法供后台服务调用，用于更新数据和状态

        /// <summary>
        /// 更新心率数据（由后台服务调用）
        /// </summary>
        public void UpdateHeartRateData(int heartRate)
        {
            try
            {
                _sessionData.AddHeartRate(heartRate);
                HeartRateDataReceived?.Invoke(heartRate);
                //Debug.WriteLine($"{Tag}: 心率数据已更新: {heartRate} bpm");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{Tag}: 更新心率数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新服务状态（由后台服务调用）
        /// </summary>
        public void UpdateServiceStatus(bool isRunning, string statusMessage, bool isWebSocketConnected, string webSocketUrl = "")
        {
            try
            {
                _serviceStatus.IsRunning = isRunning;
                _serviceStatus.StatusMessage = statusMessage;
                _serviceStatus.IsWebSocketConnected = isWebSocketConnected;
                _serviceStatus.WebSocketUrl = webSocketUrl;
                _serviceStatus.LastUpdateTime = DateTime.Now;

                ServiceStatusChanged?.Invoke(_serviceStatus);
                Debug.WriteLine($"{Tag}: 服务状态已更新: {statusMessage}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{Tag}: 更新服务状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新设备连接状态（由后台服务调用）
        /// </summary>
        public void UpdateDeviceStatus(bool isConnected, string deviceName, string connectionMessage)
        {
            try
            {
                _deviceStatus.IsConnected = isConnected;
                _deviceStatus.DeviceName = deviceName;
                _deviceStatus.ConnectionMessage = connectionMessage;
                _deviceStatus.LastConnectionTime = DateTime.Now;

                DeviceStatusChanged?.Invoke(_deviceStatus);
                Debug.WriteLine($"{Tag}: 设备状态已更新: {connectionMessage}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{Tag}: 更新设备状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重置会话数据（由后台服务调用）
        /// </summary>
        public void ResetSessionData()
        {
            try
            {
                _sessionData.ResetData();
                Debug.WriteLine($"{Tag}: 会话数据已重置");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{Tag}: 重置会话数据失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 平台特定服务启动器接口
    /// </summary>
    public interface IBackgroundServiceStarter
    {
        Task StartServiceAsync();
        Task StopServiceAsync();
    }

    /// <summary>
    /// 服务助手类，用于获取平台特定的服务
    /// </summary>
    public static class ServiceHelper
    {
        public static IServiceHelper Current { get; set; }
    }

    /// <summary>
    /// 服务助手接口
    /// </summary>
    public interface IServiceHelper
    {
        IBackgroundServiceStarter BackgroundServiceStarter { get; }
    }
}
