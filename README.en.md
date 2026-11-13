# DSHView

> A ~1MB portable window that bundles Microsoft's fixed-version WebView2 runtime, so a **locked-down intranet Windows box without WebView2 and without admin rights** can still open the [DeepSeek Harness](https://github.com/deepseek-ai) web GUI.

English | [中文](README.md)

![Icons](docs/icons.png)

## Why this exists

The DeepSeek Harness web GUI needs a Chromium engine. The typical intranet situation is awkward:

- Win10/Server VMs usually have **no WebView2 runtime** (Server ships without it, and slimmed images often strip it)
- Installing software, requesting admin rights, or downloading a runtime from the internet is not allowed
- Shipping a whole Chrome/Edge is heavy for a box with 2–4GB of RAM

DSHView's approach: **put Microsoft's official fixed-version WebView2 runtime in the same folder as the executable** and point `browserExecutableFolder` at it. Nothing is registered, nothing lands in system directories, no admin rights are needed. Delete the folder and it is gone.

## Features

- **Zero install**: ships Chromium `157.0.4341.46` (fixed version); the target machine needs no WebView2 at all
- **Content-only window**: no address bar or buttons by default (`toolbar=1` brings them back)
- **Truly portable**: no registry writes, no service, nothing in system directories
- **Low memory**: GPU and background networking are disabled; ~129MB private memory on an empty page
- **Clipboard works**: intranet http origins are promoted to a secure context, so `navigator.clipboard` behaves
- **Four icon modes**: official artwork / follow system theme / fixed dark whale / fixed white whale
- **Self-diagnosing**: log file, self-test mode, and a startup failure dialog that names the file to edit

## Quick start

```
1. Unzip the whole folder onto the target machine (e.g. D:\DSHView\)
2. Edit `url` in dshview.ini
3. Double-click DSHView.exe
```

## Building

This repository ships **source only**: the runtime folder is 510MB and does not belong in git (GitHub rejects files over 100MB). The build script fetches what is missing.

```powershell
# Windows PowerShell 5.1 (ships with Windows)
powershell -ExecutionPolicy Bypass -File .\build.ps1
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Zip

# or PowerShell 7+
pwsh ./build.ps1 -Zip
```

What it does:

1. Downloads `WebView2.Runtime.X64` and `WebView2.Runtime.X64.Core` (which carries `msedge.dll`) from NuGet and merges them into `webview2/`
2. Pulls the three assemblies of the official `Microsoft.Web.WebView2` SDK
3. Compiles `src/Program.cs` with the in-box .NET Framework `csc.exe`
4. Assembles a folder you can copy straight to the target machine

No Visual Studio, no .NET SDK, no NuGet client required. The build machine needs access to `api.nuget.org`; the produced folder runs fully offline.

> Downloads resume with `curl -C -` because a 260MB pull over a flaky proxy gets truncated easily.

## Configuration (`dshview.ini`)

| Key | Default | Meaning |
|---|---|---|
| `url` | `http://127.0.0.1:3080/` | DSH Web address; include `?token=...` on first use or after a port change |
| `runtime` | `webview2` | bundled runtime; `system` uses the machine-installed WebView2 |
| `data` | `profile` | browser data directory (cookies, login state); deleting it clears the login |
| `zoom` | `1.0` | initial zoom |
| `toolbar` | `0` | `1` shows the address bar, back/forward and zoom |
| `icon` | `default` | `default` / `auto` / `light` / `dark` |
| `args` | empty | extra Chromium flags |
| `secureorigin` | `1` | promote http intranet origins to a secure context (needed for clipboard) |

The file is recreated automatically when missing.

## Icons

| Value | Artwork | Notes |
|---|---|---|
| `default` | Official DSH icon (rounded backdrop + whale) | default; readable on light and dark frames |
| `auto` | Follows the system theme: dark whale on light, white whale on dark | reads `AppsUseLightTheme` |
| `light` | Always the dark whale | for a light title bar |
| `dark` | Always the white whale | for a dark title bar |

The **static file icon** of the exe is always `default` (what Explorer shows); only the window and taskbar icon follow the setting.

## About tokens

- Authentication is **mandatory**, including on `127.0.0.1` — dsh has no "loopback is exempt" tier.
- But you only do it **once**: `GET /?token=...` writes a signed cookie bound to `host:port`, valid 30 days by default. Its signing secret is persisted, so **the cookie survives `dsh web` restarts**.
- The token itself cannot be pinned: it is a random per-process value held in memory, with no config key or CLI flag.
- To make the credential longer-lived, raise it on the server:

  ```yaml
  - name: '@deepseek-ai/dsh-client-connection'
    config:
      cookieMaxAgeDays: 3650
  ```

  The lifetime is written when the cookie is issued, so re-authenticate once after changing it.

## Deploying across two machines

Server (say `10.0.0.123`):

```powershell
dsh --profile web --host 10.0.0.123 --port 3080 --no-open
```

- Binding only `127.0.0.1` keeps it local; remote clients cannot reach it
- `--host 0.0.0.0` is rejected on purpose; name one concrete address
- It prints `dsh web: http://10.0.0.123:3080/?token=...` — hand that whole line to the client

Client `dshview.ini`:

```ini
url=http://10.0.0.123:3080/?token=xxxxxxxx   ; first run
url=http://10.0.0.123:3080/                  ; after authentication
```

When reaching it through a DNS name or reverse proxy, the server also needs `--trusted-host`, otherwise the Host check answers 403.

## Troubleshooting

```powershell
DSHView.exe --selftest --url=file:///C:/Windows/win.ini
```

This writes `selftest-result.json`; `"ok":true` with `"version":"157.0.4341.46"` means the host and kernel are fine and the problem is network or token related.

- `dshview.log` records every start, the resolved runtime folder, kernel version, icon choice, navigation result and stack traces
- On a runtime init failure the dialog lists every folder that was probed; verify `webview2\` contains `msedgewebview2.exe`, `msedge.dll` and `157.0.4341.46.manifest`
- Blank page with `no connection` / `Access denied` in the log: a security product is blocking Chromium's sandbox IPC; add `--no-sandbox` temporarily to confirm

## Layout

```
src/Program.cs        main program (WinForms + WebView2, C# 5, single file)
src/*.ico             three icon sets
src/Icon*.cs          icon generation helpers (not part of the main build)
src/IcoExtract.cs     extracts RT_GROUP_ICON from a PE verbatim
build.ps1             one-command build
dshview.ini           configuration template
docs/icons.png        icon preview
```

## Notes

- Icons come from the official DSH install (`resources/icon.png`) and DSH-Portable's icon resources, used only to make this tool recognisable
- This project is not affiliated with DeepSeek
- Prebuilt package: [Releases](https://github.com/ljh575/dshview-client/releases)

## License

[MIT](LICENSE)
