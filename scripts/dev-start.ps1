<#
.SYNOPSIS
  Heimdall 一键开发环境启动脚本

.DESCRIPTION
  检查 PostgreSQL Docker 容器状态，从 .env.example 生成 .env（若不存在），
  执行数据库迁移，并行启动后端和前端服务。

.PARAMETER SkipDb
  跳过 PostgreSQL Docker 检查

.PARAMETER SkipMigration
  跳过数据库迁移

.PARAMETER BackendOnly
  仅启动后端

.PARAMETER FrontendOnly
  仅启动前端

.EXAMPLE
  .\scripts\dev-start.ps1
  .\scripts\dev-start.ps1 -BackendOnly
  .\scripts\dev-start.ps1 -SkipDb
#>

[CmdletBinding()]
param(
    [switch]$SkipDb,
    [switch]$SkipMigration,
    [switch]$BackendOnly,
    [switch]$FrontendOnly
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDir '..')).Path
$envFile = Join-Path $repoRoot '.env'
$envExample = Join-Path $repoRoot '.env.example'

Write-Host ''
Write-Host '══ Heimdall 开发环境启动 ══' -ForegroundColor Cyan
Write-Host ''

# ── 1. 检查 .env 文件 ──────────────────────────────────────
if (-not (Test-Path $envFile)) {
    Write-Host '▸ .env 文件不存在，从 .env.example 生成...' -ForegroundColor Yellow
    if (Test-Path $envExample) {
        Copy-Item $envExample $envFile
        Write-Host '  ✓ 已生成 .env 文件，请编辑填入 Provider 密钥' -ForegroundColor Green
        Write-Host "    编辑: $envFile" -ForegroundColor DarkGray
    } else {
        Write-Warning '  .env.example 模板文件不存在，跳过生成'
    }
}

# ── 2. 加载环境变量 ────────────────────────────────────────
Write-Host '▸ 加载环境变量...' -ForegroundColor Green
$envVars = @{}
if (Test-Path $envFile) {
    foreach ($line in (Get-Content -LiteralPath $envFile -Encoding UTF8)) {
        $t = $line.Trim()
        if ($t.Length -eq 0 -or $t.StartsWith('#')) { continue }
        $idx = $t.IndexOf('=')
        if ($idx -lt 1) { continue }
        $k = $t.Substring(0, $idx).Trim()
        $v = $t.Substring($idx + 1).Trim('"', '''', ' ')
        if ($v.Length -gt 0) {
            [Environment]::SetEnvironmentVariable($k, $v, 'Process')
            $envVars[$k] = $v
        }
    }
}
# 补齐默认值
if (-not $env:ASPNETCORE_ENVIRONMENT) { [Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', 'Development', 'Process') }
if (-not $env:ASPNETCORE_URLS) { [Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', 'http://localhost:8001', 'Process') }
if (-not $env:SERVER_BASE_URL) { [Environment]::SetEnvironmentVariable('SERVER_BASE_URL', 'http://localhost:8001', 'Process') }

# ── 3. 检查 PostgreSQL Docker ───────────────────────────────
if (-not $SkipDb -and -not $FrontendOnly) {
    Write-Host '▸ 检查 PostgreSQL 容器...' -ForegroundColor Green
    $pgRunning = docker ps --format '{{.Names}}' 2>$null | Select-String 'postgres'
    if (-not $pgRunning) {
        Write-Host '  PostgreSQL 容器未运行，尝试启动...' -ForegroundColor Yellow
        docker compose up -d postgres 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Warning '  Docker Compose 启动失败，请确认 Docker Desktop 已运行'
        } else {
            Write-Host '  ✓ PostgreSQL 容器已启动' -ForegroundColor Green
            Start-Sleep -Seconds 3
        }
    } else {
        Write-Host '  ✓ PostgreSQL 容器运行中' -ForegroundColor Green
    }
}

# ── 4. 数据库迁移 ──────────────────────────────────────────
if (-not $SkipMigration -and -not $FrontendOnly) {
    Write-Host '▸ 执行数据库迁移...' -ForegroundColor Green
    Push-Location $repoRoot
    try {
        dotnet ef database update `
            --project backend/Heimdall.Repository/Heimdall.Repository.csproj `
            --startup-project backend/Heimdall.Api/Heimdall.Api.csproj 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host '  ✓ 数据库迁移完成' -ForegroundColor Green
        } else {
            Write-Warning '  迁移可能未完全成功，请检查数据库连接'
        }
    } finally {
        Pop-Location
    }
}

# ── 5. 启动服务（复用 dev.ps1 的窗口启动逻辑） ─────────────
$backendDir = Join-Path $repoRoot 'backend\Heimdall.Api'
$frontendDir = Join-Path $repoRoot 'frontend'
$backendUrl = $env:ASPNETCORE_URLS -split ';' | Select-Object -First 1

# 查找包管理器
$pm = if (Get-Command yarn -ErrorAction SilentlyContinue) { 'yarn' } else { 'npm' }

Write-Host ''

# 启动后端
if (-not $FrontendOnly) {
    Write-Host '▸ 启动后端服务...' -ForegroundColor Green
    Start-Process pwsh -ArgumentList @(
        '-NoLogo', '-NoProfile', '-Command',
        "`$env:ASPNETCORE_ENVIRONMENT='Development'; `$env:ASPNETCORE_URLS='$backendUrl'; Set-Location '$backendDir'; dotnet run --no-launch-profile --project Heimdall.Api.csproj; Read-Host '按 Enter 退出'"
    )
}

# 等待后端就绪
if (-not $FrontendOnly) {
    Write-Host '▸ 等待后端就绪...' -ForegroundColor Yellow
    $healthUrl = "$backendUrl/api/repositories"
    for ($i = 1; $i -le 30; $i++) {
        try {
            $null = Invoke-RestMethod -Uri $healthUrl -TimeoutSec 2 -ErrorAction Stop
            Write-Host "  ✓ 后端已就绪 (${i}s)" -ForegroundColor Green
            break
        } catch {
            Write-Host '.' -NoNewline -ForegroundColor DarkGray
            Start-Sleep -Seconds 1
        }
    }
    if ($i -gt 30) {
        Write-Host "`n  ⚠ 后端未在 30s 内就绪，将继续启动前端" -ForegroundColor Yellow
    }
}

# 启动前端
if (-not $BackendOnly) {
    Write-Host '▸ 启动前端服务...' -ForegroundColor Green
    Start-Process pwsh -ArgumentList @(
        '-NoLogo', '-NoProfile', '-Command',
        "Set-Location '$frontendDir'; $pm run dev; Read-Host '按 Enter 退出'"
    )
}

Write-Host ''
Write-Host '══ 开发环境已启动 ══' -ForegroundColor Green
Write-Host "后端 : $backendUrl" -ForegroundColor Cyan
Write-Host "前端 : http://localhost:3000" -ForegroundColor Cyan
Write-Host ''
Write-Host '提示: 关闭后端/前端窗口即可停止服务，或运行 dev-stop.ps1' -ForegroundColor DarkGray
Write-Host ''
