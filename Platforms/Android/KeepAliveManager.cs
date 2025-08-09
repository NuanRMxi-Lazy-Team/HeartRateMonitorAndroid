using Android.App;
using Android.App.Job;
using Android.Content;
using Android.OS;
using Android.Provider;
using AndroidX.Core.App;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Uri = Android.Net.Uri;

namespace HeartRateMonitorAndroid.Platforms.Android
{
    /// <summary>
    /// 保活管理器，负责统一管理所有保活功能
    /// </summary>
    public static class KeepAliveManager
    {
        private static bool _isInitialized = false;

        /// <summary>
        /// 初始化保活功能
        /// </summary>
        public static void Initialize(Context context)
        {
            if (_isInitialized) return;

            try
            {
                System.Diagnostics.Debug.WriteLine("KeepAliveManager: 开始初始化保活功能");

                // 1. 启动前台服务
                StartForegroundService(context);

                // 2. 调度JobScheduler任务
                ScheduleJobService(context);

                // 3. 注册广播接收器（通过代码方式补充）
                RegisterBroadcastReceiver(context);

                // 4. 请求系统权限
                RequestSystemPermissions(context);

                _isInitialized = true;
                System.Diagnostics.Debug.WriteLine("KeepAliveManager: 保活功能初始化完成");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"KeepAliveManager Initialize Error: {ex.Message}");
            }
        }

        /// <summary>
        /// 启动前台服务
        /// </summary>
        public static void StartForegroundService(Context context)
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
                System.Diagnostics.Debug.WriteLine("KeepAliveManager: 前台服务已启动");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartForegroundService Error: {ex.Message}");
            }
        }

        /// <summary>
        /// 调度JobScheduler任务
        /// </summary>
        public static void ScheduleJobService(Context context)
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

                var result = jobScheduler?.Schedule(jobInfo);
                System.Diagnostics.Debug.WriteLine($"KeepAliveManager: JobScheduler调度结果: {result}");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ScheduleJobService Error: {ex.Message}");
            }
        }

        /// <summary>
        /// 动态注册广播接收器
        /// </summary>
        public static void RegisterBroadcastReceiver(Context context)
        {
            try
            {
                var receiver = new KeepAliveBroadcastReceiver();
                var filter = new IntentFilter();
                
                // 添加需要监听的动作
                filter.AddAction(Intent.ActionUserPresent);
                filter.AddAction("android.net.conn.CONNECTIVITY_CHANGE");
                filter.AddAction(Intent.ActionScreenOn);
                filter.AddAction(Intent.ActionScreenOff);
                filter.Priority = 1000;

                if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
                {
                    context.RegisterReceiver(receiver, filter, ReceiverFlags.Exported);
                }
                else
                {
                    context.RegisterReceiver(receiver, filter);
                }

                System.Diagnostics.Debug.WriteLine("KeepAliveManager: 动态广播接收器已注册");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RegisterBroadcastReceiver Error: {ex.Message}");
            }
        }

        /// <summary>
        /// 请求系统权限
        /// </summary>
        public static void RequestSystemPermissions(Context context)
        {
            try
            {
                // 请求忽略电池优化
                RequestIgnoreBatteryOptimization(context);

                // 请求自启动权限（针对不同厂商）
                RequestAutoStartPermission(context);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RequestSystemPermissions Error: {ex.Message}");
            }
        }

        /// <summary>
        /// 请求忽略电池优化
        /// </summary>
        public static void RequestIgnoreBatteryOptimization(Context context)
        {
            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
                {
                    var powerManager = context.GetSystemService(Context.PowerService) as PowerManager;
                    if (powerManager != null && !powerManager.IsIgnoringBatteryOptimizations(context.PackageName))
                    {
                        var intent = new Intent(Settings.ActionRequestIgnoreBatteryOptimizations);
                        intent.SetData(Uri.Parse($"package:{context.PackageName}"));
                        intent.SetFlags(ActivityFlags.NewTask);
                        
                        if (intent.ResolveActivity(context.PackageManager) != null)
                        {
                            context.StartActivity(intent);
                            System.Diagnostics.Debug.WriteLine("KeepAliveManager: 请求电池优化豁免");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RequestIgnoreBatteryOptimization Error: {ex.Message}");
            }
        }

        /// <summary>
        /// 请求自启动权限（针对不同手机厂商）
        /// </summary>
        public static void RequestAutoStartPermission(Context context)
        {
            try
            {
                var manufacturer = Build.Manufacturer?.ToLower();
                Intent intent = null;

                switch (manufacturer)
                {
                    case "xiaomi":
                        intent = new Intent("miui.intent.action.APP_PERM_EDITOR");
                        intent.SetClassName("com.miui.securitycenter", 
                            "com.miui.permcenter.autostart.AutoStartManagementActivity");
                        intent.PutExtra("extra_pkgname", context.PackageName);
                        break;

                    case "oppo":
                        intent = new Intent();
                        intent.SetClassName("com.coloros.safecenter",
                            "com.coloros.safecenter.permission.startup.StartupAppListActivity");
                        break;

                    case "vivo":
                        intent = new Intent();
                        intent.SetClassName("com.vivo.permissionmanager",
                            "com.vivo.permissionmanager.activity.BgStartUpManagerActivity");
                        break;

                    case "honor":
                    case "huawei":
                        intent = new Intent();
                        intent.SetClassName("com.huawei.systemmanager",
                            "com.huawei.systemmanager.startupmgr.ui.StartupNormalAppListActivity");
                        break;

                    case "oneplus":
                        intent = new Intent();
                        intent.SetClassName("com.oneplus.security",
                            "com.oneplus.security.chainlaunch.view.ChainLaunchAppListActivity");
                        break;

                    case "letv":
                        intent = new Intent();
                        intent.SetClassName("com.letv.android.letvsafe",
                            "com.letv.android.letvsafe.AutobootManageActivity");
                        break;
                }

                if (intent != null)
                {
                    intent.SetFlags(ActivityFlags.NewTask);
                    if (intent.ResolveActivity(context.PackageManager) != null)
                    {
                        context.StartActivity(intent);
                        System.Diagnostics.Debug.WriteLine($"KeepAliveManager: 请求{manufacturer}自启动权限");
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RequestAutoStartPermission Error: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查服务是否运行
        /// </summary>
        public static bool IsServiceRunning(Context context, System.Type serviceType)
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

        /// <summary>
        /// 重启所有保活服务
        /// </summary>
        public static void RestartKeepAliveServices(Context context)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("KeepAliveManager: 重启保活服务");
                
                // 重启前台服务
                StartForegroundService(context);
                
                // 重新调度Job
                ScheduleJobService(context);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RestartKeepAliveServices Error: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止所有保活服务
        /// </summary>
        public static void StopKeepAliveServices(Context context)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("KeepAliveManager: 停止保活服务");
                
                // 停止前台服务
                var intent = new Intent(context, typeof(HeartRateKeepAliveService));
                context.StopService(intent);
                
                // 取消Job调度
                var jobScheduler = context.GetSystemService(Context.JobSchedulerService) as JobScheduler;
                jobScheduler?.Cancel(1002);
                
                _isInitialized = false;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StopKeepAliveServices Error: {ex.Message}");
            }
        }
    }
}
