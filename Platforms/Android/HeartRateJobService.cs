using Android.App;
using Android.App.Job;
using Android.Content;
using Android.OS;

namespace HeartRateMonitorAndroid.Platforms.Android
{
    [Service(Name = "com.nuanrmxi.heartratemonitor.HeartRateJobService",
        Permission = "android.permission.BIND_JOB_SERVICE",
        Enabled = true,
        Exported = false)]
    public class HeartRateJobService : JobService
    {
        private const int JOB_ID = 1002;

        public override bool OnStartJob(JobParameters @params)
        {
            System.Diagnostics.Debug.WriteLine("HeartRateJobService: OnStartJob called");
            
            // 在后台线程中执行任务
            Task.Run(() =>
            {
                try
                {
                    CheckAndRestartService();
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"HeartRateJobService Error: {ex.Message}");
                }
                finally
                {
                    // 完成任务
                    JobFinished(@params, false);
                }
            });

            return true; // 返回true表示任务在后台执行
        }

        public override bool OnStopJob(JobParameters @params)
        {
            System.Diagnostics.Debug.WriteLine("HeartRateJobService: OnStopJob called");
            return false; // 返回false表示不需要重新调度
        }

        private void CheckAndRestartService()
        {
            try
            {
                // 检查前台服务是否正在运行
                if (!IsServiceRunning(typeof(HeartRateKeepAliveService)))
                {
                    System.Diagnostics.Debug.WriteLine("HeartRateJobService: 前台服务未运行，正在重启...");
                    
                    var intent = new Intent(this, typeof(HeartRateKeepAliveService));
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                    {
                        StartForegroundService(intent);
                    }
                    else
                    {
                        StartService(intent);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("HeartRateJobService: 前台服务正常运行");
                }

                // 重新调度下一次检查
                ScheduleNextJob();
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CheckAndRestartService Error: {ex.Message}");
            }
        }

        private bool IsServiceRunning(System.Type serviceType)
        {
            try
            {
                var activityManager = GetSystemService(ActivityService) as ActivityManager;
                var services = activityManager?.GetRunningServices(int.MaxValue);
                
                if (services != null)
                {
                    foreach (var service in services)
                    {
                        if (service.Service.ClassName.Equals(serviceType.FullName))
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

        private void ScheduleNextJob()
        {
            try
            {
                var jobScheduler = GetSystemService(JobSchedulerService) as JobScheduler;
                var jobInfo = new JobInfo.Builder(JOB_ID, new ComponentName(this, Java.Lang.Class.FromType(typeof(HeartRateJobService))))
                    .SetRequiredNetworkType(NetworkType.Any)
                    .SetPersisted(true)
                    .SetMinimumLatency(10 * 60 * 1000) // 最少10分钟后执行
                    .SetOverrideDeadline(15 * 60 * 1000) // 最多15分钟后必须执行
                    .SetRequiresCharging(false)
                    .SetRequiresDeviceIdle(false)
                    .Build();

                var result = jobScheduler?.Schedule(jobInfo);
                System.Diagnostics.Debug.WriteLine($"HeartRateJobService: 下次任务调度结果: {result}");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ScheduleNextJob Error: {ex.Message}");
            }
        }
    }
}
