# Vendors remaining Git UPM dependencies (e.g. UI Effect) into Packages/.
# Luban is already embedded via Tools\fetch-luban-package.ps1 (no Git required).
# Run from project root: powershell -ExecutionPolicy Bypass -File Tools\setup-upm-packages.ps1
# Requires Git on PATH, or run Tools\install-git-and-packages.bat first.

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$PackagesDir = Join-Path $ProjectRoot "Packages"
$ToolsDir = $PSScriptRoot
$MinGitZip = Join-Path $ToolsDir "MinGit.zip"
$MinGitDir = Join-Path $ToolsDir "MinGit"
$GitExe = Join-Path $MinGitDir "cmd\git.exe"

function Ensure-MinGit {
    if (Test-Path $GitExe) { return }
    $url = "https://github.com/git-for-windows/git/releases/download/v2.47.1.windows.1/MinGit-2.47.1-64-bit.zip"
    Write-Host "Downloading MinGit..."
    Invoke-WebRequest -Uri $url -OutFile $MinGitZip -UseBasicParsing
    if (Test-Path $MinGitDir) { Remove-Item $MinGitDir -Recurse -Force }
    Expand-Archive -Path $MinGitZip -DestinationPath $MinGitDir -Force
    if (-not (Test-Path $GitExe)) { throw "MinGit install failed: $GitExe" }
}

function Clone-Package($name, $url, $dest, $commit) {
    $fullDest = Join-Path $PackagesDir $dest
    if (Test-Path $fullDest) { Remove-Item $fullDest -Recurse -Force }
    Write-Host "Cloning $name..."
    & $GitExe clone --depth 1 $url $fullDest
    if ($commit) {
        Push-Location $fullDest
        & $GitExe fetch --depth 1 origin $commit
        & $GitExe checkout $commit
        Pop-Location
    }
    Remove-Item (Join-Path $fullDest ".git") -Recurse -Force -ErrorAction SilentlyContinue
}

Ensure-MinGit
$env:GIT_SSL_NO_VERIFY = "1"

Clone-Package "luban" "https://gitee.com/focus-creative-games/luban_unity.git" "com.code-philosophy.luban" "d870def77ce13ea942786a6f9e6e55112b46c331"

$uiTemp = Join-Path $env:TEMP "uieffect-clone"
if (Test-Path $uiTemp) { Remove-Item $uiTemp -Recurse -Force }
& $GitExe clone --depth 1 https://github.com/mob-sakai/UIEffect.git $uiTemp
Push-Location $uiTemp
& $GitExe fetch --depth 1 origin 70937d2ce39b61c29c4feb4d13642c65bb553d6c
& $GitExe checkout 70937d2ce39b61c29c4feb4d13642c65bb553d6c
Pop-Location
$uiDest = Join-Path $PackagesDir "com.coffee.ui-effect"
if (Test-Path $uiDest) { Remove-Item $uiDest -Recurse -Force }
Copy-Item (Join-Path $uiTemp "Packages\src") $uiDest -Recurse
Remove-Item $uiTemp -Recurse -Force

Clone-Package "missingrefs" "https://github.com/edcasillas/unity-missing-references-finder.git" "com.ecasillas.missingrefsfinder" "3a039c62614ed7b962b868ee96163b7e4dce0849"

$mcpTemp = Join-Path $env:TEMP "unity-mcp-clone"
if (Test-Path $mcpTemp) { Remove-Item $mcpTemp -Recurse -Force }
& $GitExe clone --depth 1 https://github.com/CoplayDev/unity-mcp.git $mcpTemp
Push-Location $mcpTemp
& $GitExe fetch --depth 1 origin 73eb27aeccfa8e0676eaf3304136e9b85953d913
& $GitExe checkout 73eb27aeccfa8e0676eaf3304136e9b85953d913
Pop-Location
$mcpDest = Join-Path $PackagesDir "com.coplaydev.unity-mcp"
if (Test-Path $mcpDest) { Remove-Item $mcpDest -Recurse -Force }
Copy-Item (Join-Path $mcpTemp "MCPForUnity") $mcpDest -Recurse
Remove-Item $mcpTemp -Recurse -Force

Write-Host "Done. Restart Unity to reimport packages."
