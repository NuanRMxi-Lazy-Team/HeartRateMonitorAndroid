using Android.App;
using Android.App.Job;
using Android.Content;
using Android.OS;

namespace HeartRateMonitorAndroid.Platforms.Android
{
    [BroadcastReceiver(Name = "com.nuanrmxi.heartratemonitor.KeepAliveBroadcastReceiver",
        Enabled = true,
        Exported = true)]
    [IntentFilter(new[] {
        Intent.ActionBootCompleted,
        Intent.ActionUserPresent,
        Intent.ActionMyPackageReplaced,
        Intent.ActionPackageReplaced,
        "android.net.conn.CONNECTIVITY_CHANGE",
        Intent.ActionScreenOn,
        Intent.ActionScreenOff
    }, Priority = 1000)]
    public class KeepAliveBroadcastReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            var action = intent?.Action;
            System.Diagnostics.Debug.WriteLine($"KeepAliveBroadcastReceiver: 收到广播 {action}");

            try
            {
                switch (action)
                {
                    case Intent.ActionBootCompleted:
                        System.Diagnostics.Debug.WriteLine("设备启动完成，启动心率监测服务");
                        StartHeartRateService(context);
                        break;

                    case Intent.ActionUserPresent:
                        System.Diagnostics.Debug.WriteLine("用户解锁设备，检查服务状态");
                        CheckAndStartService(context);
                        break;

                    case Intent.ActionMyPackageReplaced:
                    case Intent.ActionPackageReplaced:
                        System.Diagnostics.Debug.WriteLine("应用包更新，重启服务");
                        StartHeartRateService(context);
                        break;

                    case "android.net.conn.CONNECTIVITY_CHANGE":
                        System.Diagnostics.Debug.WriteLine("网络连接状态改变，检查服务");
                        CheckAndStartService(context);
                        break;

                    case Intent.ActionScreenOn:
                        System.Diagnostics.Debug.WriteLine("屏幕亮起，检查服务状态");
                        CheckAndStartService(context);
                        break;

                    case Intent.ActionScreenOff:
                        System.Diagnostics.Debug.WriteLine("屏幕关闭，确保服务运行");
                        CheckAndStartService(context);
                        break;
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"KeepAliveBroadcastReceiver Error: {ex.Message}");
            }
        }

        private void StartHeartRateService(Context context)
        {
            try
            {
                var intent = new Intent(context, typeof(HeartRateKeepAliveService));
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    context.StartForegroundService(intent);
                }
                else
                {
                    context.StartService(intent);
                }

                // 同时启动JobScheduler
                ScheduleJobService(context);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartHeartRateService Error: {ex.Message}");
            }
        }

        private void CheckAndStartService(Context context)
        {
            try
            {
                if (!IsServiceRunning(context, typeof(HeartRateKeepAliveService)))
                {
                    System.Diagnostics.Debug.WriteLine("服务未运行，正在启动...");
                    StartHeartRateService(context);
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CheckAndStartService Error: {ex.Message}");
            }
        }

        private bool IsServiceRunning(Context context, System.Type serviceType)
        {
            try
            {
                var activityManager = context.GetSystemService(Context.ActivityService) as ActivityManager;
                var services = activityManager?.GetRunningServices(int.MaxValue);
                
                if (services != null)
                {
                    foreach (var service in services)
                    {
                        if (service.Service.ClassName.Contains(serviceType.Name))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IsServiceRunning Error: {ex.Message}");
            }
            
            return false;
        }

        private void ScheduleJobService(Context context)
        {
            try
            {
                var jobScheduler = context.GetSystemService(Context.JobSchedulerService) as JobScheduler;
                var jobInfo = new JobInfo.Builder(1002, new ComponentName(context, Java.Lang.Class.FromType(typeof(HeartRateJobService))))
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
    }
}
