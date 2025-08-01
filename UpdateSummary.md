# 程式修改總結

## 問題
原程式只計算組織專案的貢獻，但有些開發者可能主要貢獻給其他個人的專案，這樣的評分機制不夠公平。

## 解決方案
新增了對其他個人專案貢獻的計算，同時避免重複計算自己的專案。

## 主要修改內容

### 1. 資料結構更新
- 在 `GitHubUser` 類別中新增 `TopContributedRepositories` 屬性
- 用於存儲用戶貢獻的其他個人專案（前5名）

### 2. 新增方法
- `GetUserContributedRepositories(string username)`: 搜尋用戶貢獻的其他個人專案
  - 使用 GitHub Search API 搜尋 `committer:{username} -user:{username}`
  - 只取個人專案（非組織專案）且非自己的專案
  - 驗證用戶是否真的是貢獻者
  - 限制最多檢查前20個專案以避免API限制

### 3. 分數計算更新
在 `CalculateUserScore` 方法中新增：
```csharp
// 其他個人專案貢獻的 star + fork
score += user.TopContributedRepositories.Sum(r => r.StargazersCount * 1.0 + r.ForksCount * 1.0);
```

### 4. 報表更新
- **Markdown**: 更新表格標題，分別顯示 "Top Org Projects" 和 "Top Contributed Projects"
- **HTML**: 同樣更新表格結構，分別顯示組織專案和其他個人專案貢獻
- 更新說明文字，明確指出現在包含「其他個人專案貢獻」

## 新的評分規則
```
總分 = 個人追蹤數量 
     + 個人專案Star數量 + 個人專案Fork數量 
     + 組織貢獻專案的Star + 組織貢獻專案的Fork 
     + 其他個人專案貢獻的Star + 其他個人專案貢獻的Fork
```

## 避免重複計算
- 個人專案：只計算自己擁有的專案
- 組織專案：只計算組織擁有的專案
- 其他個人專案：只計算其他個人擁有的專案（排除自己和組織）

## API 使用注意事項
- 新功能會增加 GitHub API 的使用量
- 每個用戶會額外進行搜尋API調用和貢獻者API調用
- 已加入適當的延遲機制避免API限制
