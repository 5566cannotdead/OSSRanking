# 配置說明

## API 請求限制設置

可以通過修改 `RunProgress` 模型中的 `MaxApiRequestsPerRun` 屬性來調整每次運行的 API 請求限制：

### 在 Models/RunProgress.cs 中：
```csharp
public int MaxApiRequestsPerRun { get; set; } = 50; // 默認值為 50
```

### 常見設置建議：

1. **保守設置 (推薦)**: 50 個請求
   - 適合大多數情況
   - 避免觸發 GitHub API 限制
   - 每次運行約需要 1-2 分鐘

2. **標準設置**: 100 個請求
   - 適合有付費 GitHub 帳戶的用戶
   - 更快完成搜尋
   - 每次運行約需要 2-3 分鐘

3. **激進設置**: 200+ 個請求
   - 僅適合 GitHub Enterprise 或高配額帳戶
   - 可能觸發 API 限制
   - 不推薦普通用戶使用

## GitHub API 配額說明

### 未認證請求
- 每小時限制: 60 次
- 適用於: 沒有 Token 的請求

### 認證請求 (Personal Access Token)
- 每小時限制: 5,000 次
- 適用於: 使用 Token 的請求
- 推薦使用

### GitHub Enterprise
- 每小時限制: 通常更高
- 具體限制取決於企業設置

## 運行建議

1. **首次運行**: 使用默認的 50 個請求限制
2. **後續運行**: 根據實際需要調整
3. **監控使用**: 注意 GitHub API 配額使用情況
4. **錯誤處理**: 如果遇到限制，等待後重新運行

## 修改配置

如果想要修改默認的 API 請求限制，可以：

1. 修改 `Models/RunProgress.cs` 中的默認值
2. 或在程序啟動時動態設置：

```csharp
// 在 Program.cs 中的示例
var progress = await progressService.LoadProgressAsync();
progress.MaxApiRequestsPerRun = 30; // 設置為 30 個請求
await progressService.SaveProgressAsync(progress);
```

## 監控和調試

程序會輸出詳細的 API 請求計數信息：
```
📊 本次運行 API 請求限制: 50
📈 API 請求計數: 1/50
📈 API 請求計數: 2/50
...
⚠️  已達到本次運行的 API 請求限制 (50)
```

這可以幫助您：
- 了解實際的 API 使用情況
- 決定是否需要調整限制
- 估算完成時間
