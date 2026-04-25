# 甜姐的跳跳棋 / Sweet Jump Jump

本项目是一个 Unity iPad 竖屏本地跳跳棋游戏原型，当前已完成第一、二、三阶段，并已在 iPad Air 11-inch 真机上完成构建、安装与启动验证。

## 当前功能

- Splash、主菜单、游戏选项、房间列表、房间创建/编辑/删除。
- 标准 121 格六角星跳棋棋盘，6 个营地各 10 枚棋子。
- 默认房间：下方真人玩家 vs 上方普通 AI。
- 支持 2 人、4 人、6 人成对座位配置。
- 支持相邻移动、一子跳、空跳、多跳、完成移动、放弃移动、本回合悔棋。
- 支持初学者、普通、高级三档 AI。
- 支持催促提示、音效、背景音乐、胜利弹窗。
- 棋盘界面已按参考图方向调整为雪山背景、浅蓝六边形棋格、纹理棋子、底部半透明操作栏。
- iPad 竖屏 iOS 导出、Xcode 真机构建、iPad 安装与启动已验证。

## 工程信息

- Unity 版本：`2022.3.62f3`
- 主场景：`Assets/Scenes/MainScene.unity`
- Bundle ID：`com.lvzhipeng.sweetjumpjump`
- iOS 目标：iPad only、Portrait、IL2CPP
- 主要代码命名空间：`SweetJumpJump`

## 目录说明

- `Assets/Scenes/`：Unity 场景。
- `Assets/Scripts/SweetJumpJump/`：游戏运行时代码、规则、UI、存档。
- `Assets/Scripts/Editor/`：Unity 编辑器工具、阶段验证、iOS 导出工具。
- `Packages/`：Unity 包依赖声明。
- `ProjectSettings/`：Unity 工程设置。
- `docs/`：架构、构建、AI 交接文档。

## 不提交到 GitHub 的内容

这些目录/文件由 Unity、Xcode 或本机环境生成，已在 `.gitignore` 中排除：

- `Library/`
- `Logs/`
- `UserSettings/`
- `Builds/`
- `Temp/`
- Xcode `DerivedData/`、`.ipa`、`.xcarchive` 等构建产物

## 快速运行

1. 用 Unity `2022.3.62f3` 打开仓库根目录。
2. 打开 `Assets/Scenes/MainScene.unity`。
3. 点击 Play。

如果场景丢失或需要重建，可在 Unity 菜单执行：

- `Tools/SweetJumpJump/Create Or Update MainScene`

## 常用验证

在 Unity 菜单执行：

- `Tools/SweetJumpJump/Verify Stage One`
- `Tools/SweetJumpJump/Verify Stage Two`
- `Tools/SweetJumpJump/Verify Stage Three`

命令行验证示例：

```bash
'/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity' \
  -batchmode -nographics -quit \
  -projectPath '/path/to/SweetJumpJump' \
  -executeMethod SweetJumpJump.Editor.StageThreeBuildTools.VerifyStageThree \
  -logFile /tmp/sweetjumpjump_verify.log
```

更多细节见：

- `docs/ARCHITECTURE.md`
- `docs/BUILD_AND_DEPLOY.md`
- `docs/AI_HANDOFF.md`
