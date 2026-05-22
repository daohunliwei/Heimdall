<#
.SYNOPSIS
  Heimdall 环境变量交互式配置脚本

.DESCRIPTION
  读取 .env.example 模板，交互式引导用户填入各 Provider 的 API Key，
  生成 .env 文件。密钥输入时屏幕不显示明文（SecureString）。

.EXAMPLE
  .\scripts\setup-env.ps1
#>

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDir '..')).Path
$envExample = Join-Path $repoRoot '.env.example'
$envTarget = Join-Path $repoRoot '.env'

if (-not (Test-Path $envExample)) {
    Write-Error "模板文件不存在: $envExample"
    exit 1
}

Write-Host ''
Write-Host '══ Heimdall 环境变量配置向导 ══' -ForegroundColor Cyan
Write-Host ''
Write-Host '此脚本将引导你配置 .env 文件中的关键变量。' -ForegroundColor DarkGray
Write-Host '直接按 Enter 跳过不需要的配置项。' -ForegroundColor DarkGray
Write-Host '密钥输入时屏幕回显为 * 号或不可见。' -ForegroundColor DarkGray
Write-Host ''

# 读取模板
$envContent = Get-Content -LiteralPath $envExample -Encoding UTF8 -Raw

# 定义需要交互式配置的密钥列表
$secretKeys = @(
    @{ Name = 'HEIMDALL_DEFAULT_PROVIDER'; Prompt = '默认 AI Provider'; Default = 'ollama'; Secret = $false },
    @{ Name = 'HEIMDALL_CONNECTION_STRING'; Prompt = 'PostgreSQL 连接字符串'; Default = 'Host=localhost;Port=5432;Database=heimdall;Username=heimdall;Password=heimdall'; Secret = $false },
    @{ Name = 'HEIMDALL_AUTH_MODE'; Prompt = '认证模式 (none/jwt)'; Default = 'none'; Secret = $false },
    @{ Name = 'OPENAI_API_KEY'; Prompt = 'OpenAI API Key'; Default = ''; Secret = $true },
    @{ Name = 'GOOGLE_API_KEY'; Prompt = 'Google API Key'; Default = ''; Secret = $true },
    @{ Name = 'MINIMAX_API_KEY'; Prompt = 'MiniMax API Key'; Default = ''; Secret = $true },
    @{ Name = 'DASHSCOPE_API_KEY'; Prompt = 'DashScope API Key'; Default = ''; Secret = $true },
    @{ Name = 'DEEPSEEK_API_KEY'; Prompt = 'DeepSeek API Key'; Default = ''; Secret = $true },
    @{ Name = 'OPENROUTER_API_KEY'; Prompt = 'OpenRouter API Key'; Default = ''; Secret = $true },
    @{ Name = 'AZURE_OPENAI_API_KEY'; Prompt = 'Azure OpenAI API Key'; Default = ''; Secret = $true },
    @{ Name = 'OLLAMA_HOST'; Prompt = 'Ollama 服务地址'; Default = 'http://localhost:11434'; Secret = $false }
)

$updates = @{}
foreach ($item in $secretKeys) {
    Write-Host "── $($item.Prompt) ──" -ForegroundColor Yellow
    if ($item.Default) {
        Write-Host "  默认值: $($item.Default)" -ForegroundColor DarkGray
    }

    $val = if ($item.Secret) {
        Read-Host "  $($item.Name)" -AsSecureString
    } else {
        Read-Host "  $($item.Name)"
    }

    if ($item.Secret -and $val) {
        $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($val)
        try {
            $plain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
            if ($plain) { $updates[$item.Name] = $plain }
        } finally {
            [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        }
    } elseif (-not $item.Secret -and $val) {
        $updates[$item.Name] = $val
    } elseif (-not $item.Secret -and -not $val -and $item.Default) {
        $updates[$item.Name] = $item.Default
    }
    Write-Host ''
}

# 应用更新到 .env 内容
foreach ($key in $updates.Keys) {
    $val = $updates[$key]
    $pattern = "(?<=^${key}=).*$"
    $replacement = $val
    if ($envContent -match $pattern) {
        $envContent = $envContent -replace $pattern, $replacement
    }
}

# 写入 .env
$envContent | Set-Content -LiteralPath $envTarget -Encoding UTF8

Write-Host '══ 配置完成 ══' -ForegroundColor Green
Write-Host ".env 已生成: $envTarget" -ForegroundColor Cyan
Write-Host ''
Write-Host '提示: 运行 .\scripts\dev-start.ps1 启动开发环境' -ForegroundColor DarkGray
Write-Host ''
