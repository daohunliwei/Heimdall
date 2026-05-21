<#
.SYNOPSIS
  Heimdall 一键开发环境启动脚本

.DESCRIPTION
  加载 dev.env 中的环境变量，启动后端（.NET 10）和前端（Next.js 16），
  等待后端就绪后再启动前端，确保前后端连通。

.PARAMETER EnvFile
  环境变量文件路径，默认为脚本同目录下的 dev.env

.PARAMETER BackendOnly
  仅启动后端

.PARAMETER FrontendOnly
  仅启动前端

.PARAMETER DryRun
  仅打印将要执行的命令和环境变量，不实际启动

.PARAMETER Env
  额外的环境变量键值对，用逗号或分号分隔，例如 -Env "KEY1=val1,KEY2=val2"

.PARAMETER NoHealthCheck
  跳过等待后端就绪的步骤

.EXAMPLE
  .\scripts\dev.ps1
  .\scripts\dev.ps1 -BackendOnly
  .\scripts\dev.ps1 -Env "HEIMDALL_AUTH_MODE=jwt,HEIMDALL_JWT_SECRET=mysecret"
#>

[CmdletBinding(PositionalBinding = $false)]
param(
    [string]$EnvFile = '',
    [switch]$BackendOnly,
    [switch]$FrontendOnly,
    [switch]$DryRun,
    [switch]$NoHealthCheck,
    [string[]]$Env = @()
)

$ErrorActionPreference = 'Stop'

# ── 路径解析 ──────────────────────────────────────────────
$scriptDir = if ($MyInvocation.MyCommand.Path) {
    Split-Path -Parent $MyInvocation.MyCommand.Path
} else {
    (Get-Location).Path
}

if ([string]::IsNullOrWhiteSpace($EnvFile)) {
    $EnvFile = Join-Path $scriptDir 'dev.env'
}

$repoRoot = (Resolve-Path (Join-Path $scriptDir '..')).Path
$backendDir = Join-Path $repoRoot 'backend\Heimdall.Api'
$frontendDir = Join-Path $repoRoot 'frontend'

# ── 环境变量加载 ──────────────────────────────────────────
function Load-EnvFile([string]$path) {
    $map = [ordered]@{}
    if (-not (Test-Path -LiteralPath $path)) {
        Write-Warning "环境变量文件不存在: $path，将使用系统默认值"
        return $map
    }

    foreach ($line in (Get-Content -LiteralPath $path -Encoding UTF8)) {
        $t = $line.Trim()
        if ($t.Length -eq 0 -or $t.StartsWith('#')) { continue }

        $idx = $t.IndexOf('=')
        if ($idx -lt 1) { continue }

        $k = $t.Substring(0, $idx).Trim()
        if ([string]::IsNullOrWhiteSpace($k)) { continue }

        $v = $t.Substring($idx + 1)
        # 去除可选引号
        if ($v.Length -ge 2) {
            if (($v[0] -eq '"' -and $v[-1] -eq '"') -or ($v[0] -eq "'" -and $v[-1] -eq "'")) {
                $v = $v.Substring(1, $v.Length - 2)
            }
        }

        if ($v.Length -gt 0) {
            $map[$k] = $v
        }
    }
    return $map
}

function Set-EnvVars([hashtable]$map) {
    foreach ($key in $map.Keys) {
        [Environment]::SetEnvironmentVariable($key, $map[$key], 'Process')
    }
}

# 1. 加载 dev.env
$envMap = Load-EnvFile $EnvFile

# 2. 合并命令行 -Env 参数
foreach ($pair in $Env) {
    if ([string]::IsNullOrWhiteSpace($pair)) { continue }
    foreach ($part in ($pair -split '[,;]')) {
        $s = $part.Trim()
        if ($s.Length -eq 0) { continue }
        $idx = $s.IndexOf('=')
        if ($idx -lt 1) { continue }
        $k = $s.Substring(0, $idx).Trim()
        $v = $s.Substring($idx + 1)
        $envMap[$k] = $v
    }
}

# 3. 应用环境变量到当前进程
Set-EnvVars $envMap

# ── 默认值补齐 ────────────────────────────────────────────
if ([string]::IsNullOrWhiteSpace($env:ASPNETCORE_ENVIRONMENT)) {
    [Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', 'Development', 'Process')
}

if ([string]::IsNullOrWhiteSpace($env:ASPNETCORE_URLS)) {
    [Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', 'http://localhost:8001', 'Process')
}

if ([string]::IsNullOrWhiteSpace($env:SERVER_BASE_URL)) {
    [Environment]::SetEnvironmentVariable('SERVER_BASE_URL', 'http://localhost:8001', 'Process')
}

$backendUrl = $env:ASPNETCORE_URLS -split ';' | Select-Object -First 1
$frontendPort = 3000

# ── 解析 Node.js 包管理器 ─────────────────────────────────
function Find-PackageManager {
    # 优先使用 yarn（项目 package.json 指定了 yarn）
    $yarn = Get-Command yarn -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($yarn) { return @{ Name = 'yarn'; Cmd = $yarn.Source } }

    $npm = Get-Command npm -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($npm) { return @{ Name = 'npm'; Cmd = $npm.Source } }

    # 尝试常见安装位置
    $candidates = @(
        "$env:APPDATA\npm\npm.cmd",
        "$env:ProgramFiles\nodejs\npm.cmd",
        "${env:ProgramFiles(x86)}\nodejs\npm.cmd",
        "$env:LOCALAPPDATA\fnm_multishells\*\npm.cmd",
        "$env:HOME\.nvm\*\npm.cmd"
    )

    foreach ($c in $candidates) {
        $found = Get-Item $c -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) { return @{ Name = 'npm'; Cmd = $found.FullName } }
    }

    throw '未找到 npm 或 yarn。请安装 Node.js 或将 npm 添加到 PATH。'
}

$pm = $null
if (-not $BackendOnly) {
    try {
        $pm = Find-PackageManager
        Write-Host "包管理器: $($pm.Name) ($($pm.Cmd))" -ForegroundColor DarkGray
    } catch {
        Write-Error $_.Exception.Message
        exit 1
    }
}

# ── 构建环境变量字符串（传递给子进程） ──────────────────────
function Build-EnvBlock {
    $lines = @()
    foreach ($key in $envMap.Keys) {
        $val = $envMap[$key]
        if ($val.Contains('"')) {
            $val = $val.Replace('"', '""')
        }
        $lines += "`$env:$key = '$val'"
    }
    # 补充默认值
    $lines += "`$env:ASPNETCORE_ENVIRONMENT = '$env:ASPNETCORE_ENVIRONMENT'"
    $lines += "`$env:ASPNETCORE_URLS = '$env:ASPNETCORE_URLS'"
    $lines += "`$env:SERVER_BASE_URL = '$env:SERVER_BASE_URL'"
    return $lines -join '; '
}

# ── Dry Run 模式 ───────────────────────────────────────────
if ($DryRun) {
    Write-Host ''
    Write-Host '══ Heimdall 开发环境 (DryRun) ══' -ForegroundColor Cyan
    Write-Host "仓库目录 : $repoRoot"
    Write-Host "后端目录 : $backendDir"
    Write-Host "前端目录 : $frontendDir"
    Write-Host "环境文件 : $EnvFile"
    Write-Host "后端地址 : $backendUrl"
    Write-Host "前端地址 : http://localhost:$frontendPort"
    Write-Host ''
    Write-Host '环境变量:' -ForegroundColor Yellow
    foreach ($key in $envMap.Keys) {
        $val = if ($key -like '*SECRET*' -or $key -like '*KEY*' -or $key -like '*PASSWORD*') {
            '***'
        } else {
            $envMap[$key]
        }
        Write-Host "  $key = $val" -ForegroundColor DarkGray
    }
    Write-Host ''
    Write-Host '启动命令:' -ForegroundColor Yellow
    Write-Host "  后端: dotnet run --no-launch-profile --project `"$backendDir\Heimdall.Api.csproj`"" -ForegroundColor DarkGray
    Write-Host "  前端: cd `"$frontendDir`"; $($pm.Name) run dev" -ForegroundColor DarkGray
    exit 0
}

# ── 实际启动 ───────────────────────────────────────────────
Write-Host ''
Write-Host '══ Heimdall 开发环境 ══' -ForegroundColor Cyan
Write-Host "后端: $backendUrl" -ForegroundColor DarkGray
Write-Host "前端: http://localhost:$frontendPort" -ForegroundColor DarkGray
Write-Host ''

# 辅助：在新窗口中运行命令
function Start-InNewWindow([string]$title, [string]$workingDir, [string]$command) {
    $psFile = Join-Path $env:TEMP "heimdall_launch_$([Guid]::NewGuid().ToString('N').Substring(0,8)).ps1"

    $envBlock = Build-EnvBlock
    $scriptContent = @"
# Heimdall - $title
$envBlock
Set-Location '$workingDir'
Write-Host '══ $title ══' -ForegroundColor Cyan
$command
Write-Host ''
Write-Host '$title 已退出。关闭此窗口即可。' -ForegroundColor Yellow
Read-Host '按 Enter 退出'
"@
    $scriptContent | Set-Content -LiteralPath $psFile -Encoding UTF8

    # 优先使用 pwsh（PS7），回退到 powershell.exe（PS5）
    $shell = if (Get-Command pwsh -ErrorAction SilentlyContinue) { 'pwsh' } else { 'powershell.exe' }
    $proc = Start-Process -FilePath $shell `
        -ArgumentList @('-NoLogo', '-NoProfile', '-NoExit', '-File', $psFile) `
        -WindowStyle Normal `
        -PassThru

    return @{ Process = $proc; ScriptPath = $psFile }
}

# ── 启动后端 ───────────────────────────────────────────────
$backendProc = $null
if (-not $FrontendOnly) {
    Write-Host '▸ 启动后端服务...' -ForegroundColor Green
    $result = Start-InNewWindow 'Heimdall 后端 API' $backendDir `
        "dotnet run --no-launch-profile --project `"$backendDir\Heimdall.Api.csproj`""
    $backendProc = $result.Process
}

# ── 等待后端就绪 ───────────────────────────────────────────
if (-not $FrontendOnly -and -not $NoHealthCheck) {
    $healthUrl = "$backendUrl/api/repositories"
    Write-Host '▸ 等待后端就绪...' -ForegroundColor Yellow
    $retries = 30
    $ready = $false
    for ($i = 1; $i -le $retries; $i++) {
        try {
            $null = Invoke-RestMethod -Uri $healthUrl -TimeoutSec 3 -ErrorAction Stop
            $ready = $true
            Write-Host "  ✓ 后端已就绪 (${i}s)" -ForegroundColor Green
            break
        } catch {
            if ($i -eq 1) {
                Write-Host "  等待中" -NoNewline -ForegroundColor DarkGray
            }
            Write-Host '.' -NoNewline -ForegroundColor DarkGray
            Start-Sleep -Seconds 1
        }
    }
    if (-not $ready) {
        Write-Host "`n  ⚠ 后端未在 ${retries}s 内就绪，但仍将继续启动前端" -ForegroundColor Yellow
    }
    Write-Host ''
}

# ── 启动前端 ───────────────────────────────────────────────
$frontendProc = $null
if (-not $BackendOnly) {
    Write-Host '▸ 启动前端服务...' -ForegroundColor Green
    $result = Start-InNewWindow 'Heimdall 前端' $frontendDir `
        "$($pm.Name) run dev"
    $frontendProc = $result.Process
}

# ── 启动完成 ───────────────────────────────────────────────
Write-Host ''
Write-Host '══ 开发环境已启动 ══' -ForegroundColor Green
Write-Host "后端 : $backendUrl" -ForegroundColor Cyan
Write-Host "前端 : http://localhost:$frontendPort" -ForegroundColor Cyan
Write-Host ''
Write-Host '提示: 关闭后端/前端窗口即可停止服务' -ForegroundColor DarkGray
Write-Host ''

# 脚本退出（子进程继续运行在独立窗口中）
