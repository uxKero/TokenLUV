# Releasing TokenLUV

## GitHub Releases

TokenLUV is currently prepared for Windows distribution as an unpackaged WinUI 3 desktop app zipped from the verified `bin/Release/win-x64` output.

### Local release build

```powershell
.\publish-release.ps1 -Version v0.1.0
```

Outputs:

- `dist\TokenLUV-win-x64`
- `dist\TokenLUV-v0.1.0-win-x64.zip`

### What to upload

Upload the zip produced in `dist\` to a GitHub Release.

## Signed MSIX packages

TokenLUV can also be packaged as a signed `.msix` for sideload validation or Microsoft Store preparation.

### Local signed package

```powershell
.\publish-msix.ps1 -Version 0.1.0.0
```

Outputs:

- `dist\msix\x64\0.1.0.0\TokenLuv.WinUI_0.1.0.0_x64.msix`
- `dist\msix\x64\0.1.0.0\TokenLuv.WinUI.cer`
- `dist\msix\x64\0.1.0.0\Add-AppDevPackage.ps1`

By default, `publish-msix.ps1` creates or reuses a strict development code-signing certificate that matches the current `Package.appxmanifest` publisher. For CI or real distribution, pass a real PFX:

```powershell
.\publish-msix.ps1 -Version 0.1.0.0 -PfxPath .\signing\TokenLUV.pfx -PfxPassword $env:TOKENLUV_PFX_PASSWORD
```

### GitHub Actions

The Windows release workflow can publish the zip lane by itself and will also attach a signed `.msix` when these repository secrets are configured:

- `WINDOWS_SIGNING_PFX_BASE64`
- `WINDOWS_SIGNING_PFX_PASSWORD`

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

## Microsoft Store / MSIX roadmap

GitHub Releases are still the fastest shipping path today because the unpackaged `.exe` is the smoothest install story while provider auth flows keep evolving.

The Store lane is now technically in place as a signed MSIX build. The remaining work for a real Partner Center submission is:

1. Reserve the final app in Partner Center.
2. Replace the manifest identity and publisher with the Store-associated values.
3. Build the signed MSIX with the real signing certificate.
4. Upload the package to the Store.

Useful official references:

- Microsoft Learn: https://learn.microsoft.com/windows/apps/package-and-deploy/deploy-overview
- Microsoft Learn: https://learn.microsoft.com/windows/apps/package-and-deploy/
- Microsoft Learn: https://learn.microsoft.com/windows/apps/package-and-deploy/project-properties
