# OneKey HDR

Windows 托盘常驻工具，按自定义全局快捷键切换显示器 HDR。使用 C# / .NET 8 WinForms，无第三方 NuGet 依赖。

## 使用

1. 双击 `dist\OneKeyHdr.exe`。发布版自带 .NET 运行时，不需要管理员权限。
2. 默认按 **Ctrl + Alt + H** 切换 HDR，也可以双击托盘图标或右键选择“切换 HDR”。图标可能收在右下角的隐藏图标区域。
3. 托盘右键 → **设置快捷键与显示器…**，选择修饰键和字母、数字或 F1–F11，再保存。快捷键被占用时会提示，原快捷键继续有效。
4. 可选择所有 HDR 显示器或指定显示器。所有显示器模式下，如果全部开启则统一关闭，否则统一开启；不支持 HDR 的屏幕会跳过。
5. 按需勾选 **登录 Windows 时启动**。默认不会设置开机启动。退出通过托盘菜单完成。

首次启动不会切换 HDR。持续按住快捷键不会反复触发；切换期间也会忽略重复操作。每次操作会重新枚举显示器，完成后读回状态并通过 Windows 通知提示结果；驱动报错或多屏部分失败会明确提示。通知被系统关闭时可能看不到气泡，可打开托盘菜单检查状态。

## 常驻方式

这是在当前用户登录后运行的托盘应用。Windows 系统服务运行在独立会话，无法直接提供当前用户的托盘和桌面快捷键，因此本项目使用用户会话后台进程实现常驻需求。注销用户后程序会结束。

使用 Windows `RegisterHotKey` 接收全局快捷键，不记录键盘输入。使用 `QueryDisplayConfig` / `DisplayConfigGetDeviceInfo` / `DisplayConfigSetDeviceInfo` 读取和切换显示器 HDR，不依赖 Xbox Game Bar 或模拟系统快捷键。新版系统优先使用 HDR 专用接口，旧版系统使用 Advanced Color 接口；实际可用性取决于系统、显卡驱动和显示设备。

建议使用 Windows 11；Windows 10 的兼容路径需要在对应系统实测。这是系统“使用 HDR”开关，不是游戏“自动 HDR”开关。

## 配置和卸载

- 配置文件：`%LOCALAPPDATA%\OneKeyHdr\settings.json`，保存快捷键和显示器设备路径。
- 开机启动：当前用户注册表 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 中的 `OneKeyHdr` 项。
- 移动 exe 后，请重新勾选开机启动以更新路径。
- 卸载：取消开机启动，退出程序，删除发布目录；需要时删除上述配置目录。

## 开发与发布

需要 Windows 和 .NET 8 SDK：

```powershell
dotnet build -c Release
dotnet run
.\publish.ps1
```

输出为 `dist\OneKeyHdr.exe`，自包含 Windows x64 单文件版本。

只读诊断（先退出托盘应用，避免默认快捷键冲突）：

```powershell
$report = Join-Path $env:TEMP 'one-key-hdr-diagnostic.json'
Start-Process .\dist\OneKeyHdr.exe -ArgumentList @('--diagnose', $report) -Wait
Get-Content $report -Encoding UTF8
```

诊断会检查原生结构尺寸、注册/释放默认快捷键，并读取显示器 HDR 能力和状态，不执行切换。

## 验收

- 已在本机编译、发布，原生显示配置读取与快捷键注册通过；读取到 Odyssey G7 支持 HDR、当前已开启，使用新版 HDR 接口。
- 实际按键触发、HDR 开关切换、托盘视觉、多显示器和登录启动仍需交互验收；只读诊断不代表完成这些验证。
- 手动检查：按默认快捷键并对照 Windows 显示设置确认状态改变，再按一次恢复；更改快捷键后确认旧组合失效、新组合生效；退出后确认快捷键释放。

接口参考：[RegisterHotKey](https://learn.microsoft.com/zh-cn/windows/win32/api/winuser/nf-winuser-registerhotkey)。显示器接口结构与枚举值已对照本机 Windows SDK 10.0.26100.0 的 `wingdi.h`。
