using Android.App;
using Android.Runtime;
using HeartRateMonitorAndroid.Services;

namespace HeartRateMonitorAndroid;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override void OnCreate()
    {
        base.OnCreate();
        
        // 设置Android平台特定的服务助手
        ServiceHelper.Current = new Platforms.Android.AndroidServiceHelper();
    }
}