param(
  [Parameter(Mandatory=$true)][string]$GameDir,
  [Parameter(Mandatory=$true)][string]$ProfileDir
)
$ErrorActionPreference = 'Stop'
$managed = Join-Path $GameDir 'Stonewards_Data\Managed'
$core = Join-Path $ProfileDir 'BepInEx\core'
$required = @(
    (Join-Path $managed 'Assembly-CSharp.dll'),
    (Join-Path $managed 'Mirror.dll'),
    (Join-Path $managed 'MyBox.dll'),
    (Join-Path $core 'BepInEx.dll'),
    (Join-Path $core '0Harmony.dll')
)
foreach ($path in $required) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing required DLL: $path" }
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Install .NET SDK (7/8/9) first. dotnet command not found.'
}
$project = Join-Path $PSScriptRoot 'StonewardsDisconnectDrops.csproj'
& dotnet build $project -c Release "-p:GameDir=$GameDir" "-p:ProfileDir=$ProfileDir" --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with code $LASTEXITCODE" }
$dll = Join-Path $PSScriptRoot 'bin\Release\StonewardsDisconnectDrops.dll'
if (-not (Test-Path $dll)) { throw "Missing build output: $dll" }
$dest = Join-Path $ProfileDir 'BepInEx\plugins\thereisnouser-DisconnectDrops'
New-Item -ItemType Directory -Path $dest -Force | Out-Null
# Prevent duplicate old and new plugins from loading together.
$oldDlls = @(
    (Join-Path $ProfileDir 'BepInEx\plugins\thereisnouser-LeaveLootDrop\StonewardsLeaveLootDrop.dll'),
    (Join-Path $ProfileDir 'BepInEx\plugins\thereisnouser-LeaveTomeDrop\StonewardsLeaveTomeDrop.dll'),
    (Join-Path $ProfileDir 'BepInEx\plugins\thereisnouser-DropConsumedTomes\StonewardsDropConsumedTomes.dll')
)
foreach ($oldDll in $oldDlls) {
    if (Test-Path -LiteralPath $oldDll) {
        Remove-Item -LiteralPath $oldDll -Force
        Write-Host "Removed old plugin: $oldDll" -ForegroundColor Yellow
    }
}
# Preserve the previous configuration when first installing under the new plugin ID.
$newConfig = Join-Path $ProfileDir 'BepInEx\config\com.thereisnouser.stonewards.disconnectdrops.cfg'
$oldConfig = Join-Path $ProfileDir 'BepInEx\config\com.thereisnouser.stonewards.leavelootdrop.cfg'
if ((Test-Path -LiteralPath $oldConfig) -and -not (Test-Path -LiteralPath $newConfig)) {
    Copy-Item -LiteralPath $oldConfig -Destination $newConfig
    Write-Host "Copied existing configuration to $newConfig"
}
Copy-Item -LiteralPath $dll -Destination $dest -Force
Write-Host "Installed: $(Join-Path $dest 'StonewardsDisconnectDrops.dll')" -ForegroundColor Green
Write-Host 'Launch Stonewards via r2modman, then test with a remote player.'
