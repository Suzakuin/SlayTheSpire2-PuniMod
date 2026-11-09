param(
    [string]$GameDir = 'E:\steam\steamapps\common\Slay the Spire 2',
    [string]$GodotPath = 'godot'
)
$ErrorActionPreference = 'Stop'
$projectDir = $PSScriptRoot
if (!(Test-Path -LiteralPath (Join-Path $GameDir 'data_sts2_windows_x86_64\sts2.dll'))) { throw 'GameDir 中缺少游戏程序集 sts2.dll。' }
& dotnet build (Join-Path $projectDir 'PuniMod.csproj') -c Release "-p:GameDir=$GameDir" --nologo
if ($LASTEXITCODE -ne 0) { throw 'C# 编译失败。需要 .NET 9 SDK。' }
& $GodotPath --headless --editor --path $projectDir --import
if ($LASTEXITCODE -ne 0) { throw 'Godot 资源导入失败。需要 Godot 4.5.1。' }
& $GodotPath --headless --path $projectDir --script res://Tools/PackAssets.gd
if ($LASTEXITCODE -ne 0) { throw '资源打包失败。' }
$dist = Join-Path $projectDir 'dist\PuniMod'
[IO.Directory]::CreateDirectory($dist) | Out-Null
Copy-Item -LiteralPath (Join-Path $projectDir 'bin\Release\net9.0\PuniMod.dll') -Destination $dist
Copy-Item -LiteralPath (Join-Path $projectDir 'build\PuniMod.pck') -Destination $dist
Copy-Item -LiteralPath (Join-Path $projectDir 'PuniMod.json') -Destination $dist
Write-Host "构建完成：$dist" -ForegroundColor Green
