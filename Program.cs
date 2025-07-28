using TaiwanGitHubPopularUsers.Services;
using TaiwanGitHubPopularUsers.Models;

namespace TaiwanGitHubPopularUsers
{
    static class Program
    {
        private static string GITHUB_TOKEN = File.ReadAllText("C:\\Token"); // 請替換為您的 GitHub Token
        
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== 台灣 GitHub 知名開發者抓取工具 ===");
            Console.WriteLine($"開始時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            
            // 檢查是否是診斷模式
            if (args.Length > 0 && args[0].ToLower() == "--diagnose")
            {
                await RunDiagnosticModeAsync();
                return;
            }
            
            // 檢查是否是影響力報告模式
            if (args.Length > 0 && args[0].ToLower() == "--influence")
            {
                await RunInfluenceReportModeAsync();
                return;
            }
            
            // 檢查是否是專案豐富化模式
            if (args.Length > 0 && args[0].ToLower() == "--enrich")
            {
                await RunEnrichProjectsModeAsync();
                return;
            }
            
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
                        Console.WriteLine("\n🚫 遇到 GitHub API 限制，程序已停止並保存進度");
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
                
                // 處理 API 請求限制的情況
                if (!string.IsNullOrEmpty(searchResult.ErrorMessage) && searchResult.ErrorMessage.Contains("API 請求限制"))
                {
                    Console.WriteLine($"\n⚠️  {searchResult.ErrorMessage}");
                    Console.WriteLine("📊 本次運行已完成，將保存已獲取的數據");
                    
                    if (newUsers.Count > 0)
                    {
                        Console.WriteLine($"✅ 本次運行找到 {newUsers.Count} 位符合條件的用戶");
                        var allUsers = await userDataService.MergeAndUpdateUsersAsync(newUsers);
                        userDataService.PrintUserSummary(allUsers);
                        Console.WriteLine("\n💡 提示: 再次運行程序以繼續搜尋剩餘地區");
                    }
                    else
                    {
                        Console.WriteLine("📊 本次運行沒有找到新用戶");
                        var existingUsers = await userDataService.LoadExistingUsersAsync();
                        userDataService.PrintUserSummary(existingUsers);
                    }
                    
                    Console.WriteLine($"\n✅ 程序執行完成！數據已保存到 Users.json");
                    Console.WriteLine($"結束時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine("\n按任意鍵退出...");
                    Console.ReadKey();
                    return;
                }
                
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
        }

        private static async Task RunDiagnosticModeAsync()
        {
            Console.WriteLine("\n🔧 === 診斷模式 ===");
            Console.WriteLine("這個模式將幫助診斷為什麼找不到符合條件的用戶");
            
            try
            {
                using var diagnosticTool = new DiagnosticTool(GITHUB_TOKEN);
                
                Console.WriteLine("\n請選擇診斷選項:");
                Console.WriteLine("1. 診斷特定地區");
                Console.WriteLine("2. 診斷多個測試地區");
                Console.Write("請選擇 (1 或 2): ");
                
                var choice = Console.ReadLine();
                
                if (choice == "1")
                {
                    Console.Write("請輸入要診斷的地區名稱: ");
                    var location = Console.ReadLine();
                    if (!string.IsNullOrEmpty(location))
                    {
                        await diagnosticTool.DiagnoseLocationSearchAsync(location);
                    }
                }
                else if (choice == "2")
                {
                    await diagnosticTool.TestMultipleLocationsAsync();
                }
                else
                {
                    Console.WriteLine("無效選擇，使用默認測試");
                    await diagnosticTool.TestMultipleLocationsAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 診斷模式執行錯誤: {ex.Message}");
            }
            
            Console.WriteLine("\n診斷完成！按任意鍵退出...");
            Console.ReadKey();
        }

        private static async Task RunInfluenceReportModeAsync()
        {
            Console.WriteLine("\n📊 === 台灣 GitHub 影響力報告模式 ===");
            Console.WriteLine("這個模式將生成包含個人開發者和組織的綜合影響力報告");
            
            try
            {
                var userDataService = new UserDataService();
                var organizationService = new OrganizationService(GITHUB_TOKEN);
                var reportService = new InfluenceReportService();

                Console.WriteLine("\n🔍 載入現有用戶數據...");
                var users = await userDataService.LoadExistingUsersAsync();
                
                if (users.Count == 0)
                {
                    Console.WriteLine("❌ 沒有找到用戶數據，請先運行主程序搜集數據");
                    Console.WriteLine("提示: 運行 'dotnet run' 來搜集用戶數據");
                    Console.WriteLine("\n按任意鍵退出...");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine($"✅ 已載入 {users.Count} 位用戶數據");

                Console.WriteLine("\n🏢 搜尋台灣地區的組織...");
                var organizations = await organizationService.SearchTaiwanOrganizationsAsync(30);
                
                Console.WriteLine($"✅ 找到 {organizations.Count} 個組織");

                Console.WriteLine("\n📈 生成影響力報告...");
                var report = reportService.GenerateInfluenceReport(users, organizations);
                
                await reportService.SaveReportAsync(report);
                reportService.PrintInfluenceReport(report);

                Console.WriteLine($"\n✅ 影響力報告生成完成！");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 影響力報告模式執行錯誤: {ex.Message}");
                Console.WriteLine($"錯誤詳情: {ex.StackTrace}");
            }
            
            Console.WriteLine("\n按任意鍵退出...");
            Console.ReadKey();
        }

        private static async Task RunEnrichProjectsModeAsync()
        {
            Console.WriteLine("\n📂 === 專案豐富化模式 ===");
            Console.WriteLine("這個模式將為現有用戶添加主要貢獻專案信息（包含個人和組織專案）");
            
            try
            {
                var userDataService = new UserDataService();
                
                Console.WriteLine("\n🔍 載入現有用戶數據...");
                var users = await userDataService.LoadExistingUsersAsync();
                
                if (users.Count == 0)
                {
                    Console.WriteLine("❌ 沒有找到用戶數據，請先運行主程序搜集數據");
                    Console.WriteLine("提示: 運行 'dotnet run' 來搜集用戶數據");
                    Console.WriteLine("\n按任意鍵退出...");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine($"✅ 已載入 {users.Count} 位用戶數據");
                
                // 檢查是否有用戶已經有專案信息
                var usersWithProjects = users.Where(u => u.Projects != null && u.Projects.Count > 0).ToList();
                var usersWithoutProjects = users.Where(u => u.Projects == null || u.Projects.Count == 0).ToList();
                
                Console.WriteLine($"📊 現狀統計:");
                Console.WriteLine($"   - 已有專案信息: {usersWithProjects.Count} 位用戶");
                Console.WriteLine($"   - 需要豐富化: {usersWithoutProjects.Count} 位用戶");
                
                if (usersWithoutProjects.Count == 0)
                {
                    Console.WriteLine("\n🎉 所有用戶都已經有專案信息！");
                    Console.WriteLine("\n按任意鍵退出...");
                    Console.ReadKey();
                    return;
                }
                
                Console.WriteLine($"\n🔄 即將為 {usersWithoutProjects.Count} 位用戶添加專案信息");
                Console.WriteLine("⚠️  注意: 每位用戶需要約 3-5 個 API 請求，本次運行最多 50 個請求");
                
                var maxUsersToProcess = Math.Min(usersWithoutProjects.Count, 15); // 每個用戶約 3-4 個請求
                Console.WriteLine($"📊 本次運行將處理前 {maxUsersToProcess} 位用戶");
                
                Console.Write("是否繼續？(Y/n): ");
                var input = Console.ReadLine()?.ToLower();
                if (input == "n" || input == "no")
                {
                    Console.WriteLine("操作已取消");
                    Console.WriteLine("\n按任意鍵退出...");
                    Console.ReadKey();
                    return;
                }

                using var userProjectService = new UserProjectService(GITHUB_TOKEN);
                
                var apiRequestCount = 0;
                var processedCount = 0;
                var maxApiRequests = 50;
                
                Console.WriteLine($"\n📂 開始為用戶添加專案信息...");
                Console.WriteLine($"API 請求限制: {maxApiRequests} 次");
                
                foreach (var user in usersWithoutProjects.Take(maxUsersToProcess))
                {
                    try
                    {
                        Console.WriteLine($"\n[{processedCount + 1}/{maxUsersToProcess}] 處理用戶: {user.Login}");
                        
                        var result = await userProjectService.EnrichUserWithProjectsAsync(user);
                        
                        // 估算 API 請求數量（每個用戶約 3-4 個請求）
                        apiRequestCount += 4;
                        
                        if (result.Success)
                        {
                            processedCount++;
                            Console.WriteLine($"   ✅ 成功為 {user.Login} 添加專案信息");
                            Console.WriteLine($"   📊 找到 {user.Projects?.Count ?? 0} 個主要專案");
                            Console.WriteLine($"   ⭐ 總計: {user.TotalStars} stars, {user.TotalForks} forks");
                        }
                        else
                        {
                            Console.WriteLine($"   ❌ 處理 {user.Login} 時發生錯誤: {result.ErrorMessage}");
                            
                            if (result.IsRateLimited)
                            {
                                Console.WriteLine("\n🚫 遇到 GitHub API 限制，停止處理");
                                break;
                            }
                        }
                        
                        // 檢查 API 請求限制
                        if (apiRequestCount >= maxApiRequests)
                        {
                            Console.WriteLine($"\n⚠️  已達到 API 請求限制 ({maxApiRequests} 次)，停止處理");
                            break;
                        }
                        
                        // 避免過快請求，每個用戶之間暫停 1 秒
                        await Task.Delay(1000);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"   ❌ 處理 {user.Login} 時發生異常: {ex.Message}");
                    }
                }
                
                // 保存更新的用戶數據
                Console.WriteLine($"\n💾 保存用戶數據...");
                await userDataService.SaveUsersAsync(users);
                
                Console.WriteLine($"\n✅ 專案豐富化完成！");
                Console.WriteLine($"📊 處理統計:");
                Console.WriteLine($"   - 成功處理: {processedCount} 位用戶");
                Console.WriteLine($"   - API 請求使用: 約 {apiRequestCount} 次");
                Console.WriteLine($"   - 剩餘待處理: {usersWithoutProjects.Count - processedCount} 位用戶");
                
                if (usersWithoutProjects.Count - processedCount > 0)
                {
                    Console.WriteLine($"\n💡 提示: 再次運行 'dotnet run --enrich' 來處理剩餘用戶");
                }
                
                // 顯示更新後的摘要
                Console.WriteLine($"\n📈 更新後統計:");
                var updatedUsersWithProjects = users.Where(u => u.Projects != null && u.Projects.Count > 0).ToList();
                Console.WriteLine($"   - 已有專案信息: {updatedUsersWithProjects.Count} 位用戶");
                Console.WriteLine($"   - 總計 Stars: {updatedUsersWithProjects.Sum(u => u.TotalStars):N0}");
                Console.WriteLine($"   - 總計 Forks: {updatedUsersWithProjects.Sum(u => u.TotalForks):N0}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 專案豐富化模式執行錯誤: {ex.Message}");
                Console.WriteLine($"錯誤詳情: {ex.StackTrace}");
            }
            
            Console.WriteLine("\n按任意鍵退出...");
            Console.ReadKey();
        }
    }
}
