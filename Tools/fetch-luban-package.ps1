# Downloads com.code-philosophy.luban from GitHub raw (no Git required).
$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$Dest = Join-Path $ProjectRoot "Packages\com.code-philosophy.luban"
$Base = "https://raw.githubusercontent.com/focus-creative-games/luban_unity/main"

$files = @(
    "package.json",
    "LICENSE",
    "README.md",
    "Runtime\Luban.Runtime.asmdef",
    "Runtime\BeanBase.cs",
    "Runtime\ByteBuf.cs",
    "Runtime\StringUtil.cs",
    "Runtime\SimpleJSON\SimpleJSON.cs",
    "Runtime\SimpleJSON\SimpleJSONBinary.cs",
    "Runtime\SimpleJSON\SimpleJSONDotNetTypes.cs",
    "Runtime\SimpleJSON\SimpleJSONUnity.cs",
    "Runtime\SimpleJSON\LICENSE",
    "Runtime\SimpleJSON\Changelog.txt",
    "Runtime\SimpleJSON\README"
)

if (Test-Path $Dest) { Remove-Item $Dest -Recurse -Force }
New-Item -ItemType Directory -Force -Path $Dest | Out-Null

foreach ($rel in $files) {
    $url = "$Base/$($rel -replace '\\','/')"
    $out = Join-Path $Dest $rel
    $dir = Split-Path $out -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    Write-Host $rel
    Invoke-WebRequest -Uri $url -OutFile $out -UseBasicParsing
}

Write-Host "Luban package ready at $Dest"
