# 甜姐的跳跳棋 · 第一、二、三阶段

## 当前完成范围

- Splash 3 秒自动跳转
- 主菜单
- 游戏选项入口与基础配置保存，支持默认规则、催促开关和催促间隔
- 房间列表、默认房间、新建房间、编辑房间、删除房间
- 121 格标准星形棋盘
- 默认房间：`Bottom` 真人 vs `Top` 普通 AI
- 房间可配置 2 人、4 人、6 人成对座位
- 一子跳、空跳、相邻移动、多跳、完成移动、放弃移动
- 本回合内悔棋
- 初学者、普通、高级三档 AI
- 真人玩家催促提示
- 两套背景主题：粉色糖果、薄荷花园
- 音效开关真实生效，包括按钮、选棋、移动、非法操作、催促、胜利
- 背景音乐开关真实生效，运行时循环播放
- 选中棋子、移动棋子、胜利弹窗有轻量动画反馈
- 棋盘界面已按参考图改为雪山背景、浅蓝六边形棋格、纹理棋子、底部半透明操作栏
- iPad 竖屏构建设置
- Unity 已成功导出 Xcode 工程
- Xcode 真机构建、iPad 安装与启动已成功
- 胜利判定
- `MainScene` 已创建并加入 Build Settings

## 在 Unity 中运行

1. 使用 Unity `2022.3.62f3` 打开项目目录：
   `/Users/lvzhipeng/Documents/Codex/2026-04-24/files-mentioned-by-the-user-prd/SweetJumpJump`
2. 打开场景：
   `Assets/Scenes/MainScene.unity`
3. 点击 Play

## 已做的程序化验证

- `MainScene` 可由编辑器脚本自动生成
- `StageOneVerifier` 已验证：
  - 棋盘共 121 格
  - 六个营地各 10 格
  - 默认房间配置正确
  - Bottom 真人先手
  - AI 能找到合法行动
- `StageTwoVerifier` 已验证：
  - 房间校验规则
  - 本回合内悔棋
  - 空跳规则下 AI 可行动
  - 三档 AI 都能自动选择行动
- `StageThreeBuildTools.VerifyStageThree` 已验证：
  - 第一、二阶段回归仍通过
  - iPad 默认竖屏设置
  - iOS 目标为 iPad
  - iOS 使用 IL2CPP
  - Xcode 导出前置条件可用
- Xcode 工程已成功导出到：
  `/Users/lvzhipeng/Documents/Codex/2026-04-24/files-mentioned-by-the-user-prd/SweetJumpJump/Builds/iOS`

## 真机状态

- 已检测到连接的 iPad Air 11-inch。
- Xcode 真机构建已成功。
- App 已成功安装到 iPad 并通过 `devicectl` 启动。
- 当前 Bundle ID：`com.lvzhipeng.sweetjumpjump`。
- 当前 Unity 导出脚本使用 Team ID：`J3M89K2N56`。

Unity 菜单入口：

- `Tools/SweetJumpJump/Create Or Update MainScene`
- `Tools/SweetJumpJump/Verify Stage One`
- `Tools/SweetJumpJump/Verify Stage Two`
- `Tools/SweetJumpJump/Configure iPad Build`
- `Tools/SweetJumpJump/Verify Stage Three`
- `Tools/SweetJumpJump/Export Xcode Project`

## 预留但未实现

- 联网对战
- 局域网对战
- 账号系统
- 广告
