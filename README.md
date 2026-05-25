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

## 本地运行

在仓库根目录执行：

```powershell
.\.dotnet\dotnet.exe restore .\NSJLock.sln
.\.dotnet\dotnet.exe run --project .\src\NSJLock.App\NSJLock.App.csproj
```

如果不使用项目本地 SDK：

```powershell
dotnet restore NSJLock.sln
dotnet run --project src/NSJLock.App/NSJLock.App.csproj
```

## 自动化验证

在仓库根目录执行：

```powershell
.\.dotnet\dotnet.exe restore .\NSJLock.sln
.\.dotnet\dotnet.exe build .\NSJLock.sln
.\.dotnet\dotnet.exe test .\NSJLock.sln
```

如果不使用项目本地 SDK：

```powershell
dotnet restore NSJLock.sln
dotnet build NSJLock.sln
dotnet test NSJLock.sln
```

## 发布

发布前先在 `src\NSJLock.App\NSJLock.App.csproj` 中递增版本号：

```xml
<Version>1.0.1</Version>
<AssemblyVersion>1.0.1.0</AssemblyVersion>
<FileVersion>1.0.1.0</FileVersion>
<InformationalVersion>1.0.1</InformationalVersion>
```

发布统一使用仓库脚本：

```powershell
powershell.exe -ExecutionPolicy Bypass -NoProfile -File .\scripts\publish-single-file.ps1
```

发布目录固定为：

```text
artifacts\publish\NSJLock-single-win-x64
```

发布完成后，主发布目录保留：

```text
artifacts\publish\NSJLock-single-win-x64\NSJLock.exe
artifacts\publish\NSJLock-single-win-x64\NSJLock-v<版本号>-win-x64.exe
```

其中：

- `NSJLock.exe` 永远代表最新版，用于覆盖旧版入口、快捷方式和日常启动。
- `NSJLock-v<版本号>-win-x64.exe` 是当前版本归档文件，方便识别版本、规避 Windows 同名图标缓存，也方便回退。

发布脚本会自动把主发布目录中旧的 `NSJLock-v*-win-x64.exe` 移到：

```text
artifacts\publish\archive\NSJLock-single-win-x64
```

## 发布后检查

- `artifacts\publish\NSJLock-single-win-x64\NSJLock.exe` 已刷新。
- `artifacts\publish\NSJLock-single-win-x64\NSJLock-v<版本号>-win-x64.exe` 已生成。
- 文件版本信息与 `src\NSJLock.App\NSJLock.App.csproj` 中的版本一致。
- 主发布目录只保留 `NSJLock.exe` 和当前版本号 exe。
- 旧版本号 exe 已移动到 `artifacts\publish\archive\NSJLock-single-win-x64`。
- 工作区没有误纳入发布产物。

## 手动验收清单

- 默认输出设备名称显示正确。
- 当前主音量与 Windows 系统音量一致。
- 锁定音量设置为 40% 后，把 Windows 音量调高或调低后会被调回 40%。
- 关闭保护后，Windows 音量可以自由调整，不会被拉回锁定值。
- 重新开启保护后，音量锁定恢复。
- 关闭窗口后程序仍在系统托盘中运行。
- 托盘“打开主界面”可以恢复窗口。
- 托盘“开启/关闭保护”可以切换保护状态。
- 托盘“退出”可以结束程序。
- 重启应用后设置会自动读取。
