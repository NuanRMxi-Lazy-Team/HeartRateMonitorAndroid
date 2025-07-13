using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;

namespace HeartRateMonitorAndroid.Services;

public class WebSocketService
{
    // 心率上报
    public class HeartRateWebSocketClient : IDisposable
    {
        private readonly string _serverUrl;
        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cts;
        private bool _isConnected = false;
        private int _reconnectDelayMs = 5000; // 初始重连延时5秒
        private readonly int _maxReconnectDelayMs = 60000; // 最大重连延时60秒
        private readonly object _lockObject = new object();
        private bool _isReconnecting = false; // 是否正在重连
        private int _reconnectAttempts = 0; // 重连尝试次数

        public HeartRateWebSocketClient(string serverUrl)
        {
            _serverUrl = serverUrl;
            _webSocket = new ClientWebSocket();

            // 配置WebSocket客户端选项，增强后台运行稳定性
            _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            _webSocket.Options.SetBuffer(8192, 8192); // 增加缓冲区大小

            // 在某些Android设备上，默认SubProtocol可能导致连接问题
            // _webSocket.Options.AddSubProtocol("json"); // 可以根据服务器要求添加子协议

            _cts = new CancellationTokenSource();
        }

        public async Task ConnectAsync()
        {
            if (_isConnected) return;

            try
            {
                Console.WriteLine($"正在连接到 WebSocket 服务器: {_serverUrl}");
                await _webSocket.ConnectAsync(new Uri(_serverUrl), _cts.Token);

                _isConnected = true;
                Console.WriteLine("已成功连接到 WebSocket 服务器");

                // 连接成功，重置重连参数
                _reconnectAttempts = 0;
                _reconnectDelayMs = 5000; // 重置为初始值

                // 启动接收消息的任务
                _ = ReceiveMessagesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"连接 WebSocket 服务器失败: {ex.Message}");
                if (!_isReconnecting) // 只有在不是重连过程中才触发重连
                {
                    await ReconnectAsync();
                }
            }
        }

        private async Task ReceiveMessagesAsync()
        {
            var buffer = new byte[4096];
            try
            {
                while (_webSocket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                        _isConnected = false;
                        await ReconnectAsync();
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Console.WriteLine($"收到服务器消息: {message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"接收消息时出错: {ex.Message}");
                _isConnected = false;
                await ReconnectAsync();
            }
        }

        private async Task ReconnectAsync()
        {
            if (_cts.IsCancellationRequested) return;

            // 检查是否已经在重连中，避免多次重连
            lock (_lockObject)
            {
                if (_isReconnecting)
                {
                    Console.WriteLine("已经有重连任务在进行中，跳过此次重连请求");
                    return;
                }
                _isReconnecting = true;
            }

            try
            {
                // 增加重连次数
                _reconnectAttempts++;

                // 使用指数退避策略增加等待时间
                // 每次重连失败后，等待时间翻倍，但不超过最大值
                _reconnectDelayMs = Math.Min(_reconnectDelayMs * 2, _maxReconnectDelayMs);

                Console.WriteLine($"第{_reconnectAttempts}次重连尝试，等待{_reconnectDelayMs / 1000}秒...");

                // 如果重连次数超过特定阈值，显示通知提醒用户
                if (_reconnectAttempts == 3 || _reconnectAttempts == 5 || _reconnectAttempts % 10 == 0)
                {
                    await ShowReconnectionNotification();
                }

                lock (_lockObject)
                {
                    if (_webSocket.State != WebSocketState.Open && _webSocket.State != WebSocketState.Connecting)
                    {
                        _webSocket.Dispose();
                        _webSocket = new ClientWebSocket();

                        // 设置WebSocket选项以提高后台连接可靠性
                        _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
                        _webSocket.Options.SetBuffer(8192, 8192); // 增加缓冲区大小
                    }
                }

                await Task.Delay(_reconnectDelayMs);
                await ConnectAsync();

                // 连接成功，重置重连计数和延迟
                if (_isConnected)
                {
                    _reconnectAttempts = 0;
                    _reconnectDelayMs = 5000; // 重置为初始值
                    Console.WriteLine("重连成功，重置重连参数");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"重连过程中发生错误: {ex.Message}");
                // 使用当前的延迟时间再次尝试
                await Task.Delay(_reconnectDelayMs);
                // 释放重连锁，允许下次重连
                lock (_lockObject) { _isReconnecting = false; }
                await ReconnectAsync();
            }
            finally
            {
                // 确保重连锁被释放
                lock (_lockObject) { _isReconnecting = false; }
            }
        }

        // 显示重连通知
        private async Task ShowReconnectionNotification()
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var title = "连接中断";
                    var message = $"服务器连接已断开，正在尝试第{_reconnectAttempts}次重连。";

                    // 使用应用程序的通知服务显示通知
                    HeartRateMonitorAndroid.Services.NotificationService.ShowReconnectionNotification(
                        title, 
                        message, 
                        _reconnectAttempts);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"显示重连通知失败: {ex.Message}");
            }
        }

        public async Task SendHeartRateDataAsync(HeartRateData data)
        {
            // 检查WebSocket状态
            if (_webSocket.State != WebSocketState.Open || !_isConnected)
            {
                Console.WriteLine($"WebSocket未连接，当前状态: {_webSocket.State}，尝试重新连接");
                _isConnected = false;

                // 如果已经在重连过程中，不要再次尝试连接
                if (!_isReconnecting)
                {
                    await ConnectAsync();
                }

                if (!_isConnected) 
                {
                    Console.WriteLine("重连失败，无法发送数据");

                    // 如果重连次数超过阈值，显示连接失败通知
                    if (_reconnectAttempts >= 3 && !_isReconnecting)
                    {
                        await ShowReconnectionNotification();
                        // 触发重连
                        await ReconnectAsync();
                    }
                    return;
                }
            }

            try
            {
                var json = JsonConvert.SerializeObject(data);
                var buffer = Encoding.UTF8.GetBytes(json);

                // 设置发送超时
                var sendCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                sendCts.CancelAfter(TimeSpan.FromSeconds(5)); // 5秒超时

                await _webSocket.SendAsync(
                    new ArraySegment<byte>(buffer), 
                    WebSocketMessageType.Text, 
                    true, 
                    sendCts.Token);

                //Console.WriteLine($"成功发送心率数据: {data.HeartRate} bpm");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("发送数据超时");
                _isConnected = false;
                await ReconnectAsync();
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                Console.WriteLine("WebSocket连接已关闭，尝试重新连接");
                _isConnected = false;
                await ReconnectAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发送心率数据失败: {ex.Message}, 类型: {ex.GetType().Name}");
                _isConnected = false;
                await ReconnectAsync();
            }
        }

        public void Dispose()
        {
            try
            {
                _cts.Cancel();
                if (_webSocket.State == WebSocketState.Open)
                {
                    _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "客户端关闭", CancellationToken.None)
                        .Wait(TimeSpan.FromSeconds(2));
                }
                _webSocket.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"关闭WebSocket客户端时出错: {ex.Message}");
            }
        }
    }
    public class HeartRateData
    {
        public int HeartRate { get; set; }
        public DateTime Timestamp { get; set; }
        public string DeviceName { get; set; }
        public string Token
        {
            get
            {
                using var stream = FileSystem.OpenAppPackageFileAsync("token.txt").Result;
                using var reader = new StreamReader(stream);

                var contents = reader.ReadToEnd();
                return contents;
            }
        }
    }
}