# Builds the converter and packs the release zip: dist\Zeus-World-Maps-<version>.zip
# (converter and generator exes + notices + source\; no game file and no world map picture).
# Compile le convertisseur et prepare le zip de la release.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& cmd /c "`"$root\build.cmd`""
if ($LASTEXITCODE -ne 0) { throw 'build failed' }
$exe = Join-Path $root 'dist\World Map Converter.exe'
$version = (Get-Item -LiteralPath $exe).VersionInfo.ProductVersion
$stage = Join-Path $root 'dist\stage'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory "$stage\source" | Out-Null
Copy-Item -LiteralPath $exe -Destination $stage
Copy-Item -LiteralPath (Join-Path $root 'dist\World Map Generator.exe') -Destination $stage
Copy-Item -LiteralPath "$root\docs\LISEZMOI.txt", "$root\docs\README.txt", "$root\LICENSE" -Destination $stage
Copy-Item -Path "$root\src\*" -Destination "$stage\source"
New-Item -ItemType Directory "$stage\source\generator" | Out-Null
Copy-Item -Path "$root\generator\*" -Destination "$stage\source\generator"
Copy-Item -LiteralPath "$root\LICENSE-zeus_mapper.txt" -Destination "$stage\source"
$zip = Join-Path $root "dist\Zeus-World-Maps-Tools-$version.zip"
Compress-Archive -Path "$stage\*" -DestinationPath $zip -CompressionLevel Optimal -Force
Remove-Item $stage -Recurse -Force
"Release: $zip"
