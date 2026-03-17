# Windows Distribution Strategy

## Recommended now

Use GitHub Releases with the verified WinUI Release output zip.

Why:

- easiest install path for early users
- works well with the tray-first unpackaged shell
- no Store submission overhead while auth/provider flows are still evolving

Update:

- the stable lane right now is the verified `bin/Release` output zipped for GitHub
- a dedicated `dotnet publish` lane should only replace it after the published artifact is confirmed stable

## Also available now

Use the signed MSIX lane when you want:

- a packaged Windows install artifact
- sideload testing on clean machines
- a rehearsal path for eventual Store submission

Build it with:

```powershell
.\publish-msix.ps1 -Version 0.1.0.0
```

## Recommended later

Move to a packaged MSIX variant when:

- the auth UX is stable
- auto-update/signing is ready
- you want Microsoft Store distribution

## Release lanes

### Lane 1: GitHub

- unpackaged
- verified Release output
- zipped release artifact
- manual install

### Lane 2: Store

- packaged MSIX
- signed package identity
- signing
- Store submission
- final Partner Center-associated manifest identity
