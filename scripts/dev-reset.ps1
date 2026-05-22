<#
.SYNOPSIS
  Heimdall 开发数据重置脚本

.DESCRIPTION
  清空 Wiki 缓存、任务记录、代码索引（保留仓库和用户数据），
  将数据库恢复到可重新调试的状态。

.PARAMETER Force
  跳过确认提示直接执行

.PARAMETER KeepTasks
  保留任务记录，仅清空 Wiki 缓存和代码索引

.EXAMPLE
  .\scripts\dev-reset.ps1
  .\scripts\dev-reset.ps1 -Force
  .\scripts\dev-reset.ps1 -KeepTasks
#>

[CmdletBinding()]
param(
    [switch]$Force,
    [switch]$KeepTasks
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDir '..')).Path

# 加载 .env
$envFile = Join-Path $repoRoot '.env'
if (Test-Path $envFile) {
    foreach ($line in (Get-Content -LiteralPath $envFile -Encoding UTF8)) {
        $t = $line.Trim()
        if ($t.Length -eq 0 -or $t.StartsWith('#')) { continue }
        $idx = $t.IndexOf('=')
        if ($idx -lt 1) { continue }
        $k = $t.Substring(0, $idx).Trim()
        $v = $t.Substring($idx + 1).Trim('"', '''', ' ')
        if ($v.Length -gt 0) { [Environment]::SetEnvironmentVariable($k, $v, 'Process') }
    }
}

Write-Host ''
Write-Host '══ Heimdall 数据重置 ══' -ForegroundColor Magenta
Write-Host ''

if (-not $Force) {
    $keepMsg = if ($KeepTasks) { "（保留任务记录）" } else { "" }
    Write-Host "⚠ 此操作将清空以下数据${keepMsg}:" -ForegroundColor Yellow
    Write-Host "  • Wiki 缓存页面和版本数据" -ForegroundColor Yellow
    if (-not $KeepTasks) { Write-Host "  • 任务记录" -ForegroundColor Yellow }
    Write-Host "  • 代码索引数据" -ForegroundColor Yellow
    Write-Host "  • 已生成的 Slides / Workshop 数据" -ForegroundColor Yellow
    Write-Host ''
    Write-Host "保留数据: 仓库、用户、元数据配置" -ForegroundColor Green
    Write-Host ''

    $confirm = Read-Host '输入 "yes" 确认重置'
    if ($confirm -ne 'yes') {
        Write-Host '已取消。' -ForegroundColor DarkGray
        exit 0
    }
}

# 使用 SQL 清空数据表（保留仓库和用户）
$connString = $env:HEIMDALL_CONNECTION_STRING
if (-not $connString) {
    Write-Error 'HEIMDALL_CONNECTION_STRING 未设置，请先运行 setup-env.ps1'
    exit 1
}

# 解析连接字符串
function Parse-ConnString([string]$cs) {
    $hash = @{}
    foreach ($part in ($cs -split ';')) {
        $t = $part.Trim()
        if (-not $t) { continue }
        $idx = $t.IndexOf('=')
        if ($idx -lt 1) { continue }
        $hash[$t.Substring(0, $idx)] = $t.Substring($idx + 1)
    }
    return $hash
}

$pg = Parse-ConnString $connString
$pgHost = $pg['Host'] ?? 'localhost'
$pgPort = $pg['Port'] ?? '5432'
$pgDb = $pg['Database'] ?? 'heimdall'
$pgUser = $pg['Username'] ?? 'heimdall'

$env:PGPASSWORD = $pg['Password'] ?? 'heimdall'

Write-Host '▸ 清空数据表...' -ForegroundColor Green

$sql = @"
-- 清空按正确的外键顺序
DELETE FROM "WikiPage";
DELETE FROM "WikiVersion";
DELETE FROM "CodeIndexEntry";
DELETE FROM "CodeIndexBatch";
DELETE FROM "EmbeddingVector";
DELETE FROM "SearchCache";
DELETE FROM "PromptTemplate";
"@

if (-not $KeepTasks) {
    $sql += @"

DELETE FROM "TaskArtifact";
DELETE FROM "TaskRecord";
"@
}

# 使用 psql 执行
$sql | & psql -h $pgHost -p $pgPort -U $pgUser -d $pgDb -f - 2>$null

if ($LASTEXITCODE -eq 0) {
    Write-Host '  ✓ 数据表已清空' -ForegroundColor Green
} else {
    Write-Host '  ⚠ psql 未找到或执行失败，请手动清理数据库' -ForegroundColor Yellow
    Write-Host '    连接: psql -h' $pgHost '-p' $pgPort '-U' $pgUser '-d' $pgDb -ForegroundColor DarkGray
}

# 清理文件系统缓存
$dataDir = $env:HEIMDALL_DATA_DIR
$storageDir = $env:HEIMDALL_STORAGE_DIR

if ($dataDir -and (Test-Path $dataDir)) {
    Get-ChildItem -Path $dataDir -Directory | ForEach-Object {
        Write-Host "  清理: $($_.FullName)" -ForegroundColor DarkGray
        Remove-Item -Recurse -Force $_.FullName -ErrorAction SilentlyContinue
    }
    Write-Host '  ✓ 数据目录已清理' -ForegroundColor Green
}

if ($storageDir -and (Test-Path $storageDir)) {
    Get-ChildItem -Path $storageDir -Directory | ForEach-Object {
        Write-Host "  清理: $($_.FullName)" -ForegroundColor DarkGray
        Remove-Item -Recurse -Force $_.FullName -ErrorAction SilentlyContinue
    }
    Write-Host '  ✓ 暂存目录已清理' -ForegroundColor Green
}

Write-Host ''
Write-Host '══ 数据重置完成 ══' -ForegroundColor Green
Write-Host ''
