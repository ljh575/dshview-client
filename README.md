# DSHView

> 一个 ~1MB 的绿色窗口程序，自带固定版 WebView2 运行时，让**没有 WebView2、不能装软件、内存紧张的内网 Windows 机器**也能用上 [DeepSeek Harness](https://github.com/deepseek-ai) 的 Web GUI。

[English](README.en.md) | 中文

## 为什么需要它

DeepSeek Harness 的 Web GUI 需要 Chromium 内核。典型的内网场景却很尴尬：

- Win10/Server 虚拟机**没有安装 WebView2 运行时**（Server 默认不带，精简镜像也常被裁掉）
- 不允许装软件、没有管理员权限、也连不上外网下载运行时
- 装一整个 Chrome/Edge 太重，而内存只有 2–4GB

DSHView 的做法是：**把微软官方固定版 WebView2 运行时和程序放在同一个文件夹里**，用 `browserExecutableFolder` 指过去，不写注册表、不进系统目录、不需要管理员权限。删掉文件夹即卸载。

## 特性

- **零安装**：自带 Chromium `157.0.4341.46` 固定版运行时，目标机无需任何 WebView2
- **纯内容窗口**：默认没有地址栏和按钮，只有页面本身（`toolbar=1` 可调出）
- **真正的绿色**：不写注册表、不装服务、不落系统目录
- **省内存**：默认关闭 GPU 与后台网络，空页面私有内存约 129MB
- **剪贴板可用**：自动把内网 http 源提升为安全上下文，`navigator.clipboard` 正常工作
- **四档图标**：官方图标 / 跟随系统主题 / 固定黑鲸 / 固定白鲸
- **关闭有询问、可记住**：点关闭弹出询问框，可选最小化到托盘（会话不中断）或直接退出，勾选「记住我的选择」后不再询问
- **托盘菜单**：显示窗口 / 复制当前地址 / 开发者工具 / 在浏览器中打开 / 设置 / 关于 / 退出
- **图形化设置与关于**：分段式设置卡片直接改写 ini（注释不丢），关于页可一键复制环境信息
- **中英双语**：菜单与对话框的语言跟随 dsh web 网页（读取页面的 `<html lang>`），也可用 `lang=` 固定
- **失败可自查**：日志、自检模式、启动失败时直接告诉你要改哪个文件

## 快速开始

```
1. 解压整个文件夹到目标机（例如 D:\DSHView\）
2. 编辑 dshview.ini 里的 url
3. 双击 DSHView.exe
```

## 构建

仓库**只放源码**：运行时目录有 510MB，不适合进 git（GitHub 单文件上限 100MB）。构建脚本会把缺的部分补回来。

```powershell
# Windows 自带的 Windows PowerShell 5.1
powershell -ExecutionPolicy Bypass -File .\build.ps1
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Zip

# 或者 PowerShell 7+
pwsh ./build.ps1 -Zip
```

**增量构建**：输出目录里已有完整运行时和三个 SDK 程序集时，会跳过 248MB 下载与 510MB 拷贝，只重新编译 exe（约 1 秒）。加 `-Force` 则重新组装运行时（`build/.cache` 里的包仍然复用，不会重新下载）。首次构建必须联网；之后只要缓存或输出目录还在，离线也能重建。

脚本做的事：

1. 从 NuGet 下载 `WebView2.Runtime.X64` 与 `WebView2.Runtime.X64.Core`（含 `msedge.dll`），合并成 `webview2/`
2. 从 `Microsoft.Web.WebView2` 取官方 SDK 的三个程序集
3. 用 Windows 自带的 .NET Framework 编译器 `csc.exe` 编译 `src/Program.cs`
4. 组装出可直接拷走的目录

不需要 Visual Studio、不需要 .NET SDK、不需要 NuGet 客户端；构建机需要能访问 `api.nuget.org`，产物运行完全离线。

> 下载环节对代理抖动做了断点续传（`curl -C -`），因为 260MB 的包在弱网下经常被截断。

## 配置（`dshview.ini`）

| 键 | 默认 | 说明 |
|---|---|---|
| `url` | `http://127.0.0.1:3080/` | DSH Web 地址；首次或换端口时带上 `?token=...` |
| `runtime` | `webview2` | 用自带运行时；`system` 表示用系统已装的 WebView2 |
| `data` | `profile` | 浏览器数据目录（存 cookie/登录态），删掉等于清空登录 |
| `zoom` | `1.0` | 初始缩放 |
| `toolbar` | `0` | `1` 显示地址栏与前进/后退/缩放 |
| `icon` | `default` | `default` / `auto` / `light` / `dark` |
| `lang` | `auto` | `auto` 跟随 dsh web 网页语言 / `zh` 始终中文 / `en` 始终英文 |
| `closeaction` | `ask` | `ask` 每次询问 / `tray` 直接最小化到托盘 / `quit` 直接退出（旧键 `closetotray=1` 在本键缺失时仍等价于 `tray`） |
| `args` | 空 | 追加给 Chromium 的参数 |
| `secureorigin` | `1` | 把 http 内网源提升为安全上下文（剪贴板需要） |

文件缺失时程序会自动重建。

## 图标

`icon` 四档。下表每行的图都是**同一选项在浅色底（左）与深色底（右）的实际效果**：

| 值 | 浅底 / 深底 | 说明 |
|---|---|---|
| `default` | ![default](docs/ic-default.png) | DSH 官方图标（圆角底 + 鲸鱼），默认值。自带浅色底，两种背景都看得清 |
| `auto` | ![auto](docs/ic-auto.png) | 跟随系统主题：浅色用黑鲸、深色用白鲸，读 `AppsUseLightTheme` |
| `light` | ![light](docs/ic-light.png) | 始终黑鲸。适合浅色标题栏；深色标题栏下会看不清 |
| `dark` | ![dark](docs/ic-dark.png) | 始终白鲸。适合深色标题栏；浅色标题栏下会看不清 |

exe 的**静态文件图标**固定是 `default`（资源管理器里看得到）；只有窗口/任务栏图标按设置切换。

## 关闭与托盘

默认 `closeaction=ask`：点关闭会**先弹一个询问框**，让你选这次是「最小化到托盘」还是「直接退出」，避免误点关闭丢掉正在看的会话。

- 选「最小化到托盘」：窗口从任务栏消失（不占任务栏位置），进程继续在后台运行，DSH 会话不会中断
- 选「直接退出」：关闭程序
- 点对话框的 ×、按 `Esc` 或选「取消」：什么都不做，窗口留着
- 勾选**「记住我的选择」**：程序会把这次的选择写进 `closeaction`（`tray` 或 `quit`），以后**不再询问**
- 想手动改回来：把 `closeaction` 写成 `ask` / `tray` / `quit`
- 双击托盘图标 → 恢复窗口（保留原来的最大化/普通状态）
- 右键托盘图标 → 「显示窗口」/「复制当前地址」/「开发者工具」/「在系统浏览器中打开」/「设置…」/「关于」/「退出」

> 托盘图标现在**始终存在**（不再只在最小化到托盘时出现），因为纯内容窗口没有地址栏，它是菜单的唯一入口。

## 设置与关于

托盘右键「设置…」打开设置窗口（`toolbar=1` 时工具栏右侧也有齿轮）：

- 分段：**连接**（地址、内核、安全上下文）/ **外观**（图标、缩放、地址栏）/ **行为**（关闭按钮、界面语言）/ **高级**（附加内核参数、数据目录）
- 底部：打开配置文件 · 取消 · 保存 · **保存并重启**
- 保存只改变化的键，走原文件逐行替换，**注释与顺序都不动**；地址/内核/数据目录/缩放这类改动需要重启，点「保存并重启」即可（新进程会等旧进程退出再启动，不会两个实例抢同一个 `profile`）

「关于」显示版本、WebView2 内核与来源、数据目录、配置文件、日志路径、项目地址与许可，并可「复制信息」一次性粘进工单。

**界面语言**：`lang=auto`（默认）时，菜单与对话框跟随 dsh web 网页的语言——程序读取 `document.documentElement.lang`（dsh 客户端会把当前语言镜像到这里，中文为 `zh-CN`）后切换中/英文。dsh 网页里**切换语言即时生效，不需要刷新窗口**：注入到页面里的观察器会盯住 `<html lang>` 等属性变化并通知宿主重新取值（页面加载后还会在 0/600/1200 ms 各探一次，兜住 dsh 主题/语言设置的延迟到达）。`lang=zh` / `lang=en` 可强制指定。

## 关于 token

- 访问 dsh 的 Web 界面**必须**认证，本机 `127.0.0.1` 也不例外——dsh 没有"回环地址免认证"这一层。
- **token 每次启动都会变**：它是 dsh 进程生成的随机启动令牌，不落盘，也没有环境变量或命令行开关能把它固定住。
- 但 token 只需要用**一次**：`GET /?token=...` 会被换成一张签名 cookie，之后浏览器靠 cookie 认证。
- cookie 绑定的是 **authority**（Host 头里的 `host:port`；https 下含协议），签名密钥持久化在服务端 `$DSH_HOME/.credentials.yaml`，默认有效期 30 天，**dsh web 重启后依然有效**（只要 authority 和密钥没变）。

### 固定端口（强烈建议）

`dsh web` 默认用 `--port 0` 取随机端口，**每次重启端口都变**；authority 一变，旧 cookie 立刻失效，客户端就得重新贴一条带新 token 的地址。把端口固定下来，authority 就稳定了：

```powershell
dsh web --host 10.0.0.123 --port 3080 --no-open
```

固定端口之后：

- dsh web 重启后，客户端里那条**旧地址照样能连**——哪怕里面的 token 早就过期。认证靠的是 cookie 而不是 URL 里的 token，过期 token 会被服务端忽略并重定向到 `./`。
- 只有 cookie 也失效时才需要重新贴一次新地址：换了 host / 端口 / 协议，超过 `cookieMaxAgeDays`，`$DSH_HOME/.credentials.yaml` 被删除或换了 home，或者客户端 `data` 目录被清空。
- `dshview.ini` 里的 `url` 只要 authority 不变就不用改，里面留着旧 token 也不会出问题。

cookie 寿命可以在服务端调长：

```yaml
- name: '@deepseek-ai/dsh-client-connection'
  config:
    cookieMaxAgeDays: 3650
```

有效期在签发时写死，改完要重新认证一次才生效；而且**只能调大**——调小会让已经签发的 cookie 立刻失效（校验要求 `expiresAt - issuedAt <= cookieMaxAgeDays`）。

## 部署到另一台机器

服务器（假设 `10.0.0.123`）：

```powershell
dsh web --host 10.0.0.123 --port 3080 --no-open
```

- 不能只绑 `127.0.0.1`，否则跨机连不上
- `--host 0.0.0.0` 不被支持，必须写具体地址
- 端口要**固定**（上面例子里的 `3080`）。`--port 0` 每次重启都换端口，旧 cookie 随之失效，客户端就得重新贴一条带 token 的地址；原因见上面「固定端口」
- 启动后会打印 `dsh web: http://10.0.0.123:3080/?token=...`，把这一整行给客户端

客户端 `dshview.ini`：

```ini
url=http://10.0.0.123:3080/?token=xxxxxxxx   ; 首次
url=http://10.0.0.123:3080/                  ; 认证后
```

用 DNS 名或反向代理时，服务器还要加 `--trusted-host`，否则会被 Host 校验拦成 403。

## 排障

```powershell
DSHView.exe --selftest --url=file:///C:/Windows/win.ini
```

生成 `selftest-result.json`，`"ok":true` 且 `"version":"157.0.4341.46"` 说明宿主与内核正常，问题在网络或 token。

- `dshview.log` 记录每次启动、运行时目录、内核版本、图标选择、导航结果与异常栈
- 提示运行时初始化失败时，对话框会列出所有尝试过的目录；确认 `webview2\` 里有 `msedgewebview2.exe`、`msedge.dll`、`157.0.4341.46.manifest`
- 页面空白且日志出现 `no connection` / `Access denied`：安全软件拦了 Chromium 沙箱 IPC，可临时加 `--no-sandbox` 定位

## 目录结构

```
src/Program.cs        主程序（WinForms + WebView2，C# 5，单文件）
src/*.ico             三套图标资源
src/Icon*.cs          图标生成/合成工具（生成 ico 用的，不参与主程序编译）
src/IcoExtract.cs     从 PE 里原样导出 RT_GROUP_ICON
build.ps1             一键构建
dshview.ini           配置模板
docs/ic-*.png        图标在浅底/深底下的效果图（README 表格用）
```

## 说明

- 图标取自 DSH 官方安装目录的 `resources/icon.png` 与 DSH-Portable 的图标资源，仅作本工具识别用途
- 本项目与 DeepSeek 官方无关
- 预编译包见 [Releases](https://github.com/ljh575/dshview-client/releases)

## License

[MIT](LICENSE)
