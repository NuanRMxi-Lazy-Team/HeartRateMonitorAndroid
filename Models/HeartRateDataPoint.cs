namespace HeartRateMonitorAndroid.Models
{
    /// <summary>
    /// 心率数据点类
    /// </summary>
    public class HeartRateDataPoint
    {
        public DateTime Timestamp { get; set; }
        public int HeartRate { get; set; }
    }
}
