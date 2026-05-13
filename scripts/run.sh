#!/usr/bin/env bash
set -euo pipefail

echo '启动 Heimdall 的 C# 后端（.NET 10）与 Next.js 前端开发环境'
echo '说明：环境变量统一使用 HEIMDALL_* 键名'
echo '后端命令：dotnet run --project backend/Heimdall.Api/Heimdall.Api.csproj'
echo '前端命令：npm run dev'
