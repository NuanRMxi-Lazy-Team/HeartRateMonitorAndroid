using System.Diagnostics;
using HeartRateMonitorAndroid.Models;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;

namespace HeartRateMonitorAndroid.Services
{
    /// <summary>
    /// 蓝牙服务类，用于管理蓝牙设备连接和心率监测
    /// </summary>
    public class BluetoothService
    {
        private const string TAG = "BluetoothService";

        // 心率服务和特征的UUID常量
        private static readonly Guid HEART_RATE_SERVICE_UUID = Guid.Parse("0000180D-0000-1000-8000-00805F9B34FB");
        private static readonly Guid HEART_RATE_MEASUREMENT_CHARACTERISTIC_UUID = Guid.Parse("00002A37-0000-1000-8000-00805F9B34FB");

        private readonly IBluetoothLE _ble;
        private readonly IAdapter _adapter;
        private bool _isConnecting = false;
        private IDevice _connectedDevice = null;
        private int _lastHeartRate = 0;

        /// <summary>
        /// 心率数据更新事件
        /// </summary>
        public event Action<int> HeartRateUpdated;

        /// <summary>
        /// 蓝牙状态更新事件
        /// </summary>
        public event Action<string> StatusUpdated;

        /// <summary>
        /// 设备发现事件
        /// </summary>
        public event Action<IDevice> DeviceDiscovered;

        /// <summary>
        /// 当前连接的设备
        /// </summary>
        public IDevice ConnectedDevice => _connectedDevice;

        /// <summary>
        /// 蓝牙是否可用
        /// </summary>
        public bool IsBluetoothAvailable => _ble.IsAvailable && _ble.IsOn;

        /// <summary>
        /// 是否正在扫描
        /// </summary>
        public bool IsScanning => _adapter.IsScanning;

        /// <summary>
        /// 获取最新的心率值
        /// </summary>
        public int LastHeartRate => _lastHeartRate;

        /// <summary>
        /// 初始化蓝牙服务
        /// </summary>
        public BluetoothService()
        {
            _ble = CrossBluetoothLE.Current;
            _adapter = CrossBluetoothLE.Current.Adapter;

            // 注册设备发现事件
            _adapter.DeviceDiscovered += OnDeviceDiscovered;
        }

        /// <summary>
        /// 检查蓝牙状态
        /// </summary>
        /// <returns>状态信息</returns>
        public string CheckBluetoothState()
        {
            Debug.WriteLine($"{TAG}: 检查 BLE 状态...");

            if (!_ble.IsAvailable)
            {
                Debug.WriteLine($"{TAG}: 设备不支持 BLE");
                StatusUpdated?.Invoke("设备不支持 BLE");
                return "设备不支持 BLE";
            }

            if (!_ble.IsOn)
            {
                Debug.WriteLine($"{TAG}: 蓝牙未开启");
                StatusUpdated?.Invoke("请开启蓝牙后再试");
                return "请开启蓝牙后再试";
            }

            Debug.WriteLine($"{TAG}: BLE 可用且已开启");
            StatusUpdated?.Invoke("准备就绪，点击开始扫描");
            return "准备就绪，点击开始扫描";
        }

        /// <summary>
        /// 开始扫描设备
        /// </summary>
        public async Task StartScanAsync()
        {
            Debug.WriteLine($"{TAG}: 开始扫描附近设备...");
            StatusUpdated?.Invoke("正在扫描...");

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

                // 开始全扫描模式
                Debug.WriteLine($"{TAG}: 开始全扫描模式");
                await _adapter.StartScanningForDevicesAsync();

                Debug.WriteLine($"{TAG}: 扫描已启动，将自动超时或在发现心率设备时停止");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{TAG}: 扫描出错: {ex.Message}");
                StatusUpdated?.Invoke($"扫描出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止扫描设备
        /// </summary>
        public async Task StopScanAsync()
        {
            if (_adapter.IsScanning)
            {
                try
                {
                    Debug.WriteLine($"{TAG}: 停止扫描");
                    await _adapter.StopScanningForDevicesAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{TAG}: 停止扫描时出错: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 连接到心率设备
        /// </summary>
        /// <param name="device">要连接的设备</param>
        public async Task ConnectToDeviceAsync(IDevice device)
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
                // 确保扫描已停止
                if (_adapter.IsScanning)
                {
                    Debug.WriteLine($"{TAG}: 连接前确保扫描已停止");
                    await _adapter.StopScanningForDevicesAsync();
                    await Task.Delay(300); // 确保扫描完全停止
                }

                StatusUpdated?.Invoke($"正在连接到 {device.Name ?? "未知设备"}...");
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
                    StatusUpdated?.Invoke("连接超时，请重试");
                    return;
                }

                if (device.State == DeviceState.Connected)
                {
                    StatusUpdated?.Invoke($"已连接到 {device.Name ?? "未知设备"}");
                    Debug.WriteLine($"{TAG}: 已连接到设备: {device.Name ?? "未知设备"}");

                    // 保存连接的设备引用
                    _connectedDevice = device;

                    // 保存设备地址以供保活服务使用
                    SaveLastConnectedDeviceAddress(device.Id.ToString());

                    // 获取心率服务
                    Debug.WriteLine($"{TAG}: 尝试获取心率服务 {HEART_RATE_SERVICE_UUID}");
                    var heartRateService = await device.GetServiceAsync(HEART_RATE_SERVICE_UUID);
                    if (heartRateService == null)
                    {
                        Debug.WriteLine($"{TAG}: 未找到心率服务");
                        StatusUpdated?.Invoke("未找到心率服务");
                        return;
                    }

                    Debug.WriteLine($"{TAG}: 已获取心率服务，尝试获取心率特征");
                    // 获取心率特征
                    var heartRateCharacteristic = await heartRateService.GetCharacteristicAsync(HEART_RATE_MEASUREMENT_CHARACTERISTIC_UUID);
                    if (heartRateCharacteristic == null)
                    {
                        Debug.WriteLine($"{TAG}: 未找到心率特征");
                        StatusUpdated?.Invoke("未找到心率特征");
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

                        // 更新最新心率值
                        _lastHeartRate = heartRate;

                        // 触发心率更新事件
                        HeartRateUpdated?.Invoke(heartRate);
                    };

                    // 开始接收通知
                    await heartRateCharacteristic.StartUpdatesAsync();
                    StatusUpdated?.Invoke("正在监测心率...");
                }
                else
                {
                    Debug.WriteLine($"{TAG}: 连接失败");
                    StatusUpdated?.Invoke("连接失败，请重试");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{TAG}: 连接错误: {ex.Message}");
                StatusUpdated?.Invoke($"连接错误: {ex.Message}");
            }
            finally
            {
                // 重置连接状态标志
                _isConnecting = false;
            }
        }

        /// <summary>
        /// 断开设备连接
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (_connectedDevice != null && _connectedDevice.State == DeviceState.Connected)
            {
                try
                {
                    await _adapter.DisconnectDeviceAsync(_connectedDevice);
                    _connectedDevice = null;
                    StatusUpdated?.Invoke("设备已断开连接");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{TAG}: 断开连接时出错: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 设备发现事件处理
        /// </summary>
        private void OnDeviceDiscovered(object sender, DeviceEventArgs args)
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
                                DeviceDiscovered?.Invoke(device);
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
                    // 如果没有扫描，直接通知发现了设备
                    DeviceDiscovered?.Invoke(device);
                }

                // 尝试从广播数据中解析心率值（有些设备可能在广播中包含数据）
                var manufacturerData = device.AdvertisementRecords.FirstOrDefault(r =>
                    r.Type == AdvertisementRecordType.ManufacturerSpecificData);

                if (manufacturerData != null && manufacturerData.Data.Length > 1)
                {
                    int heartRate = manufacturerData.Data[1];
                    Debug.WriteLine($"{TAG}: 广播心率值: {heartRate} bpm");
                    HeartRateUpdated?.Invoke(heartRate);
                }
                else
                {
                    Debug.WriteLine($"{TAG}: 未在广播中找到心率值，将尝试连接设备读取");
                }
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_adapter != null && _adapter.IsScanning)
            {
                _adapter.StopScanningForDevicesAsync().Wait();
            }

            if (_connectedDevice != null && _connectedDevice.State == DeviceState.Connected)
            {
                _adapter.DisconnectDeviceAsync(_connectedDevice).Wait();
            }

            _adapter.DeviceDiscovered -= OnDeviceDiscovered;
        }

        /// <summary>
        /// 保存上次连接的设备地址
        /// </summary>
        private void SaveLastConnectedDeviceAddress(string deviceId)
        {
            try
            {
                // 使用Preferences来保存设备地址，这样可以跨平台工作
                Microsoft.Maui.Storage.Preferences.Set("LastConnectedDevice", deviceId);
                Debug.WriteLine($"{TAG}: 已保存设备地址: {deviceId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{TAG}: 保存设备地址失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取上次连接的设备地址
        /// </summary>
        public string GetLastConnectedDeviceAddress()
        {
            try
            {
                var deviceId = Microsoft.Maui.Storage.Preferences.Get("LastConnectedDevice", null);
                Debug.WriteLine($"{TAG}: 获取保存的设备地址: {deviceId ?? "无"}");
                return deviceId;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{TAG}: 获取设备地址失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 根据设备地址连接到设备
        /// </summary>
        public async Task<bool> ConnectToDeviceByAddressAsync(string deviceAddress)
        {
            try
            {
                Debug.WriteLine($"{TAG}: 尝试连接到设备地址: {deviceAddress}");
                
                // Plugin.BLE没有GetSystemConnectedOrPairedDevicesAsync方法
                // 我们需要通过扫描来查找设备
                Debug.WriteLine($"{TAG}: 开始扫描寻找目标设备");
                return false; // 返回false表示需要通过扫描来找到设备
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{TAG}: 按地址连接设备失败: {ex.Message}");
                return false;
            }
        }
    }
}
