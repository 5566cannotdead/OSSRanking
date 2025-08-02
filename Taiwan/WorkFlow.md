# 台灣知名GitHub用戶排名系統

這是一個自動化的台灣知名GitHub用戶排名系統，會根據多個指標來計算用戶的影響力分數。

## 功能特色

- 🔍 **智能搜尋**: 搜尋台灣各地區的GitHub用戶
- 📊 **多維度評分**: 基於追蹤者數量、專案Star/Fork數量、組織貢獻等指標
- 🔄 **重試機制**: 自動處理GitHub API限制，包含重試和等待機制
- 📝 **自動生成**: 生成Markdown格式的排名報告
- 💾 **資料儲存**: 將完整用戶資料儲存為JSON格式

## 排名邏輯

系統會根據以下指標計算用戶分數：

1. **個人追蹤數量** × 1.0
2. **個人專案Star數量** × 1.0
3. **個人專案Fork數量** × 1.0
4. **組織貢獻專案Star數量** × 1.0
5. **組織貢獻專案Fork數量** × 1.0

## 搜尋地區

系統會搜尋以下台灣地區的GitHub用戶：

- Taiwan (台灣)
- Taipei (台北)
- New Taipei (新北)
- Taoyuan (桃園)
- Taichung (台中)
- Tainan (台南)
- Kaohsiung (高雄)
- Hsinchu (新竹)
- Keelung (基隆)
- Chiayi (嘉義)
- Changhua (彰化)
- Yunlin (雲林)
- Nantou (南投)
- Pingtung (屏東)
- Yilan (宜蘭)
- Hualien (花蓮)
- Taitung (台東)
- Penghu (澎湖)
- Kinmen (金門)
- Matsu (馬祖)

## 使用前準備

### 1. 安裝.NET 9.0

確保您的系統已安裝.NET 9.0 SDK。

### 2. 設定GitHub API Token

為了避免API限制，建議使用GitHub Personal Access Token：

1. 前往 [GitHub Settings > Developer settings > Personal access tokens](https://github.com/settings/tokens)
2. 生成新的token，選擇適當的權限（至少需要`public_repo`和`read:user`）
3. 將token儲存到 `C:\Token` 檔案中

```bash
echo "your_github_token_here" > C:\Token
```

## 執行方式

```bash
# 編譯專案
dotnet build

# 執行程式
dotnet run
```

## 輸出檔案

程式執行後會生成以下檔案：

- **Users.json**: 包含所有用戶的完整資料，包括所有專案資訊
- **Readme.md**: 格式化的排名報告，適合在GitHub上顯示

## 程式特色

### 智能搜尋策略

1. **基本搜尋**: 搜尋各地區的用戶
2. **額外搜尋**: 使用 `followers:>50` 條件確保找到知名用戶
3. **持續搜尋**: 直到沒有50個以上追蹤者的用戶為止

### API限制處理

- **重試機制**: 最多重試3次
- **等待策略**: 遇到API限制時等待5分鐘後重試
- **速率控制**: 自動控制API調用頻率

### 資料完整性

- **所有專案**: 包含用戶的所有專案（包括低星數專案）
- **組織貢獻**: 識別用戶在組織專案中的貢獻
- **詳細資訊**: 儲存用戶的完整檔案和專案資訊

## 注意事項

1. **API限制**: 未認證的API調用有較嚴格的限制，建議使用Personal Access Token
2. **執行時間**: 由於需要處理大量用戶和專案資料，程式執行時間可能較長
3. **網路需求**: 需要穩定的網路連線來調用GitHub API

## 技術架構

- **語言**: C# (.NET 9.0)
- **HTTP客戶端**: System.Net.Http
- **JSON處理**: Newtonsoft.Json
- **非同步處理**: async/await模式
- **錯誤處理**: 完整的異常處理和重試機制

## 授權

本專案採用[MIT授權條款](/LICENSE)。
