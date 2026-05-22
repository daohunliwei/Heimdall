<#
.SYNOPSIS
  Heimdall 开发服务停止脚本

.DESCRIPTION
  优雅停止后端 (.NET) 和前端 (Next.js) 进程。
  默认通过端口杀进程，可选是否停止 PostgreSQL 容器。

.PARAMETER StopPostgres
  同时停止 PostgreSQL Docker 容器

.PARAMETER All
  停止所有相关进程（含 PostgreSQL 和 Node.js）

.EXAMPLE
  .\scripts\dev-stop.ps1
  .\scripts\dev-stop.ps1 -StopPostgres
  .\scripts\dev-stop.ps1 -All
#>

[CmdletBinding()]
param(
    [switch]$StopPostgres,
    [switch]$All
)

$ErrorActionPreference = 'Continue'

Write-Host ''
Write-Host '══ Heimdall 服务停止 ══' -ForegroundColor Cyan
Write-Host ''

$backendPort = 8001
$frontendPort = 3000

# ── 停止后端 ────────────────────────────────────────────────
Write-Host '▸ 停止后端服务...' -ForegroundColor Yellow
$killedBackend = $false

# 按端口杀进程
$backendProc = Get-NetTCPConnection -LocalPort $backendPort -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess
if ($backendProc) {
    foreach ($pid in ($backendProc | Select-Object -Unique)) {
        try {
            $proc = Get-Process -Id $pid -ErrorAction SilentlyContinue
            if ($proc -and $proc.ProcessName -match 'dotnet') {
                Write-Host "  停止 dotnet (PID: $pid)..." -ForegroundColor DarkGray
                $proc.CloseMainWindow()
                Start-Sleep -Seconds 2
                if (-not $proc.HasExited) {
                    $proc.Kill()
                }
                $killedBackend = $true
            }
        } catch { }
    }
}

# 备用：按进程名杀
$dotnetProcs = Get-Process dotnet -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -match 'Heimdall' -or $_.CommandLine -match 'Heimdall.Api' }
foreach ($p in $dotnetProcs) {
    try {
        Write-Host "  停止 dotnet (PID: $($p.Id))..." -ForegroundColor DarkGray
        $p.CloseMainWindow()
        Start-Sleep -Seconds 2
        if (-not $p.HasExited) { $p.Kill() }
        $killedBackend = $true
    } catch { }
}

if ($killedBackend) {
    Write-Host '  ✓ 后端已停止' -ForegroundColor Green
} else {
    Write-Host '  未找到运行中的后端进程' -ForegroundColor DarkGray
}

# ── 停止前端 ────────────────────────────────────────────────
Write-Host '▸ 停止前端服务...' -ForegroundColor Yellow
$killedFrontend = $false

# 按端口杀进程
$frontendProc = Get-NetTCPConnection -LocalPort $frontendPort -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess
if ($frontendProc) {
    foreach ($pid in ($frontendProc | Select-Object -Unique)) {
        try {
            $proc = Get-Process -Id $pid -ErrorAction SilentlyContinue
            if ($proc -and $proc.ProcessName -match 'node') {
                Write-Host "  停止 node (PID: $pid)..." -ForegroundColor DarkGray
                $proc.CloseMainWindow()
                Start-Sleep -Seconds 2
                if (-not $proc.HasExited) {
                    Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
                }
                $killedFrontend = $true
            }
        } catch { }
    }
}

# 备用：杀 next dev 相关进程
$nextProcs = Get-Process node -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -match 'next' }
foreach ($p in $nextProcs) {
    try {
        Write-Host "  停止 next (PID: $($p.Id))..." -ForegroundColor DarkGray
        Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
        $killedFrontend = $true
    } catch { }
}

if ($killedFrontend) {
    Write-Host '  ✓ 前端已停止' -ForegroundColor Green
} else {
    Write-Host '  未找到运行中的前端进程' -ForegroundColor DarkGray
}

# ── PostgreSQL ──────────────────────────────────────────────
if ($StopPostgres -or $All) {
    Write-Host '▸ 停止 PostgreSQL 容器...' -ForegroundColor Yellow
    docker compose stop postgres 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host '  ✓ PostgreSQL 容器已停止' -ForegroundColor Green
    } else {
        Write-Host '  停止失败或容器未运行' -ForegroundColor DarkGray
    }
}

# ── 全局 Node 清理（-All 模式） ─────────────────────────────
if ($All) {
    Write-Host '▸ 清理所有相关 Node.js 进程...' -ForegroundColor Yellow
    Get-Process node -ErrorAction SilentlyContinue | ForEach-Object {
        try {
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
        } catch { }
    }
    Write-Host '  ✓ 已清理' -ForegroundColor Green
}

Write-Host ''
Write-Host '══ 服务已停止 ══' -ForegroundColor Green
Write-Host ''

# 清理临时启动脚本
Get-ChildItem $env:TEMP "heimdall_launch_*.ps1" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
