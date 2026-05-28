<img src="https://cdn.nodeimage.com/i/KolTK8AR6DCsDby6XjBL5U3RUEzAUHD1.webp" alt="KolTK8AR6DCsDby6XjBL5U3RUEzAUHD1.webp">


# NSJ Lock

NSJ Lock 是一个 Windows 桌面音量保护工具，用来保护系统主音量不被突然拉得过高或过低。

开启保护后，NSJ Lock 会监控 Windows 默认输出设备的系统主音量。你可以使用固定锁定模式把音量拉回设定值，也可以使用动态限制模式，在保留手动调节系统音量的同时，让应用在输出峰值过高时临时压低音量。

## 下载

最新版请从 GitHub Releases 下载：

```text
https://github.com/NSJLUCAS/NSJLOCK/releases
```

下载版本号文件，例如：

```text
NSJLock-v1.0.1-win-x64.exe
```

NSJ Lock 当前以 Windows x64 单文件 exe 形式发布。

## 功能

- 固定锁定模式：将系统主音量锁定到指定百分比。
- 动态限制模式：允许手动调系统音量，只在输出峰值过高时临时压低。
- 显示当前默认输出设备。
- 显示当前系统主音量。
- 显示当前输出峰值。
- 关闭主窗口后最小化到系统托盘。
- 可从托盘菜单开启/关闭保护或退出程序。
- 本地保存设置，下次启动自动读取。
- 支持简洁的深色/浅色界面。

## 使用方法

1. 从 Releases 下载最新的 `NSJLock-v<版本号>-win-x64.exe`。
2. 在 Windows 上运行 exe。
3. 选择固定锁定或动态限制模式。
4. 设置想锁定或限制的音量，例如 `40%`。
5. 开启音量保护。
6. 如果想让它后台运行，关闭主窗口即可最小化到托盘。

如果要完全退出程序，请在托盘菜单里选择退出。

## 隐私

NSJ Lock 是本地优先工具。

- 不需要账号。
- 不使用云同步。
- 不包含分析统计。
- 设置保存在你的电脑本地。

默认配置文件位置：

```text
%AppData%\NSJ Lock\settings.json
```

## 当前范围

NSJ Lock 当前专注于系统主音量保护。

当前包含：

- 固定锁定系统主音量。
- 基于输出峰值的动态限制。

暂不实现：

- 单应用音量控制
- 虚拟声卡或底层音频流 DSP
- 参会者级别的单独音量控制
- 会议软件 API 集成
- 云同步

## 开发

开发环境：

- Windows
- .NET 8 SDK

还原、构建、测试：

```powershell
dotnet restore NSJLock.sln
dotnet build NSJLock.sln
dotnet test NSJLock.sln
```

本地运行：

```powershell
dotnet run --project .\src\NSJLock.App\NSJLock.App.csproj
```

## 发布

发布流程见：

```text
docs\release.md
```

发布脚本：

```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File .\scripts\publish-single-file.ps1
```

发布产物生成到：

```text
artifacts\publish\NSJLock-single-win-x64
```

## 项目结构

```text
src/
  NSJLock.App/      WPF 主程序、主窗口、托盘和 ViewModel
  NSJLock.Audio/    音频设备访问和音量保护逻辑
  NSJLock.Config/   本地 JSON 设置
tests/
  NSJLock.Tests/    配置、音频、ViewModel 自动化测试
scripts/
  publish-single-file.ps1
  export-public.ps1
docs/
  release.md
```
