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
$project = Join-Path $PSScriptRoot 'StonewardsLeaveTomeDrop.csproj'
& dotnet build $project -c Release "-p:GameDir=$GameDir" "-p:ProfileDir=$ProfileDir" --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with code $LASTEXITCODE" }
$dll = Join-Path $PSScriptRoot 'bin\Release\StonewardsLeaveTomeDrop.dll'
if (-not (Test-Path $dll)) { throw "Missing build output: $dll" }
$dest = Join-Path $ProfileDir 'BepInEx\plugins\thereisnouser-LeaveTomeDrop'
New-Item -ItemType Directory -Path $dest -Force | Out-Null
Copy-Item -LiteralPath $dll -Destination $dest -Force
Write-Host "Installed: $(Join-Path $dest 'StonewardsLeaveTomeDrop.dll')" -ForegroundColor Green
Write-Host 'Launch Stonewards via r2modman, then test with a remote player.'
