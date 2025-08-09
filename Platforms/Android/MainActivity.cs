using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Content;
using Android.Provider;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using HeartRateMonitorAndroid.Platforms.Android;

namespace HeartRateMonitorAndroid;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int BATTERY_OPTIMIZATION_REQUEST = 1001;
    private const int NOTIFICATION_PERMISSION_REQUEST = 1002;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        
        // 初始化保活功能
        InitializeKeepAlive();
    }

    private void InitializeKeepAlive()
    {
        try
        {
            // 请求忽略电池优化
            RequestIgnoreBatteryOptimization();
            
            // 请求通知权限（Android 13+）
            RequestNotificationPermission();
            
            // 启动前台服务
            StartHeartRateKeepAliveService();
            
            System.Diagnostics.Debug.WriteLine("保活功能初始化完成");
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"InitializeKeepAlive Error: {ex.Message}");
        }
    }

    private void RequestIgnoreBatteryOptimization()
    {
        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                var powerManager = GetSystemService(PowerService) as PowerManager;
                if (powerManager != null && !powerManager.IsIgnoringBatteryOptimizations(PackageName))
                {
                    var intent = new Intent(Settings.ActionRequestIgnoreBatteryOptimizations);
                    intent.SetData(Android.Net.Uri.Parse($"package:{PackageName}"));
                    StartActivityForResult(intent, BATTERY_OPTIMIZATION_REQUEST);
                }
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RequestIgnoreBatteryOptimization Error: {ex.Message}");
        }
    }

    private void RequestNotificationPermission()
    {
        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            {
                if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.PostNotifications) 
                    != Permission.Granted)
                {
                    ActivityCompat.RequestPermissions(this, 
                        new[] { Android.Manifest.Permission.PostNotifications }, 
                        NOTIFICATION_PERMISSION_REQUEST);
                }
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RequestNotificationPermission Error: {ex.Message}");
        }
    }

    private void StartHeartRateKeepAliveService()
    {
        try
        {
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
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"StartHeartRateKeepAliveService Error: {ex.Message}");
        }
    }

    protected override void OnResume()
    {
        base.OnResume();
        
        // 每次应用恢复时检查服务状态
        CheckServiceStatus();
    }

    private void CheckServiceStatus()
    {
        try
        {
            if (!IsServiceRunning(typeof(HeartRateKeepAliveService)))
            {
                System.Diagnostics.Debug.WriteLine("检测到服务未运行，正在重启...");
                StartHeartRateKeepAliveService();
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CheckServiceStatus Error: {ex.Message}");
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

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        
        switch (requestCode)
        {
            case NOTIFICATION_PERMISSION_REQUEST:
                if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
                {
                    System.Diagnostics.Debug.WriteLine("通知权限已授予");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("通知权限被拒绝");
                }
                break;
        }
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        
        switch (requestCode)
        {
            case BATTERY_OPTIMIZATION_REQUEST:
                System.Diagnostics.Debug.WriteLine($"电池优化请求结果: {resultCode}");
                break;
        }
    }
}