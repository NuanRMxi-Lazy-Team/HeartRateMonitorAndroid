using System.Diagnostics;
using HeartRateMonitorAndroid.Models;
using HeartRateMonitorAndroid.Services;
using HeartRateMonitorAndroid.UI;

namespace HeartRateMonitorAndroid;

public partial class MainPage : ContentPage
{
    private const string Tag = "HeartRateMonitor";
    private const int GraphUpdateIntervalMs = 1000; // 每秒更新一次图表
    private const string DefaultWebsocketUrl = "wss://ws.nuanr-mxi.com/ws"; // 默认WebSocket服务器地址

    // 图表更新定时器
    private IDispatcherTimer _graphUpdateTimer;

    // WebSocket相关
    private WebSocketService.HeartRateWebSocketClient _webSocketClient;
    private bool _isWebSocketEnabled = false;
    private string _webSocketUrl = DefaultWebsocketUrl;

    // 后台模式标志
    private bool _isRunningInBackground = false;

    // 服务
    private readonly BluetoothService _bluetoothService;

    // 心率数据模型
    private readonly HeartRateSessionData _sessionData = new();
    private readonly HeartRateGraphDrawable _heartRateGraph = new();

    public MainPage()
    {
        InitializeComponent();

        // 初始化蓝牙服务
        _bluetoothService = new BluetoothService();
        _bluetoothService.StatusUpdated += UpdateStatus;
        _bluetoothService.HeartRateUpdated += UpdateHeartRate;
        _bluetoothService.DeviceDiscovered += OnHeartRateDeviceDiscovered;

        // 初始化图表
        heartRateGraphicsView.Drawable = _heartRateGraph;

        // 初始化图表更新定时器
        InitializeGraphUpdateTimer();

        // 检查蓝牙状态
        _bluetoothService.CheckBluetoothState();
    }

    /// <summary>
    /// 初始化图表更新定时器
    /// </summary>
    private void InitializeGraphUpdateTimer()
    {
        _graphUpdateTimer = Dispatcher.CreateTimer();
        _graphUpdateTimer.Interval = TimeSpan.FromMilliseconds(GraphUpdateIntervalMs);
        _graphUpdateTimer.Tick += async (s, e) => await UpdateGraph();
        _graphUpdateTimer.Start();
    }

    /// <summary>
    /// 更新图表和通知
    /// </summary>
    private async Task UpdateGraph()
    {
        // 发送心率数据到服务器
        await SendHeartRateToServerAsync(_sessionData.LatestHeartRate);

        // 更新图表
        _heartRateGraph.UpdateData(_sessionData.HeartRateData);
        heartRateGraphicsView.Invalidate();

        // 重置新数据标记
        _sessionData.ResetNewDataFlag();

        // 如果在后台运行且有数据，更新通知
        if (_isRunningInBackground && _sessionData.HeartRateData.Count > 0)
        {
            var duration = _sessionData.GetSessionDuration();
            NotificationService.ShowHeartRateNotification(
                _sessionData.LatestHeartRate,
                _sessionData.AverageHeartRate,
                _sessionData.MinHeartRate,
                _sessionData.MaxHeartRate,
                duration);
        }
    }

    /// <summary>
    /// 更新状态标签
    /// </summary>
    private void UpdateStatus(string status)
    {
        MainThread.BeginInvokeOnMainThread(() => {
            statusLabel.Text = status;
        });
    }

    /// <summary>
    /// 更新心率数据
    /// </summary>
    private void UpdateHeartRate(int heartRate)
    {
        MainThread.BeginInvokeOnMainThread(() => {
            // 更新UI上的心率显示
            heartRateLabel.Text = $"心率: {heartRate} BPM";

            // 添加新的心率数据点
            _sessionData.AddHeartRate(heartRate);

            // 如果是第一个数据点，隐藏提示标签
            if (_sessionData.HeartRateData.Count == 1)
            {
                noDataLabel.IsVisible = false;
            }

            // 更新统计信息显示
            minHeartRateLabel.Text = _sessionData.MinHeartRate.ToString();
            maxHeartRateLabel.Text = _sessionData.MaxHeartRate.ToString();
            avgHeartRateLabel.Text = _sessionData.AverageHeartRate.ToString("0");
        });
    }

    /// <summary>
    /// 扫描按钮点击事件
    /// </summary>
    private async void OnScanClicked(object sender, EventArgs e)
    {
        await _bluetoothService.StartScanAsync();
    }

    /// <summary>
    /// 心率设备发现事件处理
    /// </summary>
    private async void OnHeartRateDeviceDiscovered(Plugin.BLE.Abstractions.Contracts.IDevice device)
    {
        await MainThread.InvokeOnMainThreadAsync(async () => {
            statusLabel.Text = $"检测到心率设备: {device.Name ?? "未知设备"}";
            await _bluetoothService.ConnectToDeviceAsync(device);

            // 连接成功后重置心率数据显示
            if (_bluetoothService.ConnectedDevice != null)
            {
                _sessionData.ResetData();
                minHeartRateLabel.Text = "--";
                maxHeartRateLabel.Text = "--";
                avgHeartRateLabel.Text = "--";
                noDataLabel.IsVisible = true;
                heartRateGraphicsView.Invalidate();
            }
        });
    }

    /// <summary>
    /// 后台运行按钮点击事件
    /// </summary>
    private async void OnBackgroundClicked(object sender, EventArgs e)
    {
        if (_bluetoothService.ConnectedDevice == null)
        {
            await DisplayAlert("提示", "请先连接心率设备", "确定");
            return;
        }

        _isRunningInBackground = !_isRunningInBackground;

        if (_isRunningInBackground)
        {
            backgroundButton.Text = "停止后台运行";

            // 显示通知
            if (_sessionData.HeartRateData.Count > 0)
            {
                TimeSpan duration = _sessionData.GetSessionDuration();
                NotificationService.ShowHeartRateNotification(
                    _sessionData.LatestHeartRate,
                    _sessionData.AverageHeartRate,
                    _sessionData.MinHeartRate,
                    _sessionData.MaxHeartRate,
                    duration);
            }
            else
            {
                NotificationService.ShowHeartRateNotification(0, 0, 0, 0, TimeSpan.Zero);
            }

            // 通知用户应用将在后台运行
            await DisplayAlert("后台运行", "应用将在后台继续监测心率。可以通过通知栏查看实时数据。", "确定");
        }
        else
        {
            backgroundButton.Text = "后台运行";

            // 取消通知
            Services.NotificationService.CancelNotification();
        }
    }

    /// <summary>
    /// WebSocket设置按钮点击事件
    /// </summary>
    private async void OnWebSocketSettingsClicked(object sender, EventArgs e)
    {
        string result = await DisplayPromptAsync(
            "配置数据上传",
            "请输入WebSocket服务器地址：\n格式：wss://example.com/ws",
            "确定",
            "取消",
            _webSocketUrl,
            maxLength: 100,
            keyboard: Keyboard.Url);

        if (string.IsNullOrWhiteSpace(result)) return;

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

    /// <summary>
    /// 初始化WebSocket客户端
    /// </summary>
    private async Task InitializeWebSocketClientAsync(string url = null)
    {
        // 释放现有的WebSocket客户端
        if (_webSocketClient != null)
        {
            _webSocketClient.Dispose();
            _webSocketClient = null;
        }

        // 使用提供的URL或默认URL
        _webSocketUrl = string.IsNullOrEmpty(url) ? DefaultWebsocketUrl : url;

        try
        {
            _webSocketClient = new Services.WebSocketService.HeartRateWebSocketClient(_webSocketUrl);
            await _webSocketClient.ConnectAsync();
            _isWebSocketEnabled = true;
            Debug.WriteLine($"{Tag}: WebSocket客户端已初始化，连接到 {_webSocketUrl}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{Tag}: 初始化WebSocket客户端失败: {ex.Message}");
            _isWebSocketEnabled = false;
        }
    }

    /// <summary>
    /// 发送心率数据到WebSocket服务器
    /// </summary>
    private async Task SendHeartRateToServerAsync(int heartRate)
    {
        if (!_isWebSocketEnabled || _webSocketClient == null) return;

        try
        {
            var data = new WebSocketService.HeartRateData
            {
                HeartRate = heartRate,
                Timestamp = DateTime.Now,
                DeviceName = _bluetoothService.ConnectedDevice?.Name ?? "未知设备"
            };

            await _webSocketClient.SendHeartRateDataAsync(data);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"发送心率数据失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 页面卸载时清理资源
    /// </summary>
    ~MainPage()
    {
        if (_webSocketClient != null)
        {
            _webSocketClient.Dispose();
            _webSocketClient = null;
        }

        _bluetoothService.Dispose();
    }
}