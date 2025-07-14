using HeartRateMonitorAndroid.Models;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HeartRateMonitorAndroid.UI
{
    /// <summary>
    /// 心率图表绘制类
    /// </summary>
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
}
