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
        
        private PowerManager.WakeLock _wakeLock;
        private System.Timers.Timer _heartRateTimer;
        private WebSocketService.HeartRateWebSocketClient _webSocketClient;
        private BluetoothService _bluetoothService;
        private bool _isServiceRunning = false;

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
                StartHeartRateMonitoring();
                ScheduleJobService();
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
                .SetSmallIcon(Resource.Drawable.abc_dialog_material_background) // 使用系统图标
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
                
                // 获取服务实例
                var serviceProvider = MauiApplication.Current?.Services;
                _bluetoothService = serviceProvider?.GetService<BluetoothService>();
                
                if (_bluetoothService == null)
                {
                    System.Diagnostics.Debug.WriteLine("KeepAliveService: 无法获取BluetoothService实例，创建新实例");
                    _bluetoothService = new BluetoothService();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("KeepAliveService: 成功获取BluetoothService实例");
                }
                
                // 初始化WebSocket客户端
                var serverUrl = GetServerUrl();
                if (!string.IsNullOrEmpty(serverUrl))
                {
                    _webSocketClient = new WebSocketService.HeartRateWebSocketClient(serverUrl);
                    System.Diagnostics.Debug.WriteLine($"KeepAliveService: WebSocket客户端已初始化，服务器: {serverUrl}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("KeepAliveService: 无法获取服务器URL");
                }

                // 如果蓝牙服务可用，尝试重新连接之前连接的设备
                if (_bluetoothService != null)
                {
                    System.Diagnostics.Debug.WriteLine("KeepAliveService: 启动蓝牙重连任务");
                    Task.Run(async () => await ReconnectBluetoothDevice());
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("KeepAliveService: 蓝牙服务不可用，跳过蓝牙重连");
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InitializeServices Error: {ex.Message}");
            }
        }

        private async Task ReconnectBluetoothDevice()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("KeepAliveService: 尝试重新连接蓝牙设备");

                // 检查蓝牙状态
                var bluetoothState = _bluetoothService.CheckBluetoothState();
                if (!bluetoothState.Contains("准备就绪"))
                {
                    System.Diagnostics.Debug.WriteLine($"KeepAliveService: 蓝牙状态不可用: {bluetoothState}");
                    return;
                }

                // 尝试获取上次连接的设备地址
                var lastConnectedDeviceAddress = _bluetoothService.GetLastConnectedDeviceAddress();
                if (!string.IsNullOrEmpty(lastConnectedDeviceAddress))
                {
                    System.Diagnostics.Debug.WriteLine($"KeepAliveService: 尝试连接上次的设备: {lastConnectedDeviceAddress}");
                    
                    // 首先尝试直接连接到已知设备
                    var directConnectSuccess = await _bluetoothService.ConnectToDeviceByAddressAsync(lastConnectedDeviceAddress);
                    
                    if (!directConnectSuccess)
                    {
                        System.Diagnostics.Debug.WriteLine("KeepAliveService: 直接连接失败，开始扫描寻找设备");
                        
                        // 注册临时设备发现事件处理器
                        bool deviceFound = false;
                        Action<Plugin.BLE.Abstractions.Contracts.IDevice> tempDeviceHandler = (device) =>
                        {
                            if (device.Id.ToString() == lastConnectedDeviceAddress)
                            {
                                deviceFound = true;
                                System.Diagnostics.Debug.WriteLine($"KeepAliveService: 在扫描中找到目标设备: {device.Name}");
                                Task.Run(async () => await _bluetoothService.ConnectToDeviceAsync(device));
                            }
                        };
                        
                        _bluetoothService.DeviceDiscovered += tempDeviceHandler;
                        
                        try
                        {
                            // 开始扫描
                            await _bluetoothService.StartScanAsync();
                            
                            // 等待10秒寻找目标设备
                            for (int i = 0; i < 100 && !deviceFound; i++)
                            {
                                await Task.Delay(100);
                            }
                            
                            // 停止扫描
                            await _bluetoothService.StopScanAsync();
                            
                            if (deviceFound)
                            {
                                System.Diagnostics.Debug.WriteLine("KeepAliveService: 成功找到并连接目标设备");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("KeepAliveService: 未找到目标设备");
                            }
                        }
                        finally
                        {
                            _bluetoothService.DeviceDiscovered -= tempDeviceHandler;
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("KeepAliveService: 直接连接成功");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("KeepAliveService: 没有找到上次连接的设备信息，开始新的扫描");
                    
                    // 注册临时设备发现事件处理器
                    bool anyDeviceFound = false;
                    Action<Plugin.BLE.Abstractions.Contracts.IDevice> tempDeviceHandler = (device) =>
                    {
                        if (!anyDeviceFound)
                        {
                            anyDeviceFound = true;
                            System.Diagnostics.Debug.WriteLine($"KeepAliveService: 发现心率设备: {device.Name}");
                            Task.Run(async () => await _bluetoothService.ConnectToDeviceAsync(device));
                        }
                    };
                    
                    _bluetoothService.DeviceDiscovered += tempDeviceHandler;
                    
                    try
                    {
                        // 如果没有上次连接的设备信息，开始扫描
                        await _bluetoothService.StartScanAsync();
                        
                        // 扫描15秒后停止
                        await Task.Delay(15000);
                        await _bluetoothService.StopScanAsync();
                        
                        if (anyDeviceFound)
                        {
                            System.Diagnostics.Debug.WriteLine("KeepAliveService: 找到并连接了新设备");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("KeepAliveService: 未找到任何心率设备");
                        }
                    }
                    finally
                    {
                        _bluetoothService.DeviceDiscovered -= tempDeviceHandler;
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ReconnectBluetoothDevice Error: {ex.Message}");
            }
        }

        private string GetLastConnectedDeviceAddress()
        {
            try
            {
                // 使用MAUI的Preferences API获取设备地址
                return Microsoft.Maui.Storage.Preferences.Get("LastConnectedDevice", null);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetLastConnectedDeviceAddress Error: {ex.Message}");
                return null;
            }
        }

        private void SaveLastConnectedDeviceAddress(string deviceAddress)
        {
            try
            {
                var sharedPreferences = GetSharedPreferences("HeartRateMonitor", FileCreationMode.Private);
                var editor = sharedPreferences.Edit();
                editor.PutString("LastConnectedDevice", deviceAddress);
                editor.Apply();
                System.Diagnostics.Debug.WriteLine($"KeepAliveService: 保存设备地址: {deviceAddress}");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveLastConnectedDeviceAddress Error: {ex.Message}");
            }
        }

        private string GetServerUrl()
        {
            try
            {
                // 从Resources/Raw/token.txt读取服务器URL
                using var stream = FileSystem.OpenAppPackageFileAsync("server.txt").Result;
                using var reader = new StreamReader(stream);

                var contents = reader.ReadToEnd();
                return contents;
            }
            catch
            {
                return "wss:///ws.nuanr-mxi.com/ws"; // 默认URL
            }
        }

        private void StartHeartRateMonitoring()
        {
            _heartRateTimer = new System.Timers.Timer(1000); // 30秒间隔
            _heartRateTimer.Elapsed += async (sender, e) =>
            {
                try
                {
                    await MonitorHeartRate();
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Heart rate monitoring error: {ex.Message}");
                }
            };
            _heartRateTimer.Start();
        }

        private async Task MonitorHeartRate()
        {
            try
            {
                // 确保WebSocket连接
                if (_webSocketClient != null)
                {
                    await _webSocketClient.ConnectAsync();
                    
                    // 从蓝牙设备获取心率数据
                    var heartRate = await GetCurrentHeartRate();
                    if (heartRate > 0)
                    {
                        await _webSocketClient.SendHeartRateAsync(heartRate);
                        UpdateNotification($"最新心率: {heartRate} BPM");
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MonitorHeartRate Error: {ex.Message}");
            }
        }

        private async Task<int> GetCurrentHeartRate()
        {
            try
            {
                // 只从蓝牙设备获取真实心率数据
                if (_bluetoothService?.ConnectedDevice != null)
                {
                    // 从BluetoothService获取最新的心率值
                    return _bluetoothService.LastHeartRate;
                }
                
                // 如果没有连接设备，返回0表示无数据
                return 0;
            }
            catch
            {
                return 0;
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
                    .SetSmallIcon(Resource.Drawable.abc_dialog_material_background) // 使用系统图标
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

        private void ScheduleJobService()
        {
            try
            {
                var jobScheduler = GetSystemService(JobSchedulerService) as JobScheduler;
                var jobInfo = new JobInfo.Builder(1002, new ComponentName(this, Java.Lang.Class.FromType(typeof(HeartRateJobService))))
                    .SetRequiredNetworkType(NetworkType.Any)
                    .SetPersisted(true)
                    .SetPeriodic(15 * 60 * 1000) // 15分钟
                    .SetRequiresCharging(false)
                    .SetRequiresDeviceIdle(false)
                    .Build();

                jobScheduler?.Schedule(jobInfo);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ScheduleJobService Error: {ex.Message}");
            }
        }

        public override void OnDestroy()
        {
            _isServiceRunning = false;
            _heartRateTimer?.Stop();
            _heartRateTimer?.Dispose();
            _webSocketClient?.Dispose();
            _wakeLock?.Release();
            
            // 服务被销毁时，立即重启
            RestartService();
            
            base.OnDestroy();
        }

        private void RestartService()
        {
            try
            {
                var intent = new Intent(this, typeof(HeartRateKeepAliveService));
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    StartForegroundService(intent);
                }
                else
                {
                    StartService(intent);
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RestartService Error: {ex.Message}");
            }
        }

        public override void OnTaskRemoved(Intent rootIntent)
        {
            // 当任务被移除时重启服务
            RestartService();
            base.OnTaskRemoved(rootIntent);
        }
    }
}
