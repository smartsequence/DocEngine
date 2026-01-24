# DocEngine SaaS 平台

DocEngine SaaS 平台是一個文件自動生成系統，整合 AI 能力自動分析專案程式碼和資料庫，生成各類技術文件。

---

## 📖 專案說明

### 這個專案是做什麼的？

**DocEngine SaaS** 是一個網頁應用程式平台，提供：

1. **專案管理**：管理多個專案的文件生成任務
2. **Agent 管理**：管理連接的 Agent，分配分析任務
3. **AI 文件生成**：使用 AI 服務根據分析結果生成文件
4. **文件版本控制**：管理文件的多個版本
5. **用戶管理**：管理用戶權限和存取控制

### 系統架構

```
DocEngine 系統
├── DocEngine-SaaS (本專案)
│   ├── Web UI - 使用者介面
│   ├── SignalR Hub - Agent 通訊
│   ├── REST API - 資料存取
│   ├── AI 服務整合 - OpenAI / 內網 AI
│   └── PostgreSQL - 資料儲存
│
├── DocEngine-Agent
│   └── 客戶端程式碼/資料庫分析工具
│
└── DocEngine-Contracts
    └── 共享通訊協議庫
```

---

## 🎯 主要功能

### ✅ 已實現功能

- ✅ **用戶認證與授權**
  - Cookie-based 認證
  - 角色權限管理

- ✅ **Agent 管理**
  - SignalR 即時通訊
  - Agent 連接狀態監控
  - 任務分配與排程

- ✅ **系統風險評估**
  - 問卷調查系統
  - 風險評分計算
  - 評估報告生成

### 🚧 開發中功能

- 🚧 AI 文件生成整合
- 🚧 文件版本控制
- 🚧 更多專案管理功能

---

## 📁 專案結構

```
DocEngine-SaaS/
├── Controllers/              # MVC 控制器
│   ├── HomeController.cs
│   ├── AgentController.cs
│   └── ...
├── Views/                    # Razor 視圖
│   ├── Home/
│   ├── Agent/
│   └── ...
├── Hubs/                     # SignalR Hubs
│   └── AgentHub.cs
├── Services/                 # 服務層
│   └── AgentService.cs
├── Models/                   # 資料模型
├── wwwroot/                  # 靜態資源
├── docs/                     # 專案文檔
│   ├── SETUP_SUMMARY.md
│   ├── GIT_BRANCH_STRATEGY.md
│   ├── REPO_ORGANIZATION_STRATEGY.md
│   └── ...
├── scripts/                  # 輔助腳本
│   ├── run-all.ps1          # 同時啟動 SaaS + Agent
│   └── stop-all.ps1         # 停止所有服務
├── appsettings.json         # 應用配置
├── Program.cs               # 應用入口
└── DocEngine.csproj         # 專案檔
```

---

## 🚀 快速開始

### 前置需求

- ✅ .NET 9.0 SDK 或更新版本
- ✅ PostgreSQL 資料庫
- ✅ （可選）OpenAI API Key 或內網 AI Server

### 步驟 1：配置資料庫

1. 安裝 PostgreSQL
2. 建立資料庫：`CREATE DATABASE docengine;`
3. 更新 `appsettings.json` 中的連線字串

### 步驟 2：配置應用程式

編輯 `appsettings.json`：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=docengine;Username=postgres;Password=yourpassword"
  },
  "OpenAI": {
    "ApiKey": "your-openai-api-key"
  }
}
```

### 步驟 3：建置與執行

```bash
# 還原套件
dotnet restore

# 建置專案
dotnet build

# 執行
dotnet run
```

應用程式會啟動在 `https://localhost:7225` 或 `http://localhost:5163`

### 步驟 4：同時啟動 SaaS + Agent（開發模式）

```bash
# PowerShell
.\scripts\run-all.ps1

# 或使用 Visual Studio launchSettings
# 選擇 "SaaS+Agent" 啟動設定
```

---

## ⚙️ Git 分支策略

### 分支說明

- **main** - 穩定發布版本（無 Agent 功能）
  - 已發布的穩定版本
  - 可接受客戶意見修改
  
- **with-agent** - Agent 整合開發分支
  - 包含 Agent 相關功能
  - 定期同步 main 的更新

### 切換分支

```bash
# 切換到穩定版本（無 Agent）
git checkout main

# 切換到 Agent 開發版本
git checkout with-agent
```

詳細的分支策略請參考 [GIT_BRANCH_STRATEGY.md](docs/GIT_BRANCH_STRATEGY.md)

---

## 🛠️ 開發指南

### 建置專案

```bash
# 編譯
dotnet build

# 編譯 Release 版本
dotnet build -c Release

# 執行測試
dotnet test

# 發布
dotnet publish -c Release
```

### 資料庫遷移

```bash
# 建立遷移
dotnet ef migrations add MigrationName

# 套用遷移
dotnet ef database update
```

### 開發環境配置

在 `appsettings.Development.json` 中設置開發環境專用配置：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=docengine_dev;..."
  }
}
```

---

## 📚 相關文檔

- [SETUP_SUMMARY.md](docs/SETUP_SUMMARY.md) - 設置總結
- [GIT_BRANCH_STRATEGY.md](docs/GIT_BRANCH_STRATEGY.md) - Git 分支策略
- [REPO_ORGANIZATION_STRATEGY.md](docs/REPO_ORGANIZATION_STRATEGY.md) - 倉庫組織策略
- [AGENT_DEVELOPMENT_SUMMARY.md](docs/AGENT_DEVELOPMENT_SUMMARY.md) - Agent 功能開發總結
- [Deployment_Architecture.md](docs/Deployment_Architecture.md) - 部署架構
- [Agent_Trigger_Design_Analysis.md](docs/Agent_Trigger_Design_Analysis.md) - Agent 觸發機制設計

---

## 🔗 相關專案

- **DocEngine-Agent** - 客戶端分析工具 (https://github.com/smartsequence/DocEngine-Agent)
- **DocEngine-Contracts** - 共享協議庫 (https://github.com/smartsequence/DocEngine-Contracts)

---

## 📄 授權

此專案為私有專案。

---

**最後更新**：2026-01-25  
**當前版本**：v1.0.0  
**GitHub**：https://github.com/smartsequence/DocEngine-SaaS  
**開發狀態**：🚀 活躍開發中
