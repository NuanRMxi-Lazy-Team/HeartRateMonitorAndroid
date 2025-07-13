# Heart Rate Monitor Android
本程序理论可以在Android/iOS设备上运行，请自行编译，编译前请修改如下部分：

1. 请新建 `./Resources/Raw/token.txt` 来存储 `token`，请确保与服务端 `token` 一致。
2. 请修改位于 `./MainPage.xaml.cs` 的默认 `websocket` 服务器地址，除非你想每次启动软件时都手动输入一次服务器地址。

## 编译须知
- 本程序依赖dotnet 8.0.400 版本，请确认你有对应版本的 dotnetSDK。
- 本程序需要maui工作负载，请使用 `dotnet workload install maui` 来安装此负载。
- 本程序无法在Windows下运行，本程序不是UWP应用。