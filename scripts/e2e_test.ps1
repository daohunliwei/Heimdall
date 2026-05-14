# Heimdall V2 端到端验证脚本
# 使用方法：在 PowerShell 中运行 .\scripts\e2e_test.ps1
# 前提：后端已在 http://localhost:8001 运行

param(
    [string]$BaseUrl = "http://localhost:8001",
    [string]$TestRepoUrl = "http://gitlab.beisencorp.com/AppCenter/Beisen.AppCenter.Ops"
)

$ErrorActionPreference = "Continue"
$Passed = 0
$Failed = 0

function Test-Endpoint {
    param([string]$Name, [string]$Method, [string]$Path, $Body)

    Write-Host -NoNewline "[TEST] $Name ... "
    try {
        $params = @{
            Method = $Method
            Uri = "$BaseUrl$Path"
            ContentType = "application/json"
        }
        if ($Body) {
            $params.Body = ($Body | ConvertTo-Json -Compress)
        }
        $response = Invoke-RestMethod @params -TimeoutSec 30
        Write-Host "PASS" -ForegroundColor Green
        $Global:Passed++
        return $response
    } catch {
        Write-Host "FAIL: $_" -ForegroundColor Red
        $Global:Failed++
        return $null
    }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Heimdall V2 端到端全链路验证" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ===== M1: 统一主标识 =====
Write-Host "--- M1: 统一主标识 ---" -ForegroundColor Yellow

# 1. 导入仓库
$importResult = Test-Endpoint "POST /api/repositories/import" "POST" "/api/repositories/import" @{
    repo_url = $TestRepoUrl
}
$repoId = $importResult.repository_id
Write-Host "  repositoryId = $repoId"

# 2. 获取仓库列表
$reposResult = Test-Endpoint "GET /api/repositories" "GET" "/api/repositories"

# 3. 获取单个仓库
if ($repoId) {
    Test-Endpoint "GET /api/repositories/$repoId" "GET" "/api/repositories/$repoId"
}

# 4. 更新仓库
if ($repoId) {
    Test-Endpoint "PATCH /api/repositories/$repoId" "PATCH" "/api/repositories/$repoId" @{
        display_name = "Beisen.AppCenter.Ops"
        description = "测试更新"
    }
}

# 5. 获取项目列表
Test-Endpoint "GET /api/processed_projects" "GET" "/api/processed_projects"

# ===== M2: 版本化底座 =====
Write-Host "--- M2: 版本化底座 ---" -ForegroundColor Yellow

# 6. 发现仓库版本
if ($repoId) {
    $version = Test-Endpoint "POST .../versions/discover" "POST" "/api/repositories/$repoId/versions/discover" @{
        branch = "main"
    }
    $repoVersionId = $version.repository_version_id
    Write-Host "  repositoryVersionId = $repoVersionId"
}

# 7. 获取版本列表
if ($repoId) {
    Test-Endpoint "GET .../versions" "GET" "/api/repositories/$repoId/versions"
}

# 8. 获取最新版本
if ($repoId) {
    Test-Endpoint "GET .../versions/latest" "GET" "/api/repositories/$repoId/versions/latest?branch=main"
}

# ===== M3: 双向量表 =====
Write-Host "--- M3: 双向量表 ---" -ForegroundColor Yellow

# 9. 按 repositoryId 读取 Wiki
if ($repoId) {
    Test-Endpoint "GET .../wiki" "GET" "/api/repositories/$repoId/wiki?language=zh"
}

# 10. 向量清理接口 (需要有效的版本 ID)
if ($repoId -and $repoVersionId) {
    Test-Endpoint "DELETE .../vectors/code" "DELETE" "/api/repositories/$repoId/vectors/code?branch=main"
}

# ===== M5: 版本对比 =====
Write-Host "--- M5: 版本对比 ---" -ForegroundColor Yellow

# 11. Wiki 版本对比 (使用占位 ID)
if ($repoId) {
    $emptyGuid = "00000000-0000-0000-0000-000000000001"
    Test-Endpoint "POST .../wiki/compare" "POST" "/api/repositories/$repoId/wiki/compare" @{
        version_id_a = $emptyGuid
        version_id_b = $emptyGuid
    }
}

# ===== 管理后台 =====
Write-Host "--- 管理后台 ---" -ForegroundColor Yellow

# 12. 数据回填
Test-Endpoint "POST /api/admin/migration/backfill" "POST" "/api/admin/migration/backfill"

# 13. 迁移状态
Test-Endpoint "GET /api/admin/migration/status" "GET" "/api/admin/migration/status"

# ===== 旧接口兼容 =====
Write-Host "--- 兼容性验证 ---" -ForegroundColor Yellow

# 14. 旧 wiki_cache 接口仍可用
Test-Endpoint "GET /api/wiki_cache (旧接口)" "GET" "/api/wiki_cache?owner=AppCenter&repo=Beisen.AppCenter.Ops&repo_type=gitlab&language=zh"

# ===== 清理 =====
Write-Host "--- 清理 ---" -ForegroundColor Yellow

# 15. 删除仓库
if ($repoId) {
    Test-Endpoint "DELETE /api/repositories/$repoId" "DELETE" "/api/repositories/$repoId"
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "验证完成: $Passed 通过, $Failed 失败" -ForegroundColor $(if ($Failed -eq 0) { "Green" } else { "Red" })
Write-Host "========================================" -ForegroundColor Cyan
