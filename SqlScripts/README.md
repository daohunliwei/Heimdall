# SqlScripts 维护约定

## 目录说明

此目录包含 Heimdall 数据库的 PostgreSQL 初始化脚本，作为 SqlSugar CodeFirst 自动同步的回退方案。

## 脚本列表

| 脚本 | 用途 | 执行顺序 |
|------|------|----------|
| `Init_Extensions.sql` | 启用当前实际依赖的 PostgreSQL 扩展 | 1 |
| `Init_Tables.sql` | 创建所有业务表 | 2 |
| `Init_Indexes.sql` | 创建外键和常用查询列索引 | 3 |
| `Init_SeedData.sql` | 插入默认系统设置和提示词模板 | 4 |

## 维护约定

1. **实体变更时同步更新**：任何新增/修改实体类的操作，必须同步更新 `Init_Tables.sql` 添加对应的 CREATE TABLE 或 ALTER TABLE 语句
2. **索引变更同步**：新增外键或常用查询列时，同步更新 `Init_Indexes.sql`
3. **种子数据变更同步**：新增或修改默认系统设置/提示词模板时，同步更新 `Init_SeedData.sql`
4. **扩展依赖变更同步**：新增 PostgreSQL 扩展依赖时，同步更新 `Init_Extensions.sql`
5. **脚本可独立执行**：所有脚本必须支持在空数据库上通过 psql 独立执行，幂等且不报错
6. **命名规范**：表名和列名使用下划线命名（snake_case），与 SqlSugar CodeFirst 生成的结构一致
7. **与 CodeFirst 一致**：脚本中的表结构必须与 SqlSugar CodeFirst 生成的完全一致

## 执行方式

```bash
# 对空数据库执行（按顺序）
psql -h <host> -U <user> -d <database> -f SqlScripts/Init_Extensions.sql
psql -h <host> -U <user> -d <database> -f SqlScripts/Init_Tables.sql
psql -h <host> -U <user> -d <database> -f SqlScripts/Init_Indexes.sql
psql -h <host> -U <user> -d <database> -f SqlScripts/Init_SeedData.sql
```
