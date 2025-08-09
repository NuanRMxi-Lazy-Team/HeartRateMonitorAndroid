using System.Diagnostics;
using HeartRateMonitorAndroid.Models;
using HeartRateMonitorAndroid.Services;
using HeartRateMonitorAndroid.UI;

namespace HeartRateMonitorAndroid;

public partial class MainPage : ContentPage
{
    private const string Tag = "HeartRateMonitor";
    private const int UIUpdateIntervalMs = 1000; // 每秒更新一次UI

    // UI更新定时器
    private IDispatcherTimer _uiUpdateTimer;

    // 数据��务
    private readonly IHeartRateDataService _dataService;

    // 心率数据图表
    private readonly HeartRateGraphDrawable _heartRateGraph = new();

    // 会话开始时间
    private DateTime _sessionStartTime = DateTime.Now;

    public MainPage()
    {
        InitializeComponent();

        // 获取数据服务实例
        _dataService = HeartRateDataService.Instance;

        // 初始化图表
        heartRateGraphicsView.Drawable = _heartRateGraph;

        // 订阅服务事件
        SubscribeToServiceEvents();

        // 初始化UI更新定时器
        InitializeUITimer();

        // 启动后台服务
        _ = StartBackgroundServiceAsync();
    }

    /// <summary>
    /// 订阅服务事件
    /// </summary>
    private void SubscribeToServiceEvents()
    {
        _dataService.HeartRateDataReceived += OnHeartRateDataReceived;
        _dataService.ServiceStatusChanged += OnServiceStatusChanged;
        _dataService.DeviceStatusChanged += OnDeviceStatusChanged;
    }

    /// <summary>
    /// 初始化UI更新定时器
    /// </summary>
    private void InitializeUITimer()
    {
        _uiUpdateTimer = Dispatcher.CreateTimer();
        _uiUpdateTimer.Interval = TimeSpan.FromMilliseconds(UIUpdateIntervalMs);
        _uiUpdateTimer.Tick += async (s, e) => await UpdateUI();
        _uiUpdateTimer.Start();
    }

    /// <summary>
    /// 启动后台服务
    /// </summary>
    private async Task StartBackgroundServiceAsync()
    {
        try
        {
            UpdateServiceStatusUI("正在启动后台服务...", false);
            await _dataService.StartBackgroundServiceAsync();
            Debug.WriteLine($"{Tag}: 后台服务启动请求已发送");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{Tag}: 启动后台服务失败: {ex.Message}");
            UpdateServiceStatusUI("服务启动失败", false);
        }
    }

    /// <summary>
    /// 更新UI界面
    /// </summary>
    private async Task UpdateUI()
    {
        var sessionData = _dataService.GetCurrentSessionData();
        
        // 更新图表
        if (sessionData.HasNewHeartRateData)
        {
            _heartRateGraph.UpdateData(sessionData.HeartRateData);
            heartRateGraphicsView.Invalidate();
            sessionData.ResetNewDataFlag();
        }

        // 更新会话时长
        var duration = DateTime.Now - _sessionStartTime;
        durationLabel.Text = $"{duration:hh\\:mm\\:ss}";

        // 更新数据点数
        dataPointsLabel.Text = sessionData.HeartRateData.Count.ToString();

        await Task.CompletedTask;
    }

    /// <summary>
    /// 心率数据接收事件处理
    /// </summary>
    private void OnHeartRateDataReceived(int heartRate)
    {
        MainThread.BeginInvokeOnMainThread(() => {
            try
            {
                var sessionData = _dataService.GetCurrentSessionData();

                // 更新当前心率显示
                heartRateLabel.Text = $"{heartRate} bpm";
                lastUpdateLabel.Text = $"更新时间: {DateTime.Now:HH:mm:ss}";

                // 隐藏无数据提示
                if (sessionData.HeartRateData.Count >= 1)
                {
                    noDataLayout.IsVisible = false;
                }

                // 更新统计信息
                minHeartRateLabel.Text = sessionData.MinHeartRate.ToString();
                maxHeartRateLabel.Text = sessionData.MaxHeartRate.ToString();
                avgHeartRateLabel.Text = sessionData.AverageHeartRate.ToString("0");

                //Debug.WriteLine($"{Tag}: UI已更新心率数据: {heartRate} bpm");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{Tag}: 更新心率UI失败: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// 服务状态变化事件处理
    /// </summary>
    private void OnServiceStatusChanged(ServiceStatus status)
    {
        MainThread.BeginInvokeOnMainThread(() => {
            try
            {
                UpdateServiceStatusUI(status.StatusMessage, status.IsRunning);
                UpdateUploadStatusUI(status.IsWebSocketConnected ? "已连接" : "未连接", status.IsWebSocketConnected);

                Debug.WriteLine($"{Tag}: 服务状态已更新: {status.StatusMessage}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{Tag}: 更新服务状态UI失败: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// 设备状态变化事件处理
    /// </summary>
    private void OnDeviceStatusChanged(DeviceConnectionStatus status)
    {
        MainThread.BeginInvokeOnMainThread(() => {
            try
            {
                connectionStatusLabel.Text = status.ConnectionMessage;

                // 如果设备重新连接，重置会话开始时间
                if (status.IsConnected && !string.IsNullOrEmpty(status.DeviceName))
                {
                    _sessionStartTime = DateTime.Now;
                    
                    // 重置UI��示
                    minHeartRateLabel.Text = "--";
                    maxHeartRateLabel.Text = "--";
                    avgHeartRateLabel.Text = "--";
                    noDataLayout.IsVisible = true;
                    heartRateGraphicsView.Invalidate();
                }

                Debug.WriteLine($"{Tag}: 设备状态已更新: {status.ConnectionMessage}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{Tag}: 更新设��状态UI失败: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// 更新服务状态UI
    /// </summary>
    private void UpdateServiceStatusUI(string status, bool isRunning)
    {
        serviceStatusLabel.Text = status;
        statusIndicator.Fill = isRunning ? Color.FromArgb("#28A745") : Color.FromArgb("#DC3545");
    }

    /// <summary>
    /// 更新数据上传状态UI
    /// </summary>
    private void UpdateUploadStatusUI(string status, bool isConnected)
    {
        uploadStatusLabel.Text = status;
        uploadStatusLabel.TextColor = isConnected ? Color.FromArgb("#28A745") : Color.FromArgb("#DC3545");
    }

    /// <summary>
    /// 页面卸载时清理资源
    /// </summary>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // 停止定时器
        _uiUpdateTimer?.Stop();

        // 取消订阅事件
        if (_dataService != null)
        {
            _dataService.HeartRateDataReceived -= OnHeartRateDataReceived;
            _dataService.ServiceStatusChanged -= OnServiceStatusChanged;
            _dataService.DeviceStatusChanged -= OnDeviceStatusChanged;
        }
    }
}
