using System.Text.Json;
using TaiwanGitHubPopularUsers.Models;

namespace TaiwanGitHubPopularUsers
{
    public class DiagnosticTool : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _token;

        public DiagnosticTool(string token)
        {
            _token = token;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Taiwan-GitHub-Popular-Users");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"token {token}");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        }

        public async Task DiagnoseLocationSearchAsync(string location)
        {
            try
            {
                var searchUrl = $"https://api.github.com/search/users?q=location:{Uri.EscapeDataString(location)}&per_page=30&sort=followers&order=desc";
                
                Console.WriteLine($"\n=== 診斷地區: {location} ===");
                Console.WriteLine($"搜尋 URL: {searchUrl}");
                
                var response = await _httpClient.GetAsync(searchUrl);
                
                Console.WriteLine($"HTTP 狀態: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    
                    // 保存原始響應到文件以便檢查
                    await File.WriteAllTextAsync($"debug_{location}_response.json", json);
                    Console.WriteLine($"原始響應已保存到: debug_{location}_response.json");
                    
                    var searchResult = JsonSerializer.Deserialize<GitHubSearchResponse>(json);
                    
                    Console.WriteLine($"總計用戶數: {searchResult?.TotalCount ?? 0}");
                    Console.WriteLine($"返回用戶數: {searchResult?.Items?.Count ?? 0}");
                    
                    if (searchResult?.Items != null && searchResult.Items.Count > 0)
                    {
                        Console.WriteLine("\n⚠️  注意：搜索 API 返回的 followers 數量通常為 0");
                        Console.WriteLine("正在獲取用戶詳細信息（包含真實的 followers 數量）...\n");
                        
                        var detailedUsers = new List<GitHubUser>();
                        
                        // 限制只處理前 10 個用戶以避免過多 API 請求
                        var usersToCheck = searchResult.Items.Take(10).ToList();
                        
                        foreach (var user in usersToCheck)
                        {
                            Console.WriteLine($"📥 獲取用戶 {user.Login} 的詳細信息...");
                            var detailedUser = await GetUserDetailsAsync(user.Login);
                            
                            if (detailedUser != null)
                            {
                                detailedUsers.Add(detailedUser);
                                Console.WriteLine($"   ✅ {user.Login}: {detailedUser.Followers} followers, {detailedUser.PublicRepos} repos");
                            }
                            else
                            {
                                Console.WriteLine($"   ❌ 無法獲取 {user.Login} 的詳細信息");
                            }
                            
                            await Task.Delay(1000); // 避免 API 限制
                        }
                        
                        if (detailedUsers.Count > 0)
                        {
                            Console.WriteLine("\n=== 詳細用戶信息 ===");
                            Console.WriteLine("用戶名\t\t追蹤者\t倉庫數\t地區");
                            Console.WriteLine("======\t\t======\t======\t====");
                            
                            foreach (var user in detailedUsers.OrderByDescending(u => u.Followers))
                            {
                                Console.WriteLine($"{user.Login,-15}\t{user.Followers,6}\t{user.PublicRepos,6}\t{user.Location ?? "未設定"}");
                            }
                            
                            var qualifiedUsers = detailedUsers.Where(u => u.Followers >= 50).ToList();
                            Console.WriteLine($"\n符合條件的用戶 (followers >= 50): {qualifiedUsers.Count}");
                            
                            if (qualifiedUsers.Count > 0)
                            {
                                Console.WriteLine("符合條件的用戶:");
                                foreach (var user in qualifiedUsers)
                                {
                                    Console.WriteLine($"  - {user.Login}: {user.Followers} followers, {user.PublicRepos} repos, 地區: {user.Location ?? "未設定"}");
                                }
                            }
                            
                            var maxFollowers = detailedUsers.Max(u => u.Followers);
                            var minFollowers = detailedUsers.Min(u => u.Followers);
                            Console.WriteLine($"\nfollowers 範圍: {minFollowers} - {maxFollowers}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("❌ 沒有找到任何用戶");
                    }
                }
                else
                {
                    Console.WriteLine($"❌ API 請求失敗: {response.ReasonPhrase}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"錯誤內容: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 診斷過程中發生錯誤: {ex.Message}");
            }
        }

        private async Task<GitHubUser?> GetUserDetailsAsync(string username)
        {
            try
            {
                var userUrl = $"https://api.github.com/users/{username}";
                var response = await _httpClient.GetAsync(userUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var user = JsonSerializer.Deserialize<GitHubUser>(json);
                    if (user != null)
                    {
                        user.LastFetched = DateTime.UtcNow;
                    }
                    return user;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ 獲取用戶 {username} 詳細信息時發生錯誤: {ex.Message}");
            }
            
            return null;
        }

        public async Task TestMultipleLocationsAsync()
        {
            var testLocations = new[]
            {
                "Taiwan",
                "Taipei",
                "Penghu",
                "澎湖",
                "台灣",
                "臺灣"
            };

            foreach (var location in testLocations)
            {
                await DiagnoseLocationSearchAsync(location);
                await Task.Delay(2000); // 避免 API 限制
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _httpClient?.Dispose();
            }
        }
    }
}
