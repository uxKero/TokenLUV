# Microsoft Store Preparation

TokenLUV now has a real MSIX packaging lane, but the repo still ships day-to-day as an unpackaged tray app because that is the best iteration loop while auth and provider flows keep changing.

## What is already real

- Signed `.msix` packaging via `publish-msix.ps1`
- GitHub Actions support for attaching signed MSIX artifacts when a PFX certificate is provided
- Existing `Package.appxmanifest` and WinUI packaging metadata

## What still changes before Store submission

1. Reserve the app in Partner Center.
2. Replace the current development identity in `native\TokenLuv.WinUI\Package.appxmanifest`:
   - `Identity Name`
   - `Identity Publisher`
3. Sign with the real certificate that matches the final Store identity.
4. Upload the signed package to Partner Center.

## Local package build

```powershell
.\publish-msix.ps1 -Version 0.1.0.0
```

That creates a signed sideload package under `dist\msix\`.

## Build with a real certificate

```powershell
.\publish-msix.ps1 -Version 0.1.0.0 -PfxPath .\signing\TokenLUV.pfx -PfxPassword $env:TOKENLUV_PFX_PASSWORD
```

## GitHub workflow secrets

If you want GitHub Actions to emit signed MSIX artifacts, configure:

- `WINDOWS_SIGNING_PFX_BASE64`
- `WINDOWS_SIGNING_PFX_PASSWORD`

## Recommended release posture

- GitHub Releases: default lane for early users
- Signed MSIX: validation lane and Store rehearsal
- Microsoft Store: final lane once Partner Center identity is locked
