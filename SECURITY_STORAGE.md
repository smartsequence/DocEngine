# 數據存儲安全性說明

## 問題背景

某些客戶有資安要求，禁止使用 `localStorage` 存儲問卷數據。原因包括：

### localStorage 的資安風險

1. **XSS 攻擊風險**
   - 惡意腳本可以讀取所有 localStorage 數據
   - JavaScript 可以直接訪問：`localStorage.getItem('survey_data')`

2. **數據持久化**
   - 數據永久存儲在用戶瀏覽器中
   - 無法從服務端控制或刪除
   - 可能包含敏感信息（如系統名稱、挑戰描述等）

3. **同源策略限制**
   - 同一域名下的所有腳本都可訪問
   - 如果網站有第三方腳本注入風險，數據可能洩露

## 解決方案

本系統已實作兩種存儲模式，可根據資安要求選擇：

### 模式 1：服務端 Session（推薦 - 更安全）

**優點：**
- ✅ 數據存儲在服務端，前端無法直接訪問
- ✅ Session Cookie 設置了 `HttpOnly`，JavaScript 無法讀取
- ✅ 支持 `SameSite=Strict`，防止 CSRF 攻擊
- ✅ 可以設置過期時間（預設 30 分鐘）
- ✅ 符合大多數企業資安要求

**缺點：**
- 需要服務端資源
- 如果使用 In-Memory Session，伺服器重啟會丟失（可改用 Redis）

**實作方式：**
- 問卷提交：`POST /Home/SubmitSurvey` → 數據存儲在 Session
- 報告讀取：`GET /Home/GetSurveyData` → 從 Session 讀取

### 模式 2：客戶端 localStorage（開發/測試用）

**優點：**
- ✅ 簡單快速
- ✅ 不佔用服務端資源
- ✅ 適合開發和測試環境

**缺點：**
- ❌ 有 XSS 風險
- ❌ 數據持久化，無法控制
- ❌ 不符合企業資安要求

## 配置方式

在 `appsettings.json` 中添加配置：

```json
{
  "Storage": {
    "UseServerSession": true,  // true = 使用 Session，false = 使用 localStorage
    "SessionTimeout": 30       // Session 過期時間（分鐘）
  }
}
```

## API 端點

### 1. 提交問卷數據
```
POST /Home/SubmitSurvey
Content-Type: application/json

{
  "M1": "3",
  "M2": "5",
  ...
}
```

### 2. 獲取問卷數據
```
GET /Home/GetSurveyData

Response:
{
  "success": true,
  "data": { ... },
  "timestamp": "2026-01-11T03:45:00Z"
}
```

### 3. 清除問卷數據
```
POST /Home/ClearSurveyData
```

## 前端修改

前端代碼會自動偵測配置，如果使用 Session 模式：
- 提交問卷時自動調用 `/Home/SubmitSurvey`
- 讀取數據時自動調用 `/Home/GetSurveyData`
- 不再使用 `localStorage.setItem/getItem`

## 安全建議

1. **生產環境**：強烈建議使用 Session 模式
2. **開發環境**：可以使用 localStorage 模式以便調試
3. **高安全性要求**：考慮使用 Redis 作為 Session 存儲後端

## 下一步

如需切換到 Session 模式，請：
1. 確保 `appsettings.json` 中 `Storage:UseServerSession = true`
2. 前端代碼已自動支援兩種模式
3. 重啟應用程式生效
