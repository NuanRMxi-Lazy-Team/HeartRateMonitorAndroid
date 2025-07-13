using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using HeartRateMonitorAndroid.Services;
using Newtonsoft.Json;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE.Abstractions;

namespace HeartRateMonitorAndroid;

// 心率数据点类
public class HeartRateDataPoint
{
    public DateTime Timestamp { get; set; }
    public int HeartRate { get; set; }
}

// 心率图表绘制类
public class HeartRateGraphDrawable : IDrawable
{
    private List<HeartRateDataPoint> _dataPoints = [];
    private int _maxPoints = 100; // 最多显示100个数据点
    private int _minHeartRate = 40;
    private int _maxHeartRate = 180;

    // 图表配色方案
    private readonly Color _backgroundColor = Color.FromArgb("#F8F9FA"); // 浅灰背景色
    private readonly Color _gridLineColor = Color.FromArgb("#E9ECEF"); // 网格线颜色
    private readonly Color _axisColor = Color.FromArgb("#CED4DA"); // 坐标轴颜色
    private readonly Color _textColor = Color.FromArgb("#6C757D"); // 文本颜色
    private readonly Color _heartRateLineColor = Color.FromArgb("#FF4757"); // 心率线颜色
    private readonly Color _heartRateAreaColor = Color.FromRgba(255, 71, 87, 0.2); // 心率区域填充颜色
    private readonly Color _heartRatePointColor = Color.FromArgb("#FF4757"); // 数据点颜色
    private readonly Color _accentColor = Color.FromArgb("#2E86DE"); // 强调色

    public void UpdateData(List<HeartRateDataPoint> dataPoints)
    {
        _dataPoints = dataPoints.ToList();
        // 如果有数据，动态调整Y轴范围
        if (_dataPoints.Count > 0)
        {
            _minHeartRate = Math.Max(40, _dataPoints.Min(p => p.HeartRate) - 10);
            _maxHeartRate = Math.Min(200, _dataPoints.Max(p => p.HeartRate) + 10);

            // 确保Y轴范围合理
            int range = _maxHeartRate - _minHeartRate;
            if (range < 30) // 如果范围太小，扩大它
            {
                _minHeartRate = Math.Max(40, _minHeartRate - (30 - range) / 2);
                _maxHeartRate = Math.Min(200, _maxHeartRate + (30 - range) / 2);
            }

            // 圆整到最接近的10
            _minHeartRate = (_minHeartRate / 10) * 10;
            _maxHeartRate = ((_maxHeartRate + 9) / 10) * 10;
        }
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // 设置背景色
        canvas.FillColor = _backgroundColor;
        canvas.FillRectangle(dirtyRect);

        if (_dataPoints.Count < 2) return; // 至少需要两个点才能绘制线条

        // 计算绘图区域，增加左侧留白以放置y轴标签
        float leftPadding = 45;
        float rightPadding = 20;
        float topPadding = 30;
        float bottomPadding = 40;

        float graphWidth = dirtyRect.Width - leftPadding - rightPadding;
        float graphHeight = dirtyRect.Height - topPadding - bottomPadding;
        float graphBottom = dirtyRect.Height - bottomPadding;
        float graphTop = topPadding;
        float graphLeft = leftPadding;
        float graphRight = dirtyRect.Width - rightPadding;

        // 绘制背景和边框
        canvas.FillColor = Colors.White;
        canvas.FillRoundedRectangle(graphLeft - 5, graphTop - 5, graphWidth + 10, graphHeight + 10, 8);
        canvas.StrokeColor = _gridLineColor;
        canvas.StrokeSize = 1;
        canvas.DrawRoundedRectangle(graphLeft - 5, graphTop - 5, graphWidth + 10, graphHeight + 10, 8);

        // 绘制网格线
        canvas.StrokeColor = _gridLineColor;
        canvas.StrokeSize = 1;
        canvas.StrokeDashPattern = new float[] { 4, 4 }; // 虚线网格

        // 水平网格线和心率刻度
        int yStep = (_maxHeartRate - _minHeartRate) > 100 ? 40 : 20; // 根据范围动态调整步长
        for (int hr = _minHeartRate; hr <= _maxHeartRate; hr += yStep)
        {
            float y = graphBottom - ((hr - _minHeartRate) * graphHeight / (_maxHeartRate - _minHeartRate));

            // 绘制网格线
            canvas.DrawLine(graphLeft, y, graphRight, y);

            // 绘制心率刻度
            canvas.FontSize = 12;
            canvas.FontColor = _textColor;
            canvas.DrawString(hr.ToString(), graphLeft - 25, y, HorizontalAlignment.Center);
        }

        // 重置虚线模式
        canvas.StrokeDashPattern = null;

        // 时间刻度线和标签
        if (_dataPoints.Count > 0)
        {
            int pointCount = _dataPoints.Count;
            int xStep = Math.Max(1, pointCount / 5); // 大约显示5个时间点

            for (int i = 0; i < pointCount; i += xStep)
            {
                if (i >= pointCount) break;
                float x = graphLeft + (i * graphWidth / (pointCount - 1));

                // 绘制垂直网格线
                canvas.StrokeColor = _gridLineColor;
                canvas.StrokeDashPattern = [4, 4];
                canvas.DrawLine(x, graphTop, x, graphBottom);
                canvas.StrokeDashPattern = null;

                // 绘制时间刻度（分钟:秒）
                canvas.FontSize = 12;
                canvas.FontColor = _textColor;
                string timeLabel = _dataPoints[i].Timestamp.ToString("mm:ss");
                canvas.DrawString(timeLabel, x, graphBottom + 15, HorizontalAlignment.Center);
            }
        }

        // 绘制坐标轴
        canvas.StrokeColor = _axisColor;
        canvas.StrokeSize = 2;
        canvas.DrawLine(graphLeft, graphBottom, graphRight, graphBottom); // X轴
        canvas.DrawLine(graphLeft, graphTop, graphLeft, graphBottom); // Y轴

        // 添加标题
        canvas.FontColor = _accentColor;
        canvas.FontSize = 14;
        canvas.DrawString("心率监测图表", dirtyRect.Width / 2, graphTop - 15, HorizontalAlignment.Center);

        // 创建心率曲线路径
        PathF linePath = new PathF();
        PathF areaPath = new PathF();
        bool isFirst = true;

        // 添加区域填充起始点
        areaPath.MoveTo(graphLeft, graphBottom);

        for (int i = 0; i < _dataPoints.Count; i++)
        {
            float x = graphLeft + (i * graphWidth / (_dataPoints.Count - 1));
            float y = graphBottom - ((_dataPoints[i].HeartRate - _minHeartRate) * graphHeight /
                                     (_maxHeartRate - _minHeartRate));

            if (isFirst)
            {
                linePath.MoveTo(x, y);
                areaPath.LineTo(x, y);
                isFirst = false;
            }
            else
            {
                // 使用曲线而不是直线，使图表更平滑
                if (i > 0 && i < _dataPoints.Count - 1)
                {
                    float prevX = graphLeft + ((i - 1) * graphWidth / (_dataPoints.Count - 1));
                    float prevY = graphBottom - ((_dataPoints[i - 1].HeartRate - _minHeartRate) * graphHeight /
                                                 (_maxHeartRate - _minHeartRate));
                    float nextX = graphLeft + ((i + 1) * graphWidth / (_dataPoints.Count - 1));
                    float nextY = graphBottom - ((_dataPoints[i + 1].HeartRate - _minHeartRate) * graphHeight /
                                                 (_maxHeartRate - _minHeartRate));

                    float cpx1 = prevX + (x - prevX) * 0.5f;
                    float cpy1 = prevY;
                    float cpx2 = x - (x - prevX) * 0.5f;
                    float cpy2 = y;

                    linePath.CurveTo(cpx1, cpy1, cpx2, cpy2, x, y);
                    areaPath.CurveTo(cpx1, cpy1, cpx2, cpy2, x, y);
                }
                else
                {
                    linePath.LineTo(x, y);
                    areaPath.LineTo(x, y);
                }
            }
        }

        // 完成区域填充路径
        areaPath.LineTo(graphLeft + graphWidth, graphBottom);
        areaPath.LineTo(graphLeft, graphBottom);
        areaPath.Close();

        // 绘制区域填充
        canvas.FillColor = _heartRateAreaColor;
        canvas.FillPath(areaPath);

        // 绘制曲线
        canvas.StrokeColor = _heartRateLineColor;
        canvas.StrokeSize = 3;
        canvas.DrawPath(linePath);

        // 只绘制最新数据点
        if (_dataPoints.Count > 0)
        {
            // 获取最新数据点的位置
            int lastIndex = _dataPoints.Count - 1;
            float x = graphLeft + (lastIndex * graphWidth / (_dataPoints.Count - 1));
            float y = graphBottom - ((_dataPoints[lastIndex].HeartRate - _minHeartRate) * graphHeight /
                                     (_maxHeartRate - _minHeartRate));

            // 绘制最新点的标记
            canvas.FillColor = _heartRatePointColor;
            canvas.FillCircle(x, y, 6);
            canvas.StrokeSize = 2;
            canvas.StrokeColor = Colors.White;
            canvas.DrawCircle(x, y, 6);

            // 显示最新心率值
            canvas.FontSize = 12;
            canvas.FontColor = _heartRateLineColor;
            //canvas.Font = FontAttributes.Bold;
            canvas.DrawString(_dataPoints[lastIndex].HeartRate + " bpm",
                x, y - 15, HorizontalAlignment.Center);
        }
    }
}

public partial class MainPage : ContentPage
{
    const string TAG = "HeartRateMonitor";

    // 心率服务和特征的UUID常量
    private static readonly Guid HEART_RATE_SERVICE_UUID = Guid.Parse("0000180D-0000-1000-8000-00805F9B34FB");

    private static readonly Guid HEART_RATE_MEASUREMENT_CHARACTERISTIC_UUID =
        Guid.Parse("00002A37-0000-1000-8000-00805F9B34FB");

    // 通知相关常量
    private const int NOTIFICATION_ID = 100;
    private const string CHANNEL_ID = "HeartRateMonitorChannel";

    // 图表更新定时器相关
    private IDispatcherTimer _graphUpdateTimer;
    private const int GRAPH_UPDATE_INTERVAL_MS = 1000; // 每秒更新一次图表
    private int _latestHeartRate = 0; // 保存最新心率值
    private bool _hasNewHeartRateData = false; // 标记是否有新数据

    // WebSocket相关
    private Services.WebSocketService.HeartRateWebSocketClient _webSocketClient;
    private bool _isWebSocketEnabled = false; // 是否启用WebSocket上报
    private const string DEFAULT_WEBSOCKET_URL = "wss://ws.nuanr-mxi.com/ws"; // 默认WebSocket服务器地址
    private string _webSocketUrl = DEFAULT_WEBSOCKET_URL;

    IAdapter _adapter;
    IBluetoothLE _ble;
    private bool _isConnecting = false; // 添加连接状态标志
    private bool _isRunningInBackground = false;

    // 心率数据相关
    private List<HeartRateDataPoint> _heartRateData = new List<HeartRateDataPoint>();
    private HeartRateGraphDrawable _heartRateGraph = new HeartRateGraphDrawable();
    private int _minHeartRate = 0;
    private int _maxHeartRate = 0;
    private double _avgHeartRate = 0;
    private DateTime _sessionStartTime;
    private IDevice _connectedDevice = null;
    private object _heartRateDataLock = new object(); // 添加锁对象，用于线程安全操作

    public MainPage()
    {
        InitializeComponent();

        _ble = CrossBluetoothLE.Current;
        _adapter = CrossBluetoothLE.Current.Adapter;

        _adapter.DeviceDiscovered += OnDeviceDiscovered;

        // 初始化图表
        heartRateGraphicsView.Drawable = _heartRateGraph;

        // 初始化定时器，用于固定频率更新图表
        InitializeGraphUpdateTimer();

        // 初始化通知服务
        NotificationService.Initialize();

        CheckBluetoothState();
    }


    // 初始化图表更新定时器
    private void InitializeGraphUpdateTimer()
    {
        _graphUpdateTimer = Dispatcher.CreateTimer();
        _graphUpdateTimer.Interval = TimeSpan.FromMilliseconds(GRAPH_UPDATE_INTERVAL_MS);
        _graphUpdateTimer.Tick += async (s, e) => await UpdateGraph();
        _graphUpdateTimer.Start();
    }

    // 定时更新图表
    private async Task UpdateGraph()
    {
        await SendHeartRateToServerAsync(_latestHeartRate);
        lock (_heartRateDataLock)
        {
            // 无论是否有新数据，都更新图表
            _heartRateGraph.UpdateData(_heartRateData);
            heartRateGraphicsView.Invalidate();

            // 重置新数据标记
            _hasNewHeartRateData = false;

            // 如果在后台运行且有数据，更新通知
            if (_isRunningInBackground && _heartRateData.Count > 0)
            {
                TimeSpan duration = DateTime.Now - _sessionStartTime;
                NotificationService.ShowHeartRateNotification(
                    _latestHeartRate,
                    _avgHeartRate,
                    _minHeartRate,
                    _maxHeartRate,
                    duration);
            }
        }
    }

    async void CheckBluetoothState()
    {
        Debug.WriteLine($"{TAG}: 检查 BLE 状态...");
        if (!_ble.IsAvailable)
        {
            Debug.WriteLine($"{TAG}: 设备不支持 BLE");
            statusLabel.Text = "设备不支持 BLE";
            return;
        }

        if (!_ble.IsOn)
        {
            Debug.WriteLine($"{TAG}: 蓝牙未开启");
            statusLabel.Text = "请开启蓝牙后再试";
            return;
        }

        Debug.WriteLine($"{TAG}: BLE 可用且已开启");
        statusLabel.Text = "准备就绪，点击开始扫描";
    }

    async void OnScanClicked(object sender, EventArgs e)
    {
        Debug.WriteLine($"{TAG}: 开始扫描附近设备...");
        statusLabel.Text = "正在扫描...";

        try
        {
            // 先停止之前的扫描
            if (_adapter.IsScanning)
            {
                Debug.WriteLine($"{TAG}: 停止之前的扫描");
                await _adapter.StopScanningForDevicesAsync();
                // 短暂延迟确保扫描完全停止
                await Task.Delay(200);
            }

            // 设置扫描参数
            _adapter.ScanMode = ScanMode.LowLatency; // 使用低延迟模式提高响应速度

            // 先尝试不带服务UUID过滤来扫描，这样可以捕获更多设备
            Debug.WriteLine($"{TAG}: 开始全扫描模式");
            await _adapter.StartScanningForDevicesAsync();

            Debug.WriteLine($"{TAG}: 扫描已启动，将自动超时或在发现心率设备时停止");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{TAG}: 扫描出错: {ex.Message}");
            statusLabel.Text = $"扫描出错: {ex.Message}";
        }
    }

    void OnDeviceDiscovered(object sender, DeviceEventArgs args)
    {
        var device = args.Device;
        Debug.WriteLine($"{TAG}: 发现设备: {device.Name ?? "未知设备"} ({device.Id})");

        foreach (var adv in device.AdvertisementRecords)
        {
            Debug.WriteLine($"{TAG}: Adv Type: {adv.Type}, Data: {BitConverter.ToString(adv.Data)}");
        }

        // 检查广播数据是否包含心率服务 UUID (0x180D)
        bool hasHeartRateService = false;

        // 检查16位UUID服务列表
        var serviceUuids16Bit = device.AdvertisementRecords.FirstOrDefault(r =>
            r.Type == AdvertisementRecordType.UuidsComplete16Bit ||
            r.Type == AdvertisementRecordType.UuidsIncomple16Bit);

        if (serviceUuids16Bit != null)
        {
            // 心率服务UUID是0x180D，根据日志，数据存储顺序为18-0D
            string dataString = BitConverter.ToString(serviceUuids16Bit.Data);
            Debug.WriteLine($"{TAG}: 16位UUID数据: {dataString}");
            hasHeartRateService = dataString.Contains("18-0D");

            if (hasHeartRateService)
            {
                Debug.WriteLine($"{TAG}: 在16位UUID中找到心率服务(0x180D)");
            }
        }

        // 如果16位列表中未找到，则检查128位UUID列表
        if (!hasHeartRateService)
        {
            var serviceUuids128Bit = device.AdvertisementRecords.FirstOrDefault(r =>
                r.Type == AdvertisementRecordType.UuidsComplete128Bit ||
                r.Type == AdvertisementRecordType.UuidsIncomplete128Bit);

            if (serviceUuids128Bit != null)
            {
                // 心率服务在128位UUID中的格式通常是0000180D-0000-1000-8000-00805F9B34FB
                // 检查两种可能的排列方式
                string dataString = BitConverter.ToString(serviceUuids128Bit.Data);
                Debug.WriteLine($"{TAG}: 128位UUID数据: {dataString}");
                hasHeartRateService = dataString.Contains("18-0D") || dataString.Contains("0D-18");

                if (hasHeartRateService)
                {
                    Debug.WriteLine($"{TAG}: 在128位UUID中找到心率服务(0x180D)");
                }
            }
        }

        // 检查设备名称，有些心率设备名称中包含相关信息
        if (!hasHeartRateService && !string.IsNullOrEmpty(device.Name))
        {
            string name = device.Name.ToLower();
            if (name.Contains("heart") || name.Contains("hr") || name.Contains("pulse") ||
                name.Contains("cardiac") || name.Contains("心率"))
            {
                hasHeartRateService = true;
            }
        }

        if (hasHeartRateService)
        {
            Debug.WriteLine($"{TAG}: 检测到心率设备: {device.Name ?? "未知设备"}");

            // 立即停止扫描 - 这是重要的，必须先停止扫描再连接
            if (_adapter.IsScanning)
            {
                try
                {
                    Debug.WriteLine($"{TAG}: 停止扫描以准备连接设备");
                    _adapter.StopScanningForDevicesAsync().ContinueWith(t =>
                    {
                        if (t.IsCompleted && !t.IsFaulted)
                        {
                            Debug.WriteLine($"{TAG}: 扫描已停止，准备连接设备");
                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                statusLabel.Text = $"检测到心率设备: {device.Name ?? "未知设备"}";
                                // 连接设备
                                await ConnectToHeartRateDeviceAsync(device);
                            });
                        }
                        else if (t.IsFaulted && t.Exception != null)
                        {
                            Debug.WriteLine($"{TAG}: 停止扫描失败: {t.Exception.Message}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{TAG}: 停止扫描时出错: {ex.Message}");
                }
            }
            else
            {
                // 如果没有扫描，直接连接
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    statusLabel.Text = $"检测到心率设备: {device.Name ?? "未知设备"}";
                    await ConnectToHeartRateDeviceAsync(device);
                });
            }

            // 尝试从广播数据中解析心率值（有些设备可能在广播中包含数据）
            var manufacturerData = device.AdvertisementRecords.FirstOrDefault(r =>
                r.Type == AdvertisementRecordType.ManufacturerSpecificData);

            if (manufacturerData != null && manufacturerData.Data.Length > 1)
            {
                int heartRate = manufacturerData.Data[1];
                Debug.WriteLine($"{TAG}: 广播心率值: {heartRate} bpm");
                MainThread.BeginInvokeOnMainThread(() => { heartRateLabel.Text = $"心率: {heartRate} bpm"; });
            }
            else
            {
                Debug.WriteLine($"{TAG}: 未在广播中找到心率值，将尝试连接设备读取");
            }

            // 返回true表示找到心率设备，不再继续处理其他设备
            return;
        }
    }

    // 更新通知
    private void UpdateNotification(int heartRate)
    {
        if (DeviceInfo.Platform != DevicePlatform.Android) return;

#if ANDROID
        var context = Android.App.Application.Context;

        // 创建PendingIntent用于点击通知时打开应用
        var intent = context.PackageManager.GetLaunchIntentForPackage(context.PackageName);
        var pendingIntent =
            Android.App.PendingIntent.GetActivity(context, 0, intent, Android.App.PendingIntentFlags.Immutable);

        // 创建通知内容
        var notificationBuilder = new AndroidX.Core.App.NotificationCompat.Builder(context, CHANNEL_ID)
            .SetContentTitle("心率监测")
            .SetContentText($"当前心率: {heartRate} bpm    平均: {_avgHeartRate:0} bpm")
            .SetSmallIcon(Resource.Drawable.notification_icon_background) // 使用Android自带图标，实际应用中应替换为自定义图标
            .SetOngoing(true)
            .SetContentIntent(pendingIntent)
            .SetPriority(AndroidX.Core.App.NotificationCompat.PriorityHigh);

        // 如果有统计数据，添加更多信息
        if (_heartRateData.Count > 1)
        {
            TimeSpan duration = DateTime.Now - _sessionStartTime;
            string timeInfo = $"监测时长: {duration.Hours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
            string statsInfo = $"最低: {_minHeartRate} bpm | 最高: {_maxHeartRate} bpm";

            notificationBuilder.SetStyle(new AndroidX.Core.App.NotificationCompat.BigTextStyle()
                .BigText($"当前心率: {heartRate} bpm\n{timeInfo}\n{statsInfo}"));
        }

        // 显示通知
        var notificationManager = AndroidX.Core.App.NotificationManagerCompat.From(context);
        notificationManager.Notify(NOTIFICATION_ID, notificationBuilder.Build());
#endif
    }

    // 切换后台模式
    async void OnBackgroundClicked(object sender, EventArgs e)
    {
        if (_connectedDevice == null || _connectedDevice.State != DeviceState.Connected)
        {
            await DisplayAlert("提示", "请先连接心率设备", "确定");
            return;
        }

        _isRunningInBackground = !_isRunningInBackground;

        if (_isRunningInBackground)
        {
            backgroundButton.Text = "停止后台运行";

            // 显示通知
            if (_heartRateData.Count > 0)
            {
                TimeSpan duration = DateTime.Now - _sessionStartTime;
                Services.NotificationService.ShowHeartRateNotification(
                    _latestHeartRate,
                    _avgHeartRate,
                    _minHeartRate,
                    _maxHeartRate,
                    duration);
            }
            else
            {
                Services.NotificationService.ShowHeartRateNotification(0, 0, 0, 0, TimeSpan.Zero);
            }

            // 通知用户应用将在后台运行
            await DisplayAlert("后台运行", "应用将在后台继续监测心率。可以通过通知栏查看实时数据。", "确定");
        }
        else
        {
            backgroundButton.Text = "后台运行";

            // 退出后台模式，如果应用在前台则恢复图表更新

            // 取消通知
            Services.NotificationService.CancelNotification();
        }
    }

    // 处理WebSocket设置按钮点击
    async void OnWebSocketSettingsClicked(object sender, EventArgs e)
    {
        string result = await DisplayPromptAsync(
            "配置数据上传",
            "请输入WebSocket服务器地址：\n格式：wss://example.com/ws",
            "确定",
            "取消",
            _webSocketUrl,
            maxLength: 100,
            keyboard: Keyboard.Url);

        //if (string.IsNullOrWhiteSpace(result)) return;

        if (!result.StartsWith("ws://") && !result.StartsWith("wss://"))
        {
            // fallback到默认websocket服务器
            result = _webSocketUrl;
        }

        try
        {
            // 更新按钮状态，显示正在连接
            webSocketButton.Text = "正在连接...";
            webSocketButton.IsEnabled = false;

            // 初始化WebSocket客户端
            await InitializeWebSocketClientAsync(result);

            if (_isWebSocketEnabled)
            {
                webSocketButton.Text = "数据上传已启用";
                webSocketButton.BackgroundColor = Color.FromArgb("#28A745"); // 绿色
                await DisplayAlert("连接成功", "心率数据将会实时上传到服务器", "确定");
                // 禁用按钮
                webSocketButton.IsEnabled = false;
            }
            else
            {
                webSocketButton.Text = "配置数据上传";
                webSocketButton.BackgroundColor = Color.FromArgb("#6C757D"); // 灰色
                await DisplayAlert("连接失败", "无法连接到指定的WebSocket服务器，请检查地址或网络连接", "确定");
            }
        }
        catch (Exception ex)
        {
            webSocketButton.Text = "配置数据上传";
            webSocketButton.BackgroundColor = Color.FromArgb("#6C757D"); // 灰色
            await DisplayAlert("错误", $"配置WebSocket时出错: {ex.Message}", "确定");
        }
        finally
        {
            webSocketButton.IsEnabled = true;
        }
    }

    async Task ConnectToHeartRateDeviceAsync(IDevice device)
    {
        // 防止重复连接
        if (_isConnecting)
        {
            Debug.WriteLine($"{TAG}: 已有连接请求正在进行中，忽略此次连接");
            return;
        }

        _isConnecting = true;

        try
        {
            // 确保扫描已停止 - 这一步非常重要
            if (_adapter.IsScanning)
            {
                Debug.WriteLine($"{TAG}: 连接前确保扫描已停止");
                await _adapter.StopScanningForDevicesAsync();
                // 短暂延迟确保扫描完全停止
                await Task.Delay(300);
            }

            statusLabel.Text = $"正在连接到 {device.Name ?? "未知设备"}...";
            Debug.WriteLine($"{TAG}: 正在连接到设备: {device.Name ?? "未知设备"}...");

            // 使用CancellationToken添加超时控制
            var cancelSource = new CancellationTokenSource();
            cancelSource.CancelAfter(TimeSpan.FromSeconds(15)); // 15秒连接超时

            // 连接到设备
            try
            {
                await _adapter.ConnectToDeviceAsync(device,
                    new ConnectParameters(autoConnect: false, forceBleTransport: true), cancelSource.Token);
                Debug.WriteLine($"{TAG}: 连接命令已发送，等待连接完成");
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"{TAG}: 连接操作超时");
                MainThread.BeginInvokeOnMainThread(() => { statusLabel.Text = "连接超时，请重试"; });
                return;
            }

            if (device.State == DeviceState.Connected)
            {
                statusLabel.Text = $"已连接到 {device.Name ?? "未知设备"}";
                Debug.WriteLine($"{TAG}: 已连接到设备: {device.Name ?? "未知设备"}");

                // 保存连接的设备引用
                _connectedDevice = device;

                // 重置心率数据
                _heartRateData.Clear();
                _minHeartRate = 0;
                _maxHeartRate = 0;
                _avgHeartRate = 0;
                minHeartRateLabel.Text = "--";
                maxHeartRateLabel.Text = "--";
                avgHeartRateLabel.Text = "--";
                noDataLabel.IsVisible = true;
                heartRateGraphicsView.Invalidate();

                // 获取心率服务
                Debug.WriteLine($"{TAG}: 尝试获取心率服务 {HEART_RATE_SERVICE_UUID}");
                var heartRateService = await device.GetServiceAsync(HEART_RATE_SERVICE_UUID);
                if (heartRateService == null)
                {
                    Debug.WriteLine($"{TAG}: 未找到心率服务");
                    statusLabel.Text = "未找到心率服务";
                    return;
                }

                Debug.WriteLine($"{TAG}: 已获取心率服务，尝试获取心率特征");
                // 获取心率特征
                var heartRateCharacteristic =
                    await heartRateService.GetCharacteristicAsync(HEART_RATE_MEASUREMENT_CHARACTERISTIC_UUID);
                if (heartRateCharacteristic == null)
                {
                    Debug.WriteLine($"{TAG}: 未找到心率特征");
                    statusLabel.Text = "未找到心率特征";
                    return;
                }

                // 订阅心率通知
                heartRateCharacteristic.ValueUpdated += (s, e) =>
                {
                    // 解析心率数据
                    var data = e.Characteristic.Value;
                    if (data == null || data.Length == 0)
                        return;

                    byte flags = data[0];
                    bool isHeartRateValueFormat16Bit = ((flags & 0x01) != 0);
                    int heartRate;

                    if (isHeartRateValueFormat16Bit && data.Length >= 3)
                    {
                        heartRate = BitConverter.ToUInt16(data, 1);
                    }
                    else if (data.Length >= 2)
                    {
                        heartRate = data[1];
                    }
                    else
                    {
                        return; // 数据不完整
                    }

                    //Debug.WriteLine($"{TAG}: 收到心率值: {heartRate} bpm");
                    MainThread.BeginInvokeOnMainThread(() => { UpdateHeartRateData(heartRate); });
                };

                // 开始接收通知
                await heartRateCharacteristic.StartUpdatesAsync();
                statusLabel.Text = "正在监测心率...";
            }
            else
            {
                Debug.WriteLine($"{TAG}: 连接失败");
                statusLabel.Text = "连接失败，请重试";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{TAG}: 连接错误: {ex.Message}");
            statusLabel.Text = $"连接错误: {ex.Message}";
        }
        finally
        {
            // 重置连接状态标志
            _isConnecting = false;
        }
    }

    // 释放资源
    ~MainPage()
    {
        if (_webSocketClient != null)
        {
            _webSocketClient.Dispose();
            _webSocketClient = null;
        }
    }

    // 更新心率数据和图表
    private void UpdateHeartRateData(int heartRate)
    {
        // 更新UI上的心率显示
        heartRateLabel.Text = $"心率: {heartRate} bpm";

        // 保存最新心率值，用于通知
        _latestHeartRate = heartRate;

        lock (_heartRateDataLock) // 使用锁确保线程安全
        {
            // 添加新的数据点
            var dataPoint = new HeartRateDataPoint
            {
                Timestamp = DateTime.Now,
                HeartRate = heartRate
            };

            // 如果是第一个数据点，记录会话开始时间
            if (_heartRateData.Count == 0)
            {
                _sessionStartTime = DateTime.Now;
                MainThread.BeginInvokeOnMainThread(() => { noDataLabel.IsVisible = false; });
            }

            _heartRateData.Add(dataPoint);

            // 限制数据点数量
            if (_heartRateData.Count > 100)
            {
                _heartRateData.RemoveAt(0);
            }

            // 更新统计信息
            if (_heartRateData.Count > 0)
            {
                _minHeartRate = _heartRateData.Min(p => p.HeartRate);
                _maxHeartRate = _heartRateData.Max(p => p.HeartRate);
                _avgHeartRate = _heartRateData.Average(p => p.HeartRate);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    minHeartRateLabel.Text = _minHeartRate.ToString();
                    maxHeartRateLabel.Text = _maxHeartRate.ToString();
                    avgHeartRateLabel.Text = _avgHeartRate.ToString("0");
                });
            }

            // 标记有新数据，等待定时器更新图表
            _hasNewHeartRateData = true;
        }
    }


    // 创建通知渠道（Android特有）
    private void CreateNotificationChannel()
    {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            var context = Android.App.Application.Context;
            var channel =
                new Android.App.NotificationChannel(CHANNEL_ID, "心率监测", Android.App.NotificationImportance.High)
                {
                    Description = "显示实时心率数据"
                };

            var notificationManager =
                context.GetSystemService(Android.Content.Context
                    .NotificationService) as Android.App.NotificationManager;
            notificationManager?.CreateNotificationChannel(channel);
        }
#endif
    }

    // 初始化WebSocket客户端
    private async Task InitializeWebSocketClientAsync(string url = null)
    {
        // 释放现有的WebSocket客户端
        if (_webSocketClient != null)
        {
            _webSocketClient.Dispose();
            _webSocketClient = null;
        }

        // 使用提供的URL或默认URL
        _webSocketUrl = string.IsNullOrEmpty(url) ? DEFAULT_WEBSOCKET_URL : url;

        try
        {
            _webSocketClient = new Services.WebSocketService.HeartRateWebSocketClient(_webSocketUrl);
            await _webSocketClient.ConnectAsync();
            _isWebSocketEnabled = true;
            Debug.WriteLine($"{TAG}: WebSocket客户端已初始化，连接到 {_webSocketUrl}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{TAG}: 初始化WebSocket客户端失败: {ex.Message}");
            _isWebSocketEnabled = false;
        }
    }

    // 发送心率数据到WebSocket服务器
    private async Task SendHeartRateToServerAsync(int heartRate)
    {
        if (!_isWebSocketEnabled || _webSocketClient == null) return;

        try
        {
            var data = new WebSocketService.HeartRateData
            {
                HeartRate = heartRate,
                Timestamp = DateTime.Now,
                DeviceName = _connectedDevice?.Name ?? "未知设备"
            };

            await _webSocketClient.SendHeartRateDataAsync(data);
            //Debug.WriteLine($"已发送心率数据 {heartRate} bpm 到服务器");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"发送心率数据失败: {ex.Message}");
        }
    }
}