using TaiwanGitHubPopularUsers.Services;

namespace TaiwanGitHubPopularUsers
{
    static class Program
    {
        private static string GITHUB_TOKEN = File.ReadAllText("C:\\Token"); // 請替換為您的 GitHub Token
        
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== 台灣 GitHub 知名開發者抓取工具 ===");
            Console.WriteLine($"開始時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            
            try
            {
                // 初始化服務
                var progressService = new ProgressService();
                var userDataService = new UserDataService();
                using var gitHubService = new GitHubService(GITHUB_TOKEN, progressService);

                // 載入運行進度
                var progress = await progressService.LoadProgressAsync();
                progressService.PrintProgressSummary(progress);

                // 檢查是否遇到 API 限制
                if (progress.EncounteredRateLimit && progress.RateLimitResetTime.HasValue)
                {
                    if (DateTime.UtcNow < progress.RateLimitResetTime.Value)
                    {
                        var waitTime = progress.RateLimitResetTime.Value - DateTime.UtcNow;
                        Console.WriteLine($"\n⚠️  上次運行遇到 API 限制，還需等待 {waitTime.TotalMinutes:F1} 分鐘");
                        Console.WriteLine("請稍後再運行程序");
                        Console.WriteLine("\n按任意鍵退出...");
                        Console.ReadKey();
                        return;
                    }
                }

                // 如果已完成，詢問是否重新開始
                if (progress.IsCompleted)
                {
                    Console.WriteLine("\n🎉 上次運行已完成所有地區搜尋");
                    Console.Write("是否要重新開始完整搜尋？(y/N): ");
                    var input = Console.ReadLine()?.ToLower();
                    
                    if (input == "y" || input == "yes")
                    {
                        progress = new TaiwanGitHubPopularUsers.Models.RunProgress
                        {
                            LastRunTime = DateTime.UtcNow,
                            CompletedLocations = new List<string>(),
                            FailedLocations = new List<string>(),
                            IsCompleted = false
                        };
                        Console.WriteLine("🔄 重新開始完整搜尋");
                    }
                    else
                    {
                        Console.WriteLine("📊 顯示現有數據摘要");
                        var existingUsers = await userDataService.LoadExistingUsersAsync();
                        userDataService.PrintUserSummary(existingUsers);
                        Console.WriteLine("\n按任意鍵退出...");
                        Console.ReadKey();
                        return;
                    }
                }

                Console.WriteLine("\n🔍 開始搜尋台灣地區的 GitHub 用戶...");
                
                // 搜尋用戶
                var searchResult = await gitHubService.SearchTaiwanUsersAsync(progress);
                
                if (!searchResult.Success)
                {
                    if (searchResult.IsRateLimited)
                    {
                        Console.WriteLine("\n🚫 遇到 API 限制，程序已停止並保存進度");
                        if (searchResult.RateLimitResetTime.HasValue)
                        {
                            var waitTime = searchResult.RateLimitResetTime.Value - DateTime.UtcNow;
                            Console.WriteLine($"⏰ 請在 {waitTime.TotalMinutes:F1} 分鐘後重新運行程序");
                            Console.WriteLine($"重置時間: {searchResult.RateLimitResetTime:yyyy-MM-dd HH:mm:ss} UTC");
                        }
                        
                        // 即使遇到限制，也要保存已獲取的數據
                        if (searchResult.Data != null && searchResult.Data.Count > 0)
                        {
                            Console.WriteLine($"📊 保存已獲取的 {searchResult.Data.Count} 位用戶數據");
                            await userDataService.MergeAndUpdateUsersAsync(searchResult.Data);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"❌ 搜尋過程中發生錯誤: {searchResult.ErrorMessage}");
                    }
                    
                    Console.WriteLine("\n按任意鍵退出...");
                    Console.ReadKey();
                    return;
                }

                var newUsers = searchResult.Data ?? new List<TaiwanGitHubPopularUsers.Models.GitHubUser>();
                
                if (newUsers.Count == 0)
                {
                    Console.WriteLine("❌ 沒有找到符合條件的新用戶");
                }
                else
                {
                    Console.WriteLine($"✅ 本次運行找到 {newUsers.Count} 位符合條件的用戶 (followers >= 100)");

                    // 合併和更新數據
                    Console.WriteLine("📊 正在合併和更新用戶數據...");
                    var allUsers = await userDataService.MergeAndUpdateUsersAsync(newUsers);

                    // 顯示摘要
                    userDataService.PrintUserSummary(allUsers);
                }

                Console.WriteLine($"\n✅ 程序執行完成！");
                Console.WriteLine($"結束時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 程序執行時發生錯誤: {ex.Message}");
                Console.WriteLine($"錯誤詳情: {ex.StackTrace}");
            }

            Console.WriteLine("\n按任意鍵退出...");
            Console.ReadKey();
        }
    }
}
