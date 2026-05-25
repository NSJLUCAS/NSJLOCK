# NSJ Lock

NSJ Lock 是一个 Windows 桌面音量保护工具，目标是作为 Sound Lock 的现代替代品。

当前版本会监控 Windows 默认输出设备的系统主音量。当保护开启，并且音量不等于用户设置的锁定音量时，应用会自动把系统音量调整到设定值。也就是说，系统音量高于或低于锁定值时，都会被拉回锁定值。

当前阶段只做主音量锁定保护，不实现复杂音频压缩器、单应用音量控制、会议软件 API 集成或云同步。

## 当前版本

当前仓库版本由 `src\NSJLock.App\NSJLock.App.csproj` 中的版本字段定义：

- `Version`
- `AssemblyVersion`
- `FileVersion`
- `InformationalVersion`

当前发布版本为 `1.0.1`。

## 当前功能

- 显示当前默认输出设备名称。
- 显示当前系统主音量。
- 设置锁定音量，例如 40%。
- 开启或关闭音量保护。
- 当系统主音量高于或低于锁定音量时，自动调整到设定值。
- 关闭主窗口后最小化到系统托盘。
- 托盘菜单支持打开主界面、开启/关闭保护、退出。
- 用户设置保存到本地配置文件，下次启动自动读取。
- 主界面支持简洁深色/浅色外观。

## 项目结构

```text
src/
  NSJLock.App/      WPF 主程序、主窗口、托盘和 ViewModel
  NSJLock.Audio/    默认输出设备读取、系统主音量控制、保护逻辑
  NSJLock.Config/   本地 JSON 配置读写
tests/
  NSJLock.Tests/    配置、音频保护、ViewModel 的自动化测试
```

配置文件默认保存到：

```text
%AppData%\NSJ Lock\settings.json
```

## 开发环境

- Windows
- .NET 8 SDK

本仓库开发时优先使用项目根目录下的本地 SDK：`.dotnet\dotnet.exe`。

如果机器已经全局安装 .NET 8 SDK，也可以直接使用 `dotnet`。
