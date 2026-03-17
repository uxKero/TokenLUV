# TokenLUV

Native WinUI 3 desktop app for Windows that monitors live AI token and credit usage from the system tray.

## What it does

- Compact tray-first widget inspired by CodexBar
- Opens above the tray icon when restored from the Windows notification area
- Uses Windows PasswordVault for API keys and admin keys
- Polls real provider data where a trustworthy API exists
- Marks providers honestly as `real`, `estimated`, `validated-only`, or `unsupported`
- Ships today as a Windows desktop `.exe` outside the Store, with a Store/MSIX path documented for later
- Ships with two release lanes: direct `.exe` zip for GitHub Releases and signed `.msix` packaging for sideloading or Store prep

## Providers

- OpenRouter: real credits from `/credits` plus key quota hints from `/key`
- OpenAI: prefers local `~/.codex/auth.json` and the ChatGPT/Codex usage API; API keys are a legacy fallback
- Anthropic: prefers local `~/.claude/.credentials.json` and the Claude OAuth usage API; API/admin keys are a legacy fallback
- Gemini: uses explicit Gemini CLI Google OAuth, supports token refresh, and reads live quota windows; API keys are a legacy fallback
- Antigravity: local-only provider stub that detects whether the desktop runtime is running
- xAI: validated model access only, no public usage API

## Open the app

- Double click `run-native.bat` to rebuild and launch the WinUI app
- Double click `open-app.bat` to open the verified Release build directly
- Visible app folder: `TokenLUV`
- Real executable: `TokenLUV\TokenLuv.WinUI.exe`

## Release the app

- Local release build: `.\publish-release.ps1 -Version v0.1.0`
- Local MSIX build: `.\publish-msix.ps1 -Version 0.1.0.0`
- Release docs: `docs\RELEASING.md`
- Windows distribution notes: `docs\WINDOWS-DISTRIBUTION.md`
- Store packaging notes: `docs\MICROSOFT-STORE.md`
- GitHub Actions workflow: `.github\workflows\release-winui.yml`
- Current release lane: zip the verified WinUI Release output from `bin\x64\Release\...\win-x64`

## Distribution strategy

- Recommended now: GitHub Release zip from the verified Release build output
- Also ready now: signed MSIX for sideload validation and Store packaging rehearsal
- Recommended later: associate the package identity with Partner Center and switch the signed MSIX lane to the final Store identity

## Source

- Main project: `native\TokenLuv.WinUI`
- Launcher scripts: `run-native.bat`, `run-native.ps1`
