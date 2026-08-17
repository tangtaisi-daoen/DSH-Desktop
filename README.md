# DSH Desktop

> ⚠️ **非官方（Unofficial）第三方工具** —— 本项目不是 DeepSeek 官方客户端，与 DeepSeek 官方无隶属关系。

DeepSeek Harness 的非官方 Windows 桌面客户端。双击一个 EXE，直接看到官方 WebUI——不需要"启动器 → 打开浏览器"的中间步骤。

## 架构

```
DSH Desktop.exe
│
├── Windows 原生外壳（C# WinForms）
│   ├── 自绘标题栏（无边框窗口）
│   ├── 系统托盘（最小化 / 退出选项）
│   └── 启动失败时的轻量错误界面
│
├── 后台
│   └── dsh web（自己启动的进程树，Job Object 管理）
│
└── WebView2
    └── http://127.0.0.1:3080
        └── 官方 DSH Web UI（含你安装的所有插件）
```

**核心思路**：本项目只是薄壳。聊天界面、侧边栏、插件、设置等全部来自官方 `dsh web`，因此 DSH 上游 UI 如何大改都不需要跟着改——只有启动命令、端口这类外部接口变化时才需要动这一层。

## 功能

- 双击即用：自动检测并启动 `dsh web`，等待端口就绪后直接在窗口内显示官方 WebUI
- 已在运行时直接复用（不重复启动）；端口被其他程序占用时给出明确提示
- **精确停止**：只终止自己启动的那棵进程树（Job Object），不再扫描并误杀其他 `dsh` 相关 Node 进程（如 GitHub MCP 子进程）
- 系统托盘：关闭窗口 = 最小化到托盘（后端继续运行）；托盘菜单可选择"停止后端并退出"或"仅退出（保留后端）"
- 异常才显示错误界面：dsh 未安装 / 端口被占用 / 启动失败 → 重试 / 查看日志
- 深色主题无边框窗口，Per-Monitor DPI 感知

## 构建

前置：Windows 10/11 + [.NET SDK 8.0+](https://dotnet.microsoft.com/download) + [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)（Win11 通常自带）。

```powershell
dotnet restore --source packages   # 使用本地包（首次离线构建）；在线环境可直接 dotnet restore
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained false -o dist   # 发布到 dist/
```

产物要求：`DSH-Desktop.exe` 与 `whale.ico`（窗口/托盘图标）、`whale-white.png`（标题栏图标）同目录（csproj 已配置自动拷贝）。

## 使用

- 双击 `DSH-Desktop.exe`：自动完成"检测 → 启动 → 等待 → 加载 WebUI"
- 关闭窗口：最小化到系统托盘，后端继续运行
- 右键托盘图标：显示主窗口 / 停止后端并退出 / 仅退出（保留后端）
- 启动失败时：界面显示错误原因，可"重试"或"查看日志"（日志位于 `%TEMP%\dsh-desktop\`）

## 环境要求

- Windows 10/11（Win11 自动圆角窗口）
- 已安装 `dsh`：`npm install -g @deepseek-ai/dsh`，且 `dsh` 在 PATH 中
- 代理等启动环境变量请在系统/用户环境变量中配置（dsh 禁止在 `~/.dsh/.env` 中写代理变量）

## 与 DSH-Console 的关系

[DSH-Console](https://github.com/tangtaisi-daoen/DSH-Console) 是此项目的前身（原生启动器 + 浏览器打开 WebUI）。DSH Desktop 将前台升级为 WebView2 内嵌 UI，并重构了停止逻辑（进程树管理替代全量扫描）。

## 图标

鲸鱼图标取自 [deepseek-ai/deepseek-harness](https://github.com/deepseek-ai/deepseek-harness)（`apps/web/public/favicon.svg`）：
- `whale.ico`：原版黑色（应用/托盘图标）
- `whale-white.png`：深色主题用的白色变体（标题栏）

DeepSeek Harness 及其 Logo 为其各自所有者的财产，本项目为独立的第三方（非官方）工具。

## License

[MIT](LICENSE)
