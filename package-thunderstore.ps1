[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)] [string]$GameDir,
    [Parameter(Mandatory=$true)] [string]$ProfileDir,
    [string]$OutputDir = ''
)
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$game = (Resolve-Path -LiteralPath $GameDir -ErrorAction Stop).Path
$profile = (Resolve-Path -LiteralPath $ProfileDir -ErrorAction Stop).Path
$store = Join-Path $PSScriptRoot 'thunderstore'
$project = Join-Path $PSScriptRoot 'StonewardsDisconnectDrops.csproj'
$plugin = Join-Path $PSScriptRoot 'src\Plugin.cs'
$dll = Join-Path $PSScriptRoot 'bin\Release\StonewardsDisconnectDrops.dll'
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $PSScriptRoot 'dist'
}

foreach ($path in @(
    (Join-Path $store 'manifest.json'), (Join-Path $store 'README.md'),
    (Join-Path $store 'CHANGELOG.md'), (Join-Path $store 'icon.png'),
    $project, $plugin,
    (Join-Path $game 'Stonewards_Data\Managed\Assembly-CSharp.dll'),
    (Join-Path $game 'Stonewards_Data\Managed\Mirror.dll'),
    (Join-Path $game 'Stonewards_Data\Managed\MyBox.dll'),
    (Join-Path $profile 'BepInEx\core\BepInEx.dll'),
    (Join-Path $profile 'BepInEx\core\0Harmony.dll')
)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required file not found: $path"
    }
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET SDK is required. The dotnet command was not found.'
}

$manifest = Get-Content -LiteralPath (Join-Path $store 'manifest.json') -Raw | ConvertFrom-Json
$version = [string]$manifest.version_number
if ($manifest.name -cne 'DisconnectDrops' -or $version -ne '0.1.0') {
    throw 'Unexpected Thunderstore package name or version.'
}
if ([string]$manifest.description -eq '' -or $manifest.description.Length -gt 250) {
    throw 'Thunderstore description is empty or too long.'
}
if (@($manifest.dependencies) -notcontains 'BepInEx-BepInExPack-5.4.2305') {
    throw 'Thunderstore manifest is missing the required BepInEx dependency.'
}
$src = Get-Content -LiteralPath $plugin -Raw
$csproj = Get-Content -LiteralPath $project -Raw
if ($src -notmatch ('PluginVersion\s*=\s*"' + [regex]::Escape($version) + '"') -or
    $csproj -notmatch ('<Version>' + [regex]::Escape($version) + '</Version>')) {
    throw 'Plugin, project, and manifest versions do not match.'
}

$icon = [IO.File]::ReadAllBytes((Join-Path $store 'icon.png'))
$signature = [byte[]](137,80,78,71,13,10,26,10)
if ($icon.Length -lt 24) { throw 'Invalid PNG icon.' }
for ($i = 0; $i -lt $signature.Length; $i++) {
    if ($icon[$i] -ne $signature[$i]) { throw 'Invalid PNG signature.' }
}
$width = [BitConverter]::ToInt32([byte[]]@($icon[19],$icon[18],$icon[17],$icon[16]),0)
$height = [BitConverter]::ToInt32([byte[]]@($icon[23],$icon[22],$icon[21],$icon[20]),0)
if ($width -ne 256 -or $height -ne 256) {
    throw 'Thunderstore icon must be exactly 256x256 PNG.'
}

& dotnet build $project -c Release "-p:GameDir=$game" "-p:ProfileDir=$profile" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed; no release ZIP created.' }
if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) {
    throw "Compiled plugin DLL not found: $dll"
}
$assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString()
if (-not $assemblyVersion.StartsWith($version + '.')) {
    throw "Compiled DLL has unexpected assembly version: $assemblyVersion"
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$zipPath = Join-Path $OutputDir ('DisconnectDrops-' + $version + '.zip')
if (Test-Path -LiteralPath $zipPath) {
    throw "A release ZIP already exists: $zipPath. Move or remove it explicitly before rebuilding."
}

$zipStream = [IO.File]::Open($zipPath, [IO.FileMode]::CreateNew)
$archive = $null
try {
    $archive = New-Object IO.Compression.ZipArchive(
        $zipStream, [IO.Compression.ZipArchiveMode]::Create, $false
    )
    $files = [ordered]@{
        'manifest.json' = (Join-Path $store 'manifest.json')
        'README.md' = (Join-Path $store 'README.md')
        'CHANGELOG.md' = (Join-Path $store 'CHANGELOG.md')
        'icon.png' = (Join-Path $store 'icon.png')
        'plugins/StonewardsDisconnectDrops.dll' = $dll
    }
    foreach ($name in $files.Keys) {
        $entry = $archive.CreateEntry($name, [IO.Compression.CompressionLevel]::Optimal)
        $input = [IO.File]::OpenRead($files[$name])
        $output = $entry.Open()
        try { $input.CopyTo($output) }
        finally { $input.Dispose(); $output.Dispose() }
    }
} catch {
    if ($null -ne $archive) { $archive.Dispose() }
    $zipStream.Dispose()
    Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
    throw
} finally {
    if ($null -ne $archive) { $archive.Dispose() }
    $zipStream.Dispose()
}

$check = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $expected = @('manifest.json','README.md','CHANGELOG.md','icon.png',
        'plugins/StonewardsDisconnectDrops.dll')
    $actual = @($check.Entries | ForEach-Object { $_.FullName })
    if (@(Compare-Object $expected $actual).Count -ne 0) {
        throw 'ZIP contents do not match the expected Thunderstore package.'
    }
    $readmeEntry = $check.GetEntry('README.md')
    $reader = New-Object IO.StreamReader($readmeEntry.Open())
    try {
        if ($reader.ReadToEnd() -cne [IO.File]::ReadAllText((Join-Path $store 'README.md'))) {
            throw 'Packaged README differs from thunderstore/README.md.'
        }
    } finally { $reader.Dispose() }
} catch {
    Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
    throw
} finally { $check.Dispose() }

Write-Host "Thunderstore package ready: $zipPath" -ForegroundColor Green
Write-Host "SHA256: $((Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash)"
