# 台灣 GitHub 知名開發者抓取工具

這是一個用於搜尋和收集台灣地區 GitHub 知名開發者信息的 C# 工具。

## 功能特色

- 🔍 自動搜尋台灣各縣市的 GitHub 用戶和組織
- 📊 篩選 followers >= 100 的知名開發者
- 🏢 搜尋和分析台灣地區的頂尖 GitHub 組織
- � 生成綜合影響力報告（包含個人和組織統計）
- �💾 支持斷點續傳，從上次未完成的地方繼續
- 🚫 智能處理 GitHub API 限制 (403 rate limit exceeded)
- 🔢 每次運行限制最多 50 個 API 請求，避免超出配額
- 📁 數據保存為 JSON 格式，支持差異更新
- � 提供詳細的診斷模式來排查問題

## 設置說明

### 1. 獲取 GitHub Token

1. 訪問 [GitHub Settings > Personal access tokens](https://github.com/settings/tokens)
2. 點擊 "Generate new token (classic)"
3. 設置適當的權限：
   - `public_repo` - 訪問公共倉庫信息
   - 如果需要訪問私有倉庫，選擇 `repo`
4. 複製生成的 token

### 2. 配置程序

在 `Program.cs` 文件中，將 `GITHUB_TOKEN` 常量替換為您的 token：

```csharp
private const string GITHUB_TOKEN = "您的_GitHub_Token";
```

### 3. 編譯運行

```bash
dotnet build
dotnet run
```

### 4. 診斷模式（問題排查）

如果遇到找不到符合條件用戶的問題，可以使用診斷模式：

```bash
dotnet run -- --diagnose
```

診斷模式會：
- 顯示實際的 GitHub API 搜尋 URL
- 顯示 API 響應的詳細信息
- 分析用戶的 followers 分佈
- 保存原始 API 響應到調試文件

## 文件說明

### 輸出文件

- `Users.json` - 用戶數據文件，包含所有找到的知名開發者信息
- `run_progress.json` - 運行進度文件，用於斷點續傳
- `taiwan_github_influence_report.json` - 台灣 GitHub 影響力綜合報告

### 項目結構

```
├── Models/
│   ├── GitHubUser.cs          # 用戶數據模型
│   ├── GitHubOrganization.cs  # 組織數據模型
│   └── RunProgress.cs         # 運行進度模型
├── Services/
│   ├── GitHubService.cs       # GitHub API 服務
│   ├── OrganizationService.cs # 組織搜尋服務
│   ├── UserDataService.cs     # 用戶數據管理
│   ├── ProgressService.cs     # 進度管理服務
│   └── InfluenceReportService.cs # 影響力報告服務
├── DiagnosticTool.cs          # 診斷工具
└── Program.cs                 # 主程序
```

## 使用說明

### 1. 搜集用戶數據（標準模式）
```bash
dotnet run
```

### 2. 診斷模式（問題排查）
```bash
dotnet run -- --diagnose
```

### 3. 影響力報告模式
```bash
dotnet run -- --influence
```
生成包含個人開發者和組織的綜合影響力報告。
程序會創建新的進度記錄，開始搜尋所有台灣地區：

```
=== 台灣 GitHub 知名開發者抓取工具 ===
📝 首次運行，創建新的進度記錄
🔍 開始搜尋剩餘的 23 個地區
🔍 正在搜尋地區: Taiwan
✅ 地區 'Taiwan' 搜尋完成，找到 45 位符合條件的用戶
...
```

### 斷點續傳
如果程序因 API 限制或其他原因中斷，下次運行時會自動從上次停止的地方繼續：

```
📂 載入運行進度: 已完成 15 個地區
⚠️  上次遇到 API 限制，重置時間: 2025-07-28 15:30:00 UTC
✅ API 限制已重置，可以繼續運行
🔍 開始搜尋剩餘的 8 個地區
```

### API 請求限制處理
為了避免超出 GitHub API 配額，程序每次運行最多發送 50 個 API 請求：

```
📊 本次運行 API 請求限制: 50
📈 API 請求計數: 15/50
📈 API 請求計數: 16/50
...
⚠️  已達到本次運行的 API 請求限制 (50)
🔒 已達到 API 請求限制 (50)，停止本次運行
✅ 本次運行找到 25 位符合條件的用戶
💡 提示: 再次運行程序以繼續搜尋剩餘地區
```

### GitHub API 限制處理
當遇到 GitHub API 限制時，程序會：

1. 自動保存當前進度
2. 顯示 API 重置時間
3. 停止運行並提示等待時間

```
🚫 遇到 GitHub API 限制！重置時間: 2025-07-28 15:30:00 UTC
⏰ 需要等待: 45.2 分鐘
📊 保存已獲取的 123 位用戶數據
```

### 完成狀態
當所有地區搜尋完成後，程序會詢問是否重新開始：

```
🎉 上次運行已完成所有地區搜尋
是否要重新開始完整搜尋？(y/N):
```

## 搜尋範圍

程序會搜尋以下台灣地區關鍵字：

- **主要地區**: Taiwan, 台灣, 臺灣
- **直轄市**: 台北, 新北, 桃園, 台中, 台南, 高雄
- **縣市**: 新竹, 基隆, 嘉義, 彰化, 雲林, 南投, 屏東, 宜蘭, 花蓮, 台東, 澎湖, 金門, 馬祖

## 輸出格式

### Users.json 示例
```json
[
  {
    "login": "username",
    "id": 12345,
    "avatarUrl": "https://avatars.githubusercontent.com/u/12345",
    "htmlUrl": "https://github.com/username",
    "followers": 1500,
    "following": 100,
    "publicRepos": 50,
    "location": "Taipei, Taiwan",
    "company": "Example Company",
    "blog": "https://example.com",
    "name": "User Name",
    "bio": "Software Developer",
    "createdAt": "2015-01-01T00:00:00Z",
    "updatedAt": "2025-07-28T10:00:00Z",
    "lastFetched": "2025-07-28T10:00:00Z"
  }
]
```

### 影響力報告示例輸出
```
================================================================================
🇹🇼 台灣 GitHub 影響力報告
================================================================================

📈 整體統計
總開發者數量: 1,234
總組織數量: 56
總追蹤者數: 45,678
總公開倉庫: 12,345
總 Stars 數: 123,456
總 Forks 數: 23,456

📍 地區分佈 (前 10 名):
  Taipei: 456 位
  Taiwan: 234 位
  Taichung: 123 位
  Kaohsiung: 89 位
  ...

👥 頂尖開發者 (前 20 名 - 按追蹤者排序)
排名    用戶名           追蹤者    倉庫數    地區
====    ======          ======   ======   ====
 1      username1        5420      156     Taipei
 2      username2        3890       89     Taiwan
 3      username3        2156      234     Taichung
 ...

🏢 頂尖組織 (前 10 名 - 按 Stars + 追蹤者排序)
排名    組織名           追蹤者   Stars  Forks   地區
====    ======          ======  =====  =====   ====
 1      org1              1234  45678   5678   Taipei
 2      org2               567  23456   2345   Taiwan
 3      org3               345  12345   1234   Taichung
 ...

📅 報告生成時間: 2025-07-28 15:30:00
================================================================================
```

### 控制台輸出示例
```
=== 台灣 GitHub 知名開發者清單 ===
總計: 456 位開發者
排名前 20 位:
排名    用戶名           追踪者    地區          姓名
====    ======          ======   ====         ====
 1      username1        5420    Taipei       張三
 2      username2        3890    Taiwan       李四
 3      username3        2156    高雄         王五
...
```

## 注意事項

1. **API 請求限制**: 
   - 程序每次運行最多發送 50 個 API 請求
   - GitHub API 對認證請求有每小時 5000 次的限制
   - 未認證請求僅有每小時 60 次限制
2. **請求延遲**: 程序在請求間添加了延遲以避免觸發 GitHub API 限制
3. **數據更新**: 每次運行都會檢查現有用戶的數據是否有變化並更新
4. **錯誤處理**: 程序會記錄失敗的地區，可在後續運行中重試
5. **斷點續傳**: 程序會自動從上次停止的地方繼續，無需重複已完成的工作

## 故障排除

### 常見問題

1. **Token 無效**
   ```
   ❌ 請先在 Program.cs 中設置您的 GitHub Token
   ```
   解決方案：確保 GitHub Token 正確設置且有效

2. **找不到符合條件的用戶**
   ```
   地區 'Penghu' 搜尋完成，找到 0 位符合條件的用戶
   ```
   解決方案：
   - 使用診斷模式：`dotnet run -- --diagnose`
   - 檢查該地區是否真的有 followers >= 100 的用戶
   - 考慮降低 followers 門檻或使用更通用的地區名稱

3. **API 限制**
   ```
   🚫 遇到 API 限制！重置時間: 2025-07-28 15:30:00 UTC
   ```
   解決方案：等待 API 重置時間後重新運行

4. **網絡連接問題**
   ```
   ❌ 搜尋 Taiwan 時發生例外: The remote name could not be resolved
   ```
   解決方案：檢查網絡連接和防火牆設置

## 許可證

本項目僅供學習和研究使用。請遵守 GitHub API 使用條款。
