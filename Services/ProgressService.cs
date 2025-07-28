using System.Text.Json;
using TaiwanGitHubPopularUsers.Models;

namespace TaiwanGitHubPopularUsers.Services
{
    public class ProgressService
    {
        private readonly string _progressFilePath;
        private readonly JsonSerializerOptions _jsonOptions;

        public ProgressService(string progressFilePath = "run_progress.json")
        {
            _progressFilePath = progressFilePath;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task<RunProgress> LoadProgressAsync()
        {
            if (!File.Exists(_progressFilePath))
            {
                Console.WriteLine("📝 首次運行，創建新的進度記錄");
                return new RunProgress
                {
                    LastRunTime = DateTime.UtcNow,
                    CompletedLocations = new List<string>(),
                    FailedLocations = new List<string>(),
                    IsCompleted = false,
                    ApiRequestCount = 0,
                    MaxApiRequestsPerRun = 50,
                    ReachedApiLimit = false
                };
            }

            try
            {
                var json = await File.ReadAllTextAsync(_progressFilePath);
                var progress = JsonSerializer.Deserialize<RunProgress>(json, _jsonOptions);
                
                if (progress != null)
                {
                    // 重置每次運行的 API 請求計數
                    progress.ApiRequestCount = 0;
                    progress.ReachedApiLimit = false;
                    
                    Console.WriteLine($"📂 載入運行進度: 已完成 {progress.CompletedLocations.Count} 個地區");
                    Console.WriteLine($"📊 本次運行 API 請求限制: {progress.MaxApiRequestsPerRun}");
                    
                    if (progress.EncounteredRateLimit && progress.RateLimitResetTime.HasValue)
                    {
                        var resetTime = progress.RateLimitResetTime.Value;
                        if (DateTime.UtcNow < resetTime)
                        {
                            var waitTime = resetTime - DateTime.UtcNow;
                            Console.WriteLine($"⚠️  上次遇到 API 限制，重置時間: {resetTime:yyyy-MM-dd HH:mm:ss} UTC");
                            Console.WriteLine($"⏰ 還需等待: {waitTime.TotalMinutes:F1} 分鐘");
                        }
                        else
                        {
                            Console.WriteLine("✅ API 限制已重置，可以繼續運行");
                            progress.EncounteredRateLimit = false;
                            progress.RateLimitResetTime = null;
                        }
                    }
                    
                    return progress;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 載入進度記錄時發生錯誤: {ex.Message}");
            }

            return new RunProgress
            {
                LastRunTime = DateTime.UtcNow,
                CompletedLocations = new List<string>(),
                FailedLocations = new List<string>(),
                IsCompleted = false,
                ApiRequestCount = 0,
                MaxApiRequestsPerRun = 50,
                ReachedApiLimit = false
            };
        }

        public async Task SaveProgressAsync(RunProgress progress)
        {
            try
            {
                progress.LastRunTime = DateTime.UtcNow;
                var json = JsonSerializer.Serialize(progress, _jsonOptions);
                await File.WriteAllTextAsync(_progressFilePath, json);
                Console.WriteLine($"💾 已保存運行進度到 {_progressFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 保存進度記錄時發生錯誤: {ex.Message}");
            }
        }

        public async Task MarkLocationCompletedAsync(RunProgress progress, string location, int usersFound)
        {
            if (!progress.CompletedLocations.Contains(location))
            {
                progress.CompletedLocations.Add(location);
                progress.TotalUsersFound += usersFound;
                
                // 從失敗列表中移除（如果存在）
                progress.FailedLocations.Remove(location);
                
                Console.WriteLine($"✅ 地區 '{location}' 搜尋完成，找到 {usersFound} 位符合條件的用戶");
                
                await SaveProgressAsync(progress);
            }
        }

        public async Task MarkLocationFailedAsync(RunProgress progress, string location, string error)
        {
            if (!progress.FailedLocations.Contains(location))
            {
                progress.FailedLocations.Add(location);
                progress.LastError = $"{location}: {error}";
                
                Console.WriteLine($"❌ 地區 '{location}' 搜尋失敗: {error}");
                
                await SaveProgressAsync(progress);
            }
        }

        public async Task MarkRateLimitEncounteredAsync(RunProgress progress, DateTime? resetTime = null)
        {
            progress.EncounteredRateLimit = true;
            progress.RateLimitResetTime = resetTime;
            progress.LastError = "遇到 API 限制 (403 rate limit exceeded)";
            
            if (resetTime.HasValue)
            {
                var waitTime = resetTime.Value - DateTime.UtcNow;
                Console.WriteLine($"🚫 遇到 API 限制！重置時間: {resetTime:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine($"⏰ 需要等待: {waitTime.TotalMinutes:F1} 分鐘");
            }
            else
            {
                Console.WriteLine("🚫 遇到 API 限制！請稍後再試");
            }
            
            await SaveProgressAsync(progress);
        }

        public async Task MarkCompletedAsync(RunProgress progress)
        {
            progress.IsCompleted = true;
            progress.EncounteredRateLimit = false;
            progress.RateLimitResetTime = null;
            
            Console.WriteLine($"🎉 所有地區搜尋完成！總共找到 {progress.TotalUsersFound} 位符合條件的用戶");
            
            await SaveProgressAsync(progress);
        }

        public bool IncrementApiRequestCount(RunProgress progress)
        {
            progress.ApiRequestCount++;
            Console.WriteLine($"📈 API 請求計數: {progress.ApiRequestCount}/{progress.MaxApiRequestsPerRun}");
            
            if (progress.ApiRequestCount >= progress.MaxApiRequestsPerRun)
            {
                progress.ReachedApiLimit = true;
                Console.WriteLine($"⚠️  已達到本次運行的 API 請求限制 ({progress.MaxApiRequestsPerRun})");
                return false; // 達到限制
            }
            
            return true; // 可以繼續
        }

        public async Task MarkApiLimitReachedAsync(RunProgress progress)
        {
            progress.ReachedApiLimit = true;
            Console.WriteLine($"🔒 已達到 API 請求限制 ({progress.MaxApiRequestsPerRun})，停止本次運行");
            await SaveProgressAsync(progress);
        }

        public void PrintProgressSummary(RunProgress progress)
        {
            Console.WriteLine("\n=== 運行進度摘要 ===");
            Console.WriteLine($"上次運行時間: {progress.LastRunTime:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"已完成地區: {progress.CompletedLocations.Count}");
            Console.WriteLine($"失敗地區: {progress.FailedLocations.Count}");
            Console.WriteLine($"找到用戶總數: {progress.TotalUsersFound}");
            Console.WriteLine($"本次 API 請求限制: {progress.MaxApiRequestsPerRun}");
            Console.WriteLine($"運行狀態: {(progress.IsCompleted ? "已完成" : "未完成")}");
            
            if (progress.ReachedApiLimit)
            {
                Console.WriteLine($"API 請求狀態: 已達到本次限制 ({progress.ApiRequestCount}/{progress.MaxApiRequestsPerRun})");
            }
            
            if (progress.EncounteredRateLimit)
            {
                Console.WriteLine($"GitHub API 限制: 是");
                if (progress.RateLimitResetTime.HasValue)
                {
                    Console.WriteLine($"限制重置時間: {progress.RateLimitResetTime:yyyy-MM-dd HH:mm:ss} UTC");
                }
            }
            
            if (!string.IsNullOrEmpty(progress.LastError))
            {
                Console.WriteLine($"最後錯誤: {progress.LastError}");
            }
            
            Console.WriteLine("==================");
        }

        public List<string> GetRemainingLocations(RunProgress progress, List<string> allLocations)
        {
            return allLocations.Where(loc => !progress.CompletedLocations.Contains(loc)).ToList();
        }
    }
}
