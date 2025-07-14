using System.Collections.Generic;
using System.Linq;

namespace HeartRateMonitorAndroid.Models
{
    /// <summary>
    /// 心率会话数据，包含一次监测会话的所有数据和统计信息
    /// </summary>
    public class HeartRateSessionData
    {
        private readonly object _heartRateDataLock = new object(); // 线程安全操作的锁对象
        private List<HeartRateDataPoint> _heartRateData = new List<HeartRateDataPoint>();

        /// <summary>
        /// 会话开始时间
        /// </summary>
        public DateTime SessionStartTime { get; private set; }

        /// <summary>
        /// 最新心率值
        /// </summary>
        public int LatestHeartRate { get; private set; }

        /// <summary>
        /// 最小心率值
        /// </summary>
        public int MinHeartRate { get; private set; }

        /// <summary>
        /// 最大心率值
        /// </summary>
        public int MaxHeartRate { get; private set; }

        /// <summary>
        /// 平均心率值
        /// </summary>
        public double AverageHeartRate { get; private set; }

        /// <summary>
        /// 获取心率数据点列表的副本
        /// </summary>
        public List<HeartRateDataPoint> HeartRateData 
        { 
            get 
            { 
                lock (_heartRateDataLock)
                {
                    return _heartRateData.ToList();
                }
            } 
        }

        /// <summary>
        /// 是否有新的心率数据
        /// </summary>
        public bool HasNewHeartRateData { get; private set; }

        /// <summary>
        /// 初始化心率会话数据
        /// </summary>
        public HeartRateSessionData()
        {
            ResetData();
        }

        /// <summary>
        /// 重置会话数据
        /// </summary>
        public void ResetData()
        {
            lock (_heartRateDataLock)
            {
                _heartRateData.Clear();
                LatestHeartRate = 0;
                MinHeartRate = 0;
                MaxHeartRate = 0;
                AverageHeartRate = 0;
                HasNewHeartRateData = false;
            }
        }

        /// <summary>
        /// 添加新的心率数据点
        /// </summary>
        /// <param name="heartRate">心率值</param>
        public void AddHeartRate(int heartRate)
        {
            lock (_heartRateDataLock)
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
                    SessionStartTime = DateTime.Now;
                }

                _heartRateData.Add(dataPoint);
                LatestHeartRate = heartRate;

                // 限制数据点数量，保留最新的100个点
                if (_heartRateData.Count > 100)
                {
                    _heartRateData.RemoveAt(0);
                }

                // 更新统计信息
                if (_heartRateData.Count > 0)
                {
                    MinHeartRate = _heartRateData.Min(p => p.HeartRate);
                    MaxHeartRate = _heartRateData.Max(p => p.HeartRate);
                    AverageHeartRate = _heartRateData.Average(p => p.HeartRate);
                }

                HasNewHeartRateData = true;
            }
        }

        /// <summary>
        /// 重置新数据标记
        /// </summary>
        public void ResetNewDataFlag()
        {
            HasNewHeartRateData = false;
        }

        /// <summary>
        /// 获取当前会话的监测时长
        /// </summary>
        public TimeSpan GetSessionDuration()
        {
            if (_heartRateData.Count == 0)
                return TimeSpan.Zero;

            return DateTime.Now - SessionStartTime;
        }
    }
}
