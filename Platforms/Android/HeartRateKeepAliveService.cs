using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Android.Content.PM;
using HeartRateMonitorAndroid.Services;
using Microsoft.Extensions.DependencyInjection;
using Java.Lang;
using Android.Provider;
using Android.App.Job;
using Resource = Android.Resource;

namespace HeartRateMonitorAndroid.Platforms.Android
{
    [Service(Name = "com.nuanrmxi.heartratemonitor.HeartRateKeepAliveService",
        Enabled = true,
        Exported = false,
        ForegroundServiceType = ForegroundService.TypeDataSync | ForegroundService.TypeLocation)]
    public class HeartRateKeepAliveService : Service
    {
        private const int NOTIFICATION_ID = 1001;
        private const string CHANNEL_ID = "HeartRateMonitorChannel";
        private const string CHANNEL_NAME = "心率监测服务";
        private const int REPORT_INTERVAL_MS = 1000; // 1秒汇报间隔
        
        private PowerManager.WakeLock _wakeLock;
        private WebSocketService.HeartRateWebSocketClient _webSocketClient;
        private BluetoothService _bluetoothService;
        private HeartRateDataService _dataService;
        private bool _isServiceRunning = false;
        
        // 定时汇报相关
        private System.Threading.Timer _reportTimer;
        private int _latestHeartRate = 0;
        private readonly object _heartRateLock = new object();

        public override void OnCreate()
        {
            base.OnCreate();
            CreateNotificationChannel();
            AcquireWakeLock();
            InitializeServices();
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            if (!_isServiceRunning)
            {
                StartForegroundService();
                _isServiceRunning = true;
            }
            
            // 返回START_STICKY确保服务被系统杀死后会重启
            return StartCommandResult.Sticky;
        }

        public override IBinder OnBind(Intent intent)
        {
            return null;
        }

        private void CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channel = new NotificationChannel(CHANNEL_ID, CHANNEL_NAME, NotificationImportance.Low)
                {
                    Description = "心率监测后台服务通知"
                };
                channel.SetShowBadge(false);
                channel.EnableLights(false);
                channel.EnableVibration(false);
                
                var notificationManager = GetSystemService(NotificationService) as NotificationManager;
                notificationManager?.CreateNotificationChannel(channel);
            }
        }

        private void StartForegroundService()
        {
            var intent = new Intent(this, typeof(MainActivity));
            intent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
            
            var pendingIntent = PendingIntent.GetActivity(this, 0, intent, 
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            var notification = new NotificationCompat.Builder(this, CHANNEL_ID)
                .SetContentTitle("心率监测服务")
                .SetContentText("正在后台监测心率数据")
                .SetSmallIcon(Resource.Drawable.abc_dialog_material_background)
                .SetContentIntent(pendingIntent)
                .SetOngoing(true)
                .SetPriority(NotificationCompat.PriorityLow)
                .SetCategory(NotificationCompat.CategoryService)
                .Build();

            StartForeground(NOTIFICATION_ID, notification);
        }

        private void AcquireWakeLock()
        {
            var powerManager = GetSystemService(PowerService) as PowerManager;
            _wakeLock = powerManager?.NewWakeLock(WakeLockFlags.Partial, "HeartRateMonitor::KeepAlive");
            _wakeLock?.Acquire();
        }

        private void InitializeServices()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("KeepAliveService: 开始初始化服务");
                
                // 获取数据服务实例
                _dataService = HeartRateDataService.Instance;
                _dataService.UpdateServiceStatus(false, "正在初始化服务...", false);

                // 初始化蓝牙服务
                _bluetoothService = new BluetoothService();
                _bluetoothService.StatusUpdated += OnBluetoothStatusUpdated;
                _bluetoothService.HeartRateUpdated += OnHeartRateDataReceived;
                _bluetoothService.DeviceDiscovered += OnDeviceDiscovered;

                System.Diagnostics.Debug.WriteLine("KeepAliveService: 蓝牙服务已初始化");
                
                // 初始化WebSocket客户端
                Task.Run(async () => await InitializeWebSocketAsync());

                // 启动蓝牙连接
                Task.Run(async () => await StartBluetoothMonitoringAsync());
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InitializeServices Error: {ex.Message}");
                _dataService?.UpdateServiceStatus(false, "服务初始化失败", false);
            }
        }

        private async Task InitializeWebSocketAsync()
        {
            try
            {
                var serverUrl = GetServerUrl();
                if (!string.IsNullOrEmpty(serverUrl))
                {
                    _webSocketClient = new WebSocketService.HeartRateWebSocketClient(serverUrl);
                    await _webSocketClient.ConnectAsync();
                    
                    _dataService?.UpdateServiceStatus(true, "后台服务运行中", true, serverUrl);
                    System.Diagnostics.Debug.WriteLine($"KeepAliveService: WebSocket连接成功: {serverUrl}");
                }
                else
                {
                    _dataService?.UpdateServiceStatus(true, "后台服务运行中", false);
                    System.Diagnostics.Debug.WriteLine("KeepAliveService: 无法获取服务器URL，WebSocket未连接");
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"KeepAliveService: WebSocket连接失败: {ex.Message}");
                _dataService?.UpdateServiceStatus(true, "后台服务运行中", false);
            }
        }

        private async Task StartBluetoothMonitoringAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("KeepAliveService: 开始蓝牙监测");

                // 检查蓝牙状态
                _bluetoothService.CheckBluetoothState();
                
                if (_bluetoothService.IsBluetoothAvailable)
                {
                    _dataService?.UpdateDeviceStatus(false, "", "正在扫描心率设备...");
                    
                    // 开始扫描心率设备
                    await _bluetoothService.StartScanAsync();
                    System.Diagnostics.Debug.WriteLine("KeepAliveService: 蓝牙扫描已启动");
                }
                else
                {
                    _dataService?.UpdateDeviceStatus(false, "", "蓝牙不可用");
                    System.Diagnostics.Debug.WriteLine("KeepAliveService: 蓝牙不可用");
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"KeepAliveService: 蓝牙监测启动失败: {ex.Message}");
                _dataService?.UpdateDeviceStatus(false, "", "蓝牙监测启动失败");
            }
        }

        private void OnBluetoothStatusUpdated(string status)
        {
            System.Diagnostics.Debug.WriteLine($"KeepAliveService: 蓝牙状态更新: {status}");
            _dataService?.UpdateDeviceStatus(false, "", status);
        }

        private void OnHeartRateDataReceived(int heartRate)
        {
            try
            {
                //System.Diagnostics.Debug.WriteLine($"KeepAliveService: 收到心率数据: {heartRate} bpm");
                
                // 只更新最新心率值，不立即发送数据
                lock (_heartRateLock)
                {
                    _latestHeartRate = heartRate;
                }
                
                //System.Diagnostics.Debug.WriteLine($"KeepAliveService: 心率数据已更新: {heartRate} bpm");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"KeepAliveService: 处理心率数据失败: {ex.Message}");
            }
        }

        private async void OnDeviceDiscovered(Plugin.BLE.Abstractions.Contracts.IDevice device)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"KeepAliveService: 发现设备: {device.Name ?? "未知设备"}");
                
                _dataService?.UpdateDeviceStatus(false, device.Name ?? "未知设备", $"发现设备: {device.Name ?? "未知设备"}");
                
                // 尝试连接设备
                await _bluetoothService.ConnectToDeviceAsync(device);

                if (_bluetoothService.ConnectedDevice != null)
                {
                    System.Diagnostics.Debug.WriteLine($"KeepAliveService: 设备连接成功: {device.Name}");
                    
                    _dataService?.UpdateDeviceStatus(true, device.Name ?? "未知设备", $"已连接: {device.Name ?? "未知设备"}");
                    _dataService?.ResetSessionData(); // 重置会话数据
                    
                    // 启动定时汇报
                    StartPeriodicReporting();
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"KeepAliveService: 设备连接失败: {ex.Message}");
                _dataService?.UpdateDeviceStatus(false, "", "设备连接失败");
            }
        }

        /// <summary>
        /// 启动定时汇报（每秒一次）
        /// </summary>
        private void StartPeriodicReporting()
        {
            try
            {
                // 停止现有定时器
                StopPeriodicReporting();
                
                // 启动新的定时器
                _reportTimer = new System.Threading.Timer(OnPeriodicReport, null, 
                    TimeSpan.FromMilliseconds(REPORT_INTERVAL_MS), 
                    TimeSpan.FromMilliseconds(REPORT_INTERVAL_MS));
                
                System.Diagnostics.Debug.WriteLine("KeepAliveService: 定时汇报已启动，间隔1秒");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"KeepAliveService: 启动定时汇报失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止定时汇报
        /// </summary>
        private void StopPeriodicReporting()
        {
            try
            {
                _reportTimer?.Dispose();
                _reportTimer = null;
                System.Diagnostics.Debug.WriteLine("KeepAliveService: 定时汇报已停止");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"KeepAliveService: 停止定时汇报失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 定时汇报回调方法
        /// </summary>
        private async void OnPeriodicReport(object state)
        {
            try
            {
                if (!_isServiceRunning) return;

                int currentHeartRate;
                lock (_heartRateLock)
                {
                    currentHeartRate = _latestHeartRate;
                }

                // 无论心率数据是否更新，都进行汇报
                //System.Diagnostics.Debug.WriteLine($"KeepAliveService: 定时汇报 - 心率: {currentHeartRate} bpm");
                
                // 更新数据服务（触发UI更新）
                if (currentHeartRate > 0)
                {
                    _dataService?.UpdateHeartRateData(currentHeartRate);
                }

                // 发送到WebSocket服务器
                if (_webSocketClient != null)
                {
                    await SendHeartRateToServerAsync(currentHeartRate);
                }

                // 更新通知
                var message = currentHeartRate > 0 ? $"最新心率: {currentHeartRate} BPM" : "等待心率数据...";
                UpdateNotification(message);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"KeepAliveService: 定时汇报失败: {ex.Message}");
            }
        }

        private async Task SendHeartRateToServerAsync(int heartRate)
        {
            try
            {
                if (_webSocketClient == null) return;

                var data = new WebSocketService.HeartRateData
                {
                    HeartRate = heartRate,
                    Timestamp = DateTime.Now,
                    DeviceName = _bluetoothService?.ConnectedDevice?.Name ?? "未知设备"
                };

                await _webSocketClient.SendHeartRateDataAsync(data);
                //System.Diagnostics.Debug.WriteLine($"KeepAliveService: 心率数据已发送到服务器: {heartRate} bpm");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"KeepAliveService: 发送心率数据失败: {ex.Message}");
                _dataService?.UpdateServiceStatus(true, "后台服务运行中", false);
            }
        }

        private void UpdateNotification(string message)
        {
            try
            {
                var intent = new Intent(this, typeof(MainActivity));
                var pendingIntent = PendingIntent.GetActivity(this, 0, intent,
                    PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

                var notification = new NotificationCompat.Builder(this, CHANNEL_ID)
                    .SetContentTitle("心率监测服务")
                    .SetContentText(message)
                    .SetSmallIcon(Resource.Drawable.abc_dialog_material_background)
                    .SetContentIntent(pendingIntent)
                    .SetOngoing(true)
                    .Build();

                var notificationManager = GetSystemService(NotificationService) as NotificationManager;
                notificationManager?.Notify(NOTIFICATION_ID, notification);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateNotification Error: {ex.Message}");
            }
        }

        private string GetServerUrl()
        {
            try
            {
                // 从资源文件读取服���器地址
                using var stream = Assets.Open("server.txt");
                using var reader = new System.IO.StreamReader(stream);
                return reader.ReadToEnd().Trim();
            }
            catch
            {
                return "wss://ws.nuanr-mxi.com/ws"; // 默认服务器地址
            }
        }

        public override void OnDestroy()
        {
            try
            {
                _isServiceRunning = false;
                
                // 停止定时汇报
                StopPeriodicReporting();
                
                // 清理资源
                _bluetoothService?.Dispose();
                _webSocketClient?.Dispose();
                _wakeLock?.Release();
                
                _dataService?.UpdateServiceStatus(false, "服务已停止", false);
                
                System.Diagnostics.Debug.WriteLine("KeepAliveService: 服务已停止");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"KeepAliveService: 服务停止时发生错误: {ex.Message}");
            }
            
            base.OnDestroy();
        }

        public override void OnTaskRemoved(Intent rootIntent)
        {
            // 当任务被移除时重启服务
            var intent = new Intent(this, typeof(HeartRateKeepAliveService));
            StartForegroundService(intent);
            base.OnTaskRemoved(rootIntent);
        }
    }
}
