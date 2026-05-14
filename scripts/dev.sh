#!/usr/bin/env bash
# Heimdall 一键开发环境启动脚本 (bash)
# 用法: ./scripts/dev.sh [--backend-only] [--frontend-only] [--dry-run]

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ENV_FILE="${SCRIPT_DIR}/dev.env"

BACKEND_ONLY=false
FRONTEND_ONLY=false
DRY_RUN=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --backend-only) BACKEND_ONLY=true ;;
        --frontend-only) FRONTEND_ONLY=true ;;
        --dry-run) DRY_RUN=true ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
    shift
done

# 加载环境变量文件
load_env_file() {
    if [[ ! -f "$ENV_FILE" ]]; then
        echo "⚠ 环境变量文件不存在: $ENV_FILE，使用系统默认值"
        return
    fi
    while IFS= read -r line || [[ -n "$line" ]]; do
        line="${line#"${line%%[![:space:]]*}"}"  # trim leading
        line="${line%"${line##*[![:space:]]}"}"  # trim trailing
        [[ -z "$line" || "$line" == \#* ]] && continue
        if [[ "$line" =~ ^([A-Za-z_][A-Za-z0-9_]*)=(.*)$ ]]; then
            export "${BASH_REMATCH[1]}=${BASH_REMATCH[2]}"
        fi
    done < "$ENV_FILE"
}

load_env_file

# 默认值
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://localhost:8001}"
export SERVER_BASE_URL="${SERVER_BASE_URL:-http://localhost:8001}"

BACKEND_URL="${ASPNETCORE_URLS%;*}"  # 取第一个 URL
FRONTEND_PORT=3000

# 查找包管理器
find_pm() {
    if command -v yarn &>/dev/null; then
        echo "yarn"
    elif command -v npm &>/dev/null; then
        echo "npm"
    else
        echo ""
    fi
}

PM=$(find_pm)
if [[ -z "$PM" ]] && [[ "$FRONTEND_ONLY" != "true" ]]; then
    echo "❌ 未找到 npm 或 yarn，请安装 Node.js"
    exit 1
fi

if [[ "$DRY_RUN" == "true" ]]; then
    echo ""
    echo "══ Heimdall 开发环境 (DryRun) ══"
    echo "仓库目录 : $REPO_ROOT"
    echo "后端目录 : $REPO_ROOT/backend/Heimdall.Api"
    echo "前端目录 : $REPO_ROOT/frontend"
    echo "环境文件 : $ENV_FILE"
    echo "后端地址 : $BACKEND_URL"
    echo "前端地址 : http://localhost:$FRONTEND_PORT"
    echo ""
    echo "环境变量:"
    env | grep -E '^(HEIMDALL|SERVER_BASE|ASPNETCORE)' | sort | while IFS='=' read -r k v; do
        echo "  $k=$v"
    done
    echo ""
    echo "启动命令:"
    echo "  后端: dotnet run --project backend/Heimdall.Api/Heimdall.Api.csproj"
    echo "  前端: cd frontend && $PM run dev"
    exit 0
fi

echo ""
echo "══ Heimdall 开发环境 ══"
echo "后端: $BACKEND_URL"
echo "前端: http://localhost:$FRONTEND_PORT"
echo ""

cleanup() {
    echo ""
    echo "正在停止所有服务..."
    if [[ -n "${BACKEND_PID:-}" ]]; then
        kill "$BACKEND_PID" 2>/dev/null || true
    fi
    if [[ -n "${FRONTEND_PID:-}" ]]; then
        kill "$FRONTEND_PID" 2>/dev/null || true
    fi
    echo "已停止。"
}
trap cleanup EXIT INT TERM

# 启动后端
if [[ "$FRONTEND_ONLY" != "true" ]]; then
    echo "▸ 启动后端服务..."
    dotnet run --project "$REPO_ROOT/backend/Heimdall.Api/Heimdall.Api.csproj" &
    BACKEND_PID=$!

    # 等待后端就绪
    echo -n "▸ 等待后端就绪"
    for i in $(seq 1 30); do
        if curl -sf "$BACKEND_URL/api/processed_projects" > /dev/null 2>&1; then
            echo " ✓ (${i}s)"
            break
        fi
        echo -n "."
        sleep 1
    done
    echo ""
fi

# 启动前端
if [[ "$BACKEND_ONLY" != "true" ]]; then
    echo "▸ 启动前端服务..."
    cd "$REPO_ROOT/frontend"
    $PM run dev &
    FRONTEND_PID=$!
fi

echo ""
echo "══ 开发环境已启动 ══"
echo "后端 : $BACKEND_URL"
echo "前端 : http://localhost:$FRONTEND_PORT"
echo ""
echo "按 Ctrl+C 停止所有服务"

# 等待子进程
wait
