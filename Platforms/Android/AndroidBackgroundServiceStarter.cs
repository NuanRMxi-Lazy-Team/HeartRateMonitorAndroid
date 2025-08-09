using Android.Content;
using HeartRateMonitorAndroid.Services;
using Application = Android.App.Application;
namespace HeartRateMonitorAndroid.Platforms.Android
{
    /// <summary>
    /// Android平台的后台服务启动器实现
    /// </summary>
    public class AndroidBackgroundServiceStarter : IBackgroundServiceStarter
    {
        public async Task StartServiceAsync()
        {
            try
            {
                var context = Platform.CurrentActivity?.ApplicationContext ?? Application.Context;
                var intent = new Intent(context, typeof(HeartRateKeepAliveService));
                context.StartForegroundService(intent);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AndroidBackgroundServiceStarter: 启动服务失败: {ex.Message}");
                throw;
            }
        }

        public async Task StopServiceAsync()
        {
            try
            {
                var context = Platform.CurrentActivity?.ApplicationContext ?? Application.Context;
                var intent = new Intent(context, typeof(HeartRateKeepAliveService));
                context.StopService(intent);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AndroidBackgroundServiceStarter: 停止服务失败: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// Android平台的服务助手实现
    /// </summary>
    public class AndroidServiceHelper : IServiceHelper
    {
        public IBackgroundServiceStarter BackgroundServiceStarter { get; }

        public AndroidServiceHelper()
        {
            BackgroundServiceStarter = new AndroidBackgroundServiceStarter();
        }
    }
}
