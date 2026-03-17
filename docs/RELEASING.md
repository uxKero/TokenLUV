# Releasing TokenLUV

TokenLUV is released as a Windows zip built from the verified WinUI Release output.

### Local release build

```powershell
.\publish-release.ps1 -Version v0.1.0
```

Outputs:

- `dist\TokenLUV-win-x64`
- `dist\TokenLUV-v0.1.0-win-x64.zip`

### What to upload

Upload the zip produced in `dist\` to a GitHub Release.

## Current prerequisite story

This release lane uses the verified Release output instead of `dotnet publish`, because the current publish lane is not yet stable for this app.

End users should have:

- .NET 8 Desktop Runtime
- Windows App Runtime 1.8

### End-user install flow

1. Download the zip.
2. Extract it anywhere.
3. Run `TokenLuv.WinUI.exe`.
4. Complete provider auth from Settings.

### Unsigned build caveat

Until the app is code-signed, Windows SmartScreen may warn on first launch.
