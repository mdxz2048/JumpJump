# 构建与部署

## 环境要求

| 工具 | 版本 / 说明 |
|------|------------|
| macOS | 开发机 |
| Unity | `2022.3.62f3` |
| Xcode | 与 iOS 目标匹配的版本 |
| .NET SDK | 7+ （用于编译服务端） |
| iPad | 真机（已验证 iPad Air 11-inch） |
| Apple 证书 | Apple Development 证书 + 可用 Team（自动签名） |

**Unity 工程设置**（由 `StageThreeBuildTools.ConfigureIPadBuild()` 自动写入）：

- Product Name：`甜姐的跳跳棋`
- Bundle ID：`com.lvzhipeng.sweetjumpjump`
- iOS Team ID：`J3M89K2N56`
- iOS Target：`iPhoneAndiPad`，Target OS ≥ 13.0
- Orientation：Portrait
- Scripting Backend：IL2CPP

---

## 一、Unity 内验证

在 Unity 编辑器菜单执行：

- `Tools/SweetJumpJump/Verify Stage One`
- `Tools/SweetJumpJump/Verify Stage Two`
- `Tools/SweetJumpJump/Verify Stage Three`

命令行批处理：

```bash
'/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity' \
  -batchmode -nographics -quit \
  -projectPath '/path/to/SweetJumpJump' \
  -executeMethod SweetJumpJump.Editor.StageThreeBuildTools.VerifyStageThree \
  -logFile /tmp/sweetjumpjump_stage3.log
```

成功日志应包含：

```text
StageOneVerifier passed
StageTwoVerifier passed
StageThreeVerifier passed: stage one/two regressions, portrait iPad settings, and iOS export readiness.
```

---

## 二、导出 Xcode 工程

Unity 菜单：`Tools/SweetJumpJump/Export Xcode Project`

命令行：

```bash
'/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity' \
  -batchmode -nographics -quit \
  -projectPath '/path/to/SweetJumpJump' \
  -executeMethod SweetJumpJump.Editor.StageThreeBuildTools.ExportXcodeProject \
  -logFile /tmp/sweetjumpjump_export.log
```

导出目录：`Builds/iOS/`（不提交到 Git，其他机器需重新导出）。

---

## 三、Xcode 真机构建

```bash
'/Applications/Xcode.app/Contents/Developer/usr/bin/xcodebuild' \
  -project '/path/to/SweetJumpJump/Builds/iOS/Unity-iPhone.xcodeproj' \
  -scheme Unity-iPhone \
  -destination 'id=<IPAD_DEVICE_ID>' \
  -configuration Debug \
  -derivedDataPath /tmp/sweetjumpjump-ios-dd \
  -allowProvisioningUpdates \
  build
```

成功日志应包含：`** BUILD SUCCEEDED **`

常见非阻塞警告：`A 1024x1024 app store icon is required for iOS apps`（不影响真机安装，上架前补齐）。

---

## 四、安装到 iPad

查设备 ID：

```bash
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
/usr/bin/xcrun devicectl list devices
```

安装：

```bash
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
/usr/bin/xcrun devicectl device install app \
  --device <IPAD_DEVICE_ID> \
  /tmp/sweetjumpjump-ios-dd/Build/Products/Debug-iphoneos/甜姐的跳跳棋.app
```

启动：

```bash
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
/usr/bin/xcrun devicectl device process launch \
  --device <IPAD_DEVICE_ID> \
  com.lvzhipeng.sweetjumpjump
```

成功日志应包含：

```text
App installed
Launched application with com.lvzhipeng.sweetjumpjump bundle identifier.
```

---

## 五、服务端部署

### 本地开发

```bash
dotnet run --project Server/SweetJumpJump.Server.csproj -- 53333
```

访问 `http://127.0.0.1:53333/` 进入网页端。

### 生产部署（Linux 示例）

```bash
dotnet publish Server/SweetJumpJump.Server.csproj -c Release -o /opt/sweetjumpjump
# 确保 Web/ 目录在 /opt/sweetjumpjump/ 同级或通过环境变量指定路径
/opt/sweetjumpjump/SweetJumpJump.Server 53333
```

服务端在启动时自动查找 `Web/` 目录（当前目录或父目录），提供静态文件服务。

### 服务端默认账号

| 账号 | 密码 |
|------|------|
| `tian` | `tian` |
| `mdxz` | `mdxz` |

账号数据保存在内存，服务重启后恢复默认。

管理员密钥：`xiaozhi2048-admin`（首页连续点圆形标记 7 次进入管理入口）。

---

## 签名排查

遇到 `No Account for Team` 或 `No profiles for com.lvzhipeng.sweetjumpjump`：

1. 打开 Xcode，打开 `Builds/iOS/Unity-iPhone.xcodeproj`。
2. 选 `Unity-iPhone` target → `Signing & Capabilities`。
3. 登录 Apple ID，勾选 `Automatically manage signing`，选择可用 Team。
4. 确认 Bundle ID 为 `com.lvzhipeng.sweetjumpjump`。

如果 Team ID 变化，同步更新 `Assets/Scripts/Editor/StageThreeBuildTools.cs` 中的 `appleDeveloperTeamID`。
