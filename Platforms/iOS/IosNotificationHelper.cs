using Foundation;
using UserNotifications;

namespace HeartRateMonitorAndroid.Platforms.iOS
{
    // iOS平台特定的通知帮助类
    public static class IosNotificationHelper
    {
        // 请求通知权限
        public static async Task RequestNotificationPermission()
        {
            var options = UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound;
            var center = UNUserNotificationCenter.Current;
            var result = await center.RequestAuthorizationAsync(options);

            // 权限请求结果处理
            if (result.Item1)
            {
                // 已获得权限
                Console.WriteLine("通知权限获取成功");
            }
            else
            {
                // 权限被拒绝
                Console.WriteLine("通知权限被拒绝");
            }
        }

        // 显示本地通知
        public static void ShowNotification(string title, string body, double timeIntervalSeconds = 0.1)
        {
            var content = new UNMutableNotificationContent
            {
                Title = title,
                Body = body,
                Sound = UNNotificationSound.Default
            };

            // 设置触发器
            var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(timeIntervalSeconds, false);

            // 创建请求
            var requestId = Guid.NewGuid().ToString();
            var request = UNNotificationRequest.FromIdentifier(requestId, content, trigger);

            // 添加请求
            UNUserNotificationCenter.Current.AddNotificationRequest(request, (error) =>
            {
                if (error != null)
                {
                    Console.WriteLine($"通知发送失败: {error}");
                }
            });
        }

        // 取消所有通知
        public static void CancelAllNotifications()
        {
            UNUserNotificationCenter.Current.RemoveAllPendingNotificationRequests();
            UNUserNotificationCenter.Current.RemoveAllDeliveredNotifications();
        }
    }
}
