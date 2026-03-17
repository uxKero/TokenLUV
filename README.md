# TokenLUV

![TokenLUV](native/TokenLuv.WinUI/Assets/TokenLUV♥.png)

Native WinUI 3 desktop app for Windows that monitors live AI token and credit usage from the system tray.

## What it does

- Compact tray-first widget inspired by CodexBar
- Opens above the tray icon when restored from the Windows notification area
- Uses Windows PasswordVault for API keys and admin keys
- Polls real provider data where a trustworthy API exists
- Marks providers honestly as `real`, `estimated`, `validated-only`, or `unsupported`
- Ships as a Windows desktop `.exe`

## Providers

- OpenRouter: real credits from `/credits` plus key quota hints from `/key`
- OpenAI: prefers local `~/.codex/auth.json` and the ChatGPT/Codex usage API; API keys are a legacy fallback
- Anthropic: prefers local `~/.claude/.credentials.json` and the Claude OAuth usage API; API/admin keys are a legacy fallback
- Gemini: uses explicit Gemini CLI Google OAuth, supports token refresh, and reads live quota windows; API keys are a legacy fallback
- Antigravity: local-only provider stub that detects whether the desktop runtime is running
- xAI: validated model access only, no public usage API

## First run

If you cloned the repo:

1. Install the .NET 8 Desktop Runtime and Windows App Runtime.
2. Double click `run-native.bat`.
3. TokenLUV will build and open.

If you downloaded a release zip:

1. Extract the zip.
2. Open `TokenLuv.WinUI.exe`.

## Useful files

- `run-native.bat`: build and launch locally
- `publish-release.ps1`: create a release zip
- `.github/workflows/release-winui.yml`: GitHub release workflow
- `native/TokenLuv.WinUI`: main WinUI app

## Release a zip

```powershell
.\publish-release.ps1 -Version v0.1.0
```
