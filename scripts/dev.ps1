[CmdletBinding(PositionalBinding = $false)]
param(
  [string]$EnvFile = '',
  [switch]$BackendOnly,
  [switch]$FrontendOnly,
  [switch]$DryRun,
  [string[]]$Env = @()
)

function Get-EnvMapFromFile([string]$path) {
  $map = @{}
  if (-not (Test-Path -LiteralPath $path)) {
    return $map
  }

  foreach ($line in (Get-Content -LiteralPath $path)) {
    $t = $line.Trim()
    if ($t.Length -eq 0) { continue }
    if ($t.StartsWith('#')) { continue }

    $idx = $t.IndexOf('=')
    if ($idx -lt 1) { continue }

    $k = $t.Substring(0, $idx).Trim()
    if ([string]::IsNullOrWhiteSpace($k)) { continue }

    $v = $t.Substring($idx + 1)
    if ($v -eq '') { continue }

    if (($v.StartsWith('"') -and $v.EndsWith('"')) -or ($v.StartsWith("'") -and $v.EndsWith("'"))) {
      if ($v.Length -ge 2) {
        $v = $v.Substring(1, $v.Length - 2)
      }
    }

    $map[$k] = $v
  }

  return $map
}

function Get-EnvMapFromPairs([string[]]$pairs) {
  $map = @{}
  foreach ($p in $pairs) {
    if ($null -eq $p) { continue }
    $t = $p.Trim()
    if ($t.Length -eq 0) { continue }

    foreach ($part in ($t -split '[,;]')) {
      $s = $part.Trim()
      if ($s.Length -eq 0) { continue }

      $idx = $s.IndexOf('=')
      if ($idx -lt 1) { continue }

      $k = $s.Substring(0, $idx).Trim()
      $v = $s.Substring($idx + 1)
      if ([string]::IsNullOrWhiteSpace($k)) { continue }

      $map[$k] = $v
    }
  }

  return $map
}

function Set-EnvVars([hashtable]$map) {
  foreach ($key in $map.Keys) {
    Set-Item -Path ("Env:{0}" -f $key) -Value $map[$key]
  }
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
  $scriptDir = (Get-Location).Path
}

if ([string]::IsNullOrWhiteSpace($EnvFile)) {
  $EnvFile = Join-Path $scriptDir 'dev.env'
}

$repoRoot = (Resolve-Path (Join-Path $scriptDir '..')).Path

$fileMap = Get-EnvMapFromFile $EnvFile
$pairMap = Get-EnvMapFromPairs $Env

Set-EnvVars $fileMap
Set-EnvVars $pairMap

if ([string]::IsNullOrWhiteSpace($env:ASPNETCORE_ENVIRONMENT)) {
  $env:ASPNETCORE_ENVIRONMENT = 'Development'
}

if ([string]::IsNullOrWhiteSpace($env:ASPNETCORE_URLS)) {
  $env:ASPNETCORE_URLS = 'http://localhost:8001'
}

if ([string]::IsNullOrWhiteSpace($env:SERVER_BASE_URL)) {
  $env:SERVER_BASE_URL = 'http://localhost:8001'
}

$startBackend = -not $FrontendOnly
$startFrontend = -not $BackendOnly
$productName = 'Heimdall'
$compatibilityNote = '环境变量统一使用 HEIMDALL_* 键名'

$backendCommand = 'dotnet run --project backend/Heimdall.Api/Heimdall.Api.csproj'
$frontendCommand = 'cd frontend; npm run dev'

if ($DryRun) {
  Write-Host ('{0} 开发环境 DryRun：{1}' -f $productName, $compatibilityNote)
  Write-Host ('仓库目录：{0}' -f $repoRoot)
  Write-Host ('环境变量文件：{0}' -f $EnvFile)
  if ($fileMap.Count -gt 0 -or $pairMap.Count -gt 0) {
    Write-Host '将注入的环境变量：'
    foreach ($k in (($fileMap.Keys + $pairMap.Keys) | Sort-Object -Unique)) {
      $value = Get-Item -Path ("Env:{0}" -f $k) -ErrorAction SilentlyContinue | ForEach-Object { $_.Value }
      if ($null -ne $value) {
        Write-Host ('  {0}={1}' -f $k, $value)
      }
    }
  }
  if ($startBackend) { Write-Host ('后端命令：{0}' -f $backendCommand) }
  if ($startFrontend) { Write-Host ('前端命令：{0}' -f $frontendCommand) }
  exit 0
}

$shell = (Get-Command pwsh -ErrorAction SilentlyContinue | Select-Object -First 1).Name
if ([string]::IsNullOrWhiteSpace($shell)) {
  $shell = (Get-Command powershell -ErrorAction SilentlyContinue | Select-Object -First 1).Name
}

if ([string]::IsNullOrWhiteSpace($shell)) {
  Write-Error '未找到可用的 PowerShell 可执行文件（pwsh 或 powershell）'
  exit 1
}

Write-Host ('正在启动 {0} 开发环境：{1}' -f $productName, $compatibilityNote)

if ($startBackend) {
  Start-Process -FilePath $shell -WorkingDirectory $repoRoot -ArgumentList @('-NoLogo', '-NoExit', '-Command', $backendCommand) | Out-Null
}

if ($startFrontend) {
  Start-Process -FilePath $shell -WorkingDirectory $repoRoot -ArgumentList @('-NoLogo', '-NoExit', '-Command', $frontendCommand) | Out-Null
}
