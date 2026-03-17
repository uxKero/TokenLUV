param(
    [string]$Version = "",
    [string]$Platform = "x64",
    [string]$PfxPath = "",
    [string]$PfxPassword = "",
    [switch]$TrustCurrentUserCertificate
)

$ErrorActionPreference = "Stop"

function Get-LatestSignToolPath {
    $sdkRoot = Join-Path $env:USERPROFILE ".nuget\packages\microsoft.windows.sdk.buildtools"
    $tool = Get-ChildItem $sdkRoot -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1

    if (-not $tool) {
        throw "signtool.exe was not found in the Microsoft.Windows.SDK.BuildTools package cache."
    }

    return $tool.FullName
}

function Get-ManifestIdentity {
    param([string]$ManifestPath)

    [xml]$manifest = Get-Content $ManifestPath -Raw
    $ns = New-Object System.Xml.XmlNamespaceManager($manifest.NameTable)
    $ns.AddNamespace("appx", "http://schemas.microsoft.com/appx/manifest/foundation/windows10")
    $identity = $manifest.SelectSingleNode("/appx:Package/appx:Identity", $ns)

    if (-not $identity) {
        throw "Could not read Identity from $ManifestPath"
    }

    return [pscustomobject]@{
        Name      = $identity.Name
        Publisher = $identity.Publisher
        Version   = $identity.Version
    }
}

function Convert-ToMsixVersion {
    param([string]$InputVersion, [string]$FallbackVersion)

    if ([string]::IsNullOrWhiteSpace($InputVersion)) {
        return $FallbackVersion
    }

    $normalized = $InputVersion.Trim()
    if ($normalized.StartsWith("v", [System.StringComparison]::OrdinalIgnoreCase)) {
        $normalized = $normalized.Substring(1)
    }

    $parts = $normalized.Split(".") | Where-Object { $_ -ne "" }
    if ($parts.Count -eq 0) {
        throw "Version '$InputVersion' could not be converted to an MSIX package version."
    }

    if ($parts.Count -gt 4) {
        $parts = $parts[0..3]
    }

    while ($parts.Count -lt 4) {
        $parts += "0"
    }

    if ($parts | Where-Object { $_ -notmatch '^\d+$' }) {
        throw "MSIX version '$InputVersion' must contain only numeric segments."
    }

    return ($parts -join ".")
}

function New-StrictDevCertificate {
    param(
        [string]$Publisher,
        [string]$PfxOutputPath,
        [string]$CerOutputPath,
        [string]$Password
    )

    $cert = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $Publisher `
        -FriendlyName "TokenLUV Dev Signing ($Publisher)" `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -KeyExportPolicy Exportable `
        -KeyUsage DigitalSignature `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -TextExtension @(
            "2.5.29.19={text}CA=false",
            "2.5.29.37={text}1.3.6.1.5.5.7.3.3"
        ) `
        -NotAfter (Get-Date).AddYears(2)

    $securePassword = ConvertTo-SecureString -String $Password -Force -AsPlainText
    Export-PfxCertificate -Cert $cert -FilePath $PfxOutputPath -Password $securePassword | Out-Null
    Export-Certificate -Cert $cert -FilePath $CerOutputPath | Out-Null

    return $cert
}

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $repoRoot "native\TokenLuv.WinUI\TokenLuv.WinUI.csproj"
$manifestPath = Join-Path $repoRoot "native\TokenLuv.WinUI\Package.appxmanifest"
$identity = Get-ManifestIdentity -ManifestPath $manifestPath
$packageVersion = Convert-ToMsixVersion -InputVersion $Version -FallbackVersion $identity.Version
$distRoot = Join-Path $repoRoot "dist\msix\$Platform\$packageVersion"
$signingRoot = Join-Path $repoRoot "artifacts\signing"
$workingRoot = Join-Path $repoRoot "artifacts\msix-build\$Platform\$packageVersion"
$signTool = Get-LatestSignToolPath

if (Test-Path $distRoot) {
    Remove-Item $distRoot -Recurse -Force
}

if (Test-Path $workingRoot) {
    Remove-Item $workingRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $distRoot | Out-Null
New-Item -ItemType Directory -Force -Path $workingRoot | Out-Null
New-Item -ItemType Directory -Force -Path $signingRoot | Out-Null

$certificateWasGenerated = $false
$certificateFileName = "TokenLuv.WinUI-$Platform-$packageVersion"

if ([string]::IsNullOrWhiteSpace($PfxPath)) {
    $PfxPassword = "tokenluv-dev"
    $PfxPath = Join-Path $signingRoot "$certificateFileName.pfx"
    $cerPath = Join-Path $signingRoot "$certificateFileName.cer"

    if (-not (Test-Path $PfxPath) -or -not (Test-Path $cerPath)) {
        Write-Host "Creating strict development signing certificate for $($identity.Publisher)..." -ForegroundColor Cyan
        $cert = New-StrictDevCertificate -Publisher $identity.Publisher -PfxOutputPath $PfxPath -CerOutputPath $cerPath -Password $PfxPassword
        $certificateWasGenerated = $true
    } else {
        $cert = Import-PfxCertificate -FilePath $PfxPath -CertStoreLocation "Cert:\CurrentUser\My" -Password (ConvertTo-SecureString -String $PfxPassword -Force -AsPlainText) -Exportable
    }
} else {
    if (-not (Test-Path $PfxPath)) {
        throw "PFX file not found at $PfxPath"
    }

    if ([string]::IsNullOrWhiteSpace($PfxPassword)) {
        throw "A PFX password is required when -PfxPath is provided."
    }

    $cert = Import-PfxCertificate -FilePath $PfxPath -CertStoreLocation "Cert:\CurrentUser\My" -Password (ConvertTo-SecureString -String $PfxPassword -Force -AsPlainText) -Exportable
    $cerPath = Join-Path $distRoot "TokenLuv.WinUI.cer"
    Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null
}

if ($cert.Subject -ne $identity.Publisher) {
    throw "The signing certificate subject '$($cert.Subject)' does not match the manifest publisher '$($identity.Publisher)'."
}

$thumbprint = $cert.Thumbprint

if ($TrustCurrentUserCertificate) {
    Import-Certificate -FilePath $cerPath -CertStoreLocation "Cert:\CurrentUser\Root" | Out-Null
    Import-Certificate -FilePath $cerPath -CertStoreLocation "Cert:\CurrentUser\TrustedPeople" | Out-Null
}

$runningProcesses = Get-Process -Name "TokenLuv.WinUI" -ErrorAction SilentlyContinue
if ($runningProcesses) {
    Write-Host "Stopping running TokenLUV instances..." -ForegroundColor Yellow
    $runningProcesses | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

Write-Host "Building MSIX package $packageVersion for $Platform..." -ForegroundColor Cyan
$originalManifest = Get-Content $manifestPath -Raw
$manifestUpdated = $false

try {
    if ($identity.Version -ne $packageVersion) {
        [xml]$manifestXml = $originalManifest
        $ns = New-Object System.Xml.XmlNamespaceManager($manifestXml.NameTable)
        $ns.AddNamespace("appx", "http://schemas.microsoft.com/appx/manifest/foundation/windows10")
        $identityNode = $manifestXml.SelectSingleNode("/appx:Package/appx:Identity", $ns)
        $identityNode.SetAttribute("Version", $packageVersion)
        $manifestXml.Save($manifestPath)
        $manifestUpdated = $true
    }

    dotnet msbuild $projectPath `
        /restore `
        /t:Build `
        /p:Configuration=Release `
        /p:Platform=$Platform `
        /p:WindowsPackageType=MSIX `
        /p:AppxPackage=true `
        /p:EnableMsixTooling=true `
        /p:GenerateAppxPackageOnBuild=true `
        /p:UapAppxPackageBuildMode=SideloadOnly `
        /p:AppxBundle=Never `
        /p:AppxPackageDir="$workingRoot\\" `
        /p:PackageCertificateThumbprint=$thumbprint

    if ($LASTEXITCODE -ne 0) {
        throw "MSIX build failed with exit code $LASTEXITCODE."
    }

    $packageFolder = Get-ChildItem $workingRoot -Directory | Select-Object -First 1
    if (-not $packageFolder) {
        throw "MSIX output folder was not created."
    }

    $msixPath = Get-ChildItem $packageFolder.FullName -Filter *.msix | Select-Object -First 1
    if (-not $msixPath) {
        throw "MSIX package was not found under $($packageFolder.FullName)"
    }

    Write-Host "Signing $($msixPath.Name)..." -ForegroundColor Cyan
    & $signTool sign /fd SHA256 /f $PfxPath /p $PfxPassword $msixPath.FullName
    if ($LASTEXITCODE -ne 0) {
        throw "signtool.exe failed with exit code $LASTEXITCODE."
    }

    $signature = Get-AuthenticodeSignature $msixPath.FullName
    if ($signature.Status -eq "NotSigned" -or -not $signature.SignerCertificate) {
        throw "The generated MSIX package is not signed."
    }

    $finalMsixFileName = "{0}_{1}_{2}.msix" -f $identity.Name, $packageVersion, $Platform
    $finalMsixPath = Join-Path $distRoot $finalMsixFileName
    Copy-Item $msixPath.FullName $finalMsixPath -Force
    Copy-Item $cerPath (Join-Path $distRoot "TokenLuv.WinUI.cer") -Force

    $filesToCopy = @(
        "Add-AppDevPackage.ps1",
        "Install.ps1"
    )

    foreach ($fileName in $filesToCopy) {
        $sourcePath = Join-Path $packageFolder.FullName $fileName
        if (Test-Path $sourcePath) {
            Copy-Item $sourcePath (Join-Path $distRoot $fileName) -Force
        }
    }

    $dependenciesSource = Join-Path $packageFolder.FullName "Dependencies"
    if (Test-Path $dependenciesSource) {
        Copy-Item $dependenciesSource (Join-Path $distRoot "Dependencies") -Recurse -Force
    }

    $installNotes = @"
TokenLUV MSIX package

Main package:
$(Split-Path -Leaf $finalMsixPath)

Certificate:
TokenLuv.WinUI.cer

Recommended install flow for local sideload testing:
1. Open PowerShell as Administrator.
2. Run Add-AppDevPackage.ps1 from this folder.
3. If Windows asks to trust the certificate, install it.

Notes:
- This package is signed and ready for sideload or Store rehearsal.
- For production Store submission, replace the manifest identity with the final Partner Center values and sign with the final certificate.
"@

    Set-Content -Path (Join-Path $distRoot "README.txt") -Value $installNotes -Encoding UTF8

    Write-Host "MSIX output:      $finalMsixPath" -ForegroundColor Green
    Write-Host "Certificate:      $(Join-Path $distRoot 'TokenLuv.WinUI.cer')" -ForegroundColor Green
    Write-Host "Install helper:   $(Join-Path $distRoot 'Add-AppDevPackage.ps1')" -ForegroundColor Green

    if ($certificateWasGenerated) {
        Write-Host "A development signing certificate was generated automatically for this build." -ForegroundColor Yellow
    }
}
finally {
    if ($manifestUpdated) {
        [System.IO.File]::WriteAllText($manifestPath, $originalManifest, [System.Text.UTF8Encoding]::new($false))
    }
}
