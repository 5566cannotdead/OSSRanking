using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TaiwanPopularDevelopers
{
    public class GitHubUser
    {
        public string Login { get; set; } = "";
        public string Name { get; set; } = "";
        public string Location { get; set; } = "";
        public int Followers { get; set; }
        public int PublicRepos { get; set; }
        public string Bio { get; set; } = "";
        public string AvatarUrl { get; set; } = "";
        public string HtmlUrl { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public double Score { get; set; }
        public List<Repository> TopRepositories { get; set; } = new List<Repository>();
        public List<Repository> TopOrganizationRepositories { get; set; } = new List<Repository>();
        public List<Repository> AllRepositories { get; set; } = new List<Repository>();
    }

    public class Repository
    {
        public string Name { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Description { get; set; } = "";
        public int StargazersCount { get; set; }
        public int ForksCount { get; set; }
        public string HtmlUrl { get; set; } = "";
        public string Language { get; set; } = "";
        public bool IsFork { get; set; }
        public string OwnerLogin { get; set; } = "";
        public bool IsOrganization { get; set; }
    }

    public class GitHubApiResponse<T>
    {
        public T Data { get; set; } = default(T)!;
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = "";
        public int RemainingRequests { get; set; }
        public DateTime ResetTime { get; set; }
    }

    public class Program
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static string? githubToken;
        private static readonly int MinFollowers = 6000; // 最低追蹤者數量門檻

        private static readonly string[] SearchQueries = {
            $"followers:>{MinFollowers}+location:Taiwan",
           $"followers:>{MinFollowers}+location:Taipei",
           $"followers:>{MinFollowers}+location:Kaohsiung",
           //$"followers:>{MinFollowers}+location:\"New Taipei\"",
           //$"followers:>{MinFollowers}+location:Taoyuan",
           //$"followers:>{MinFollowers}+location:Taichung",
           //$"followers:>{MinFollowers}+location:Tainan",
           //$"followers:>{MinFollowers}+location:Hsinchu",
           //$"followers:>{MinFollowers}+location:Keelung",
           //$"followers:>{MinFollowers}+location:Chiayi",
           //$"followers:>{MinFollowers}+location:Changhua",
           //$"followers:>{MinFollowers}+location:Yunlin",
           //$"followers:>{MinFollowers}+location:Nantou",
           //$"followers:>{MinFollowers}+location:Pingtung",
           //$"followers:>{MinFollowers}+location:Yilan",
           //$"followers:>{MinFollowers}+location:Hualien",
           //$"followers:>{MinFollowers}+location:Taitung",
           //$"followers:>{MinFollowers}+location:Penghu",
           //$"followers:>{MinFollowers}+location:Kinmen",
           //$"followers:>{MinFollowers}+location:Matsu"
        };

        static async Task Main(string[] args)
        {
            Console.WriteLine("台灣知名GitHub用戶排名系統");
            Console.WriteLine("正在讀取GitHub API Token...");

            // 讀取GitHub API Token
            try
            {
                githubToken = await File.ReadAllTextAsync(@"C:\Token");
                githubToken = githubToken.Trim();
                Console.WriteLine("GitHub API Token 已載入");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"警告: 無法讀取GitHub API Token: {ex.Message}");
                Console.WriteLine("將使用匿名API調用（限制較多）");
                githubToken = null;
            }

            Console.WriteLine("正在搜尋台灣地區的GitHub用戶...");

            // 清理並設定基本 GitHub API headers
            ClearHttpClientHeaders();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Taiwan-Popular-Developers");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

            // 如果有 Token，設定授權並驗證其有效性
            if (!string.IsNullOrEmpty(githubToken))
            {
                try
                {
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"token {githubToken}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"設定 Token 時發生錯誤: {ex.Message}，將使用匿名模式");
                    ClearHttpClientHeaders();
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Taiwan-Popular-Developers");
                    httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
                    githubToken = null;
                }
            }

            if (!string.IsNullOrEmpty(githubToken))
            {
                Console.WriteLine("已設定 GitHub API 授權");
            }
            else
            {
                Console.WriteLine("使用匿名模式（API 限制較多）");
            }

            var allUsers = new List<GitHubUser>();
            var processedUsers = new HashSet<string>();

            // 搜尋每個地區的用戶
            foreach (var query in SearchQueries)
            {
                Console.WriteLine($"搜尋地區: {query}");
                try
                {
                    var users = await SearchGitHubUsers(query);
                    
                    foreach (var user in users)
                    {
                        if (!processedUsers.Contains(user.Login))
                        {
                            processedUsers.Add(user.Login);
                            allUsers.Add(user);
                        }
                    }

                    // 避免API限制，每次搜尋後稍作延遲
                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"搜尋地區 {query} 時發生嚴重錯誤: {ex.Message}");
                    Console.WriteLine("程序即將停止");
                    Environment.Exit(1);
                }
            }


            Console.WriteLine($"找到 {allUsers.Count} 個台灣地區的GitHub用戶");

            // 計算每個用戶的分數並獲取詳細資訊
            for (int i = 0; i < allUsers.Count; i++)
            {
                var user = allUsers[i];
                Console.WriteLine($"處理用戶 {i + 1}/{allUsers.Count}: {user.Login}");
                await CalculateUserScore(user);
                
                // 避免API限制
                await Task.Delay(500);
            }

            // 按分數排序
            var rankedUsers = allUsers.OrderByDescending(u => u.Score).ToList();

            // 儲存到JSON檔案
            var jsonData = new
            {
                GeneratedAt = DateTime.Now,
                TotalUsers = rankedUsers.Count,
                Users = rankedUsers
            };
            
            var jsonString = JsonConvert.SerializeObject(jsonData, Formatting.Indented);
            await File.WriteAllTextAsync("Users.json", jsonString, Encoding.UTF8);
            Console.WriteLine("用戶資料已儲存到 Users.json");

            // 生成Markdown
            var markdown = GenerateMarkdown(rankedUsers);
            await File.WriteAllTextAsync("Readme.md", markdown, Encoding.UTF8);
            
            Console.WriteLine("排名已生成並儲存到 Readme.md");
            Console.WriteLine($"前10名用戶:");
            for (int i = 0; i < Math.Min(10, rankedUsers.Count); i++)
            {
                var user = rankedUsers[i];
                Console.WriteLine($"{i + 1}. {user.Name} (@{user.Login}) - 分數: {user.Score:F0}");
            }
        }

        static async Task<List<GitHubUser>> SearchGitHubUsers(string query)
        {
            var users = new List<GitHubUser>();
            int page = 1;
            const int maxPages = 100; // 每個查詢最多100頁
            bool hasUsersWith50PlusFollowers = true;

            while (page <= maxPages && hasUsersWith50PlusFollowers)
            {
                //增加 url follower > 最小追踪数量
                var url = $"https://api.github.com/search/users?q={query}&sort=followers&order=desc&page={page}&per_page=100";
                
                var response = await MakeGitHubApiCall<dynamic>(url);
                
                if (!response.IsSuccess)
                {
                    Console.WriteLine($"搜尋用戶時發生錯誤: {response.ErrorMessage}");
                    
                    // 如果是服務不可用錯誤，程序已經在 MakeGitHubApiCall 中處理並退出
                    // 如果是其他錯誤，跳出循環但不終止程序
                    break;
                }

                var items = response.Data?.items;
                if (items == null || items.Count == 0)
                    break;

                hasUsersWith50PlusFollowers = false;
                foreach (var item in items)
                {
                    var followers = item.followers ?? 0;
                    var user = new GitHubUser
                    {
                        Login = item.login,
                        Name = item.name ?? item.login,
                        Location = item.location ?? "",
                        Followers = followers,
                        PublicRepos = item.public_repos ?? 0,
                        AvatarUrl = item.avatar_url ?? "",
                        HtmlUrl = item.html_url ?? ""
                    };
                    users.Add(user);
                    if (followers >= MinFollowers)
                    {
                        hasUsersWith50PlusFollowers = true;
                    }
                }

                page++;
                await Task.Delay(100); // 避免API限制
            }

            return users;
        }

        static async Task CalculateUserScore(GitHubUser user)
        {
            // 獲取用戶詳細資訊
            var userDetail = await GetUserDetail(user.Login);
            if (userDetail != null)
            {
                user.Name = userDetail.Name;
                user.Bio = userDetail.Bio;
                user.CreatedAt = userDetail.CreatedAt;
                user.Followers = userDetail.Followers;  // 更新 Followers
                user.PublicRepos = userDetail.PublicRepos;  // 更新 PublicRepos
                user.Location = userDetail.Location;  // 也更新 Location 以確保準確性
            }

            // 獲取用戶的所有倉庫（包括五顆星以下的）
            var allRepositories = await GetAllUserRepositories(user.Login);
            user.AllRepositories = allRepositories;

            // 獲取用戶的頂級倉庫（前五名）
            var topRepos = allRepositories.OrderByDescending(r => r.StargazersCount + r.ForksCount).Take(5).ToList();
            user.TopRepositories = topRepos;

            // 獲取用戶參與的組織倉庫
            var orgRepos = await GetUserOrganizationRepositories(user.Login);
            var topOrgRepos = orgRepos.OrderByDescending(r => r.StargazersCount + r.ForksCount).Take(5).ToList();
            user.TopOrganizationRepositories = topOrgRepos;

            // 計算分數
            double score = 0;
            
            // 個人追蹤數量
            score += user.Followers * 1.0;
            
            // 個人專案star + fork
            score += user.TopRepositories.Sum(r => r.StargazersCount * 1.0 + r.ForksCount * 1.0);
            
            // 組織貢獻個人能排在前五名的專案 star + fork
            score += user.TopOrganizationRepositories.Sum(r => r.StargazersCount * 1.0 + r.ForksCount * 1.0);

            user.Score = score;
        }

        static async Task<GitHubUser?> GetUserDetail(string username)
        {
            var url = $"https://api.github.com/users/{username}";
            var response = await MakeGitHubApiCall<dynamic>(url);
            
            if (!response.IsSuccess)
                return null;

            return new GitHubUser
            {
                Login = response.Data.login,
                Name = response.Data.name ?? response.Data.login,
                Location = response.Data.location ?? "",
                Followers = response.Data.followers ?? 0,
                PublicRepos = response.Data.public_repos ?? 0,
                Bio = response.Data.bio ?? "",
                AvatarUrl = response.Data.avatar_url ?? "",
                HtmlUrl = response.Data.html_url ?? "",
                CreatedAt = response.Data.created_at != null ? DateTime.Parse(response.Data.created_at.ToString()) : DateTime.MinValue
            };
        }

        static async Task<List<Repository>> GetAllUserRepositories(string username)
        {
            var repositories = new List<Repository>();
            int page = 1;

            while (page <= 10) // 最多10頁，獲取更多倉庫
            {
                var url = $"https://api.github.com/users/{username}/repos?page={page}&per_page=100&sort=stars&direction=desc";
                var response = await MakeGitHubApiCall<List<dynamic>>(url);
                
                if (!response.IsSuccess)
                    break;

                if (response.Data.Count == 0)
                    break;

                foreach (var repo in response.Data)
                {
                    repositories.Add(new Repository
                    {
                        Name = repo.name,
                        FullName = repo.full_name,
                        Description = repo.description ?? "",
                        StargazersCount = repo.stargazers_count ?? 0,
                        ForksCount = repo.forks_count ?? 0,
                        HtmlUrl = repo.html_url ?? "",
                        Language = repo.language ?? "",
                        IsFork = repo.fork ?? false,
                        OwnerLogin = repo.owner?.login ?? "",
                        IsOrganization = false
                    });
                }

                page++;
                await Task.Delay(100);
            }

            return repositories;
        }

        static async Task<List<Repository>> GetUserRepositories(string username)
        {
            var repositories = new List<Repository>();
            int page = 1;

            while (page <= 5) // 最多5頁
            {
                var url = $"https://api.github.com/users/{username}/repos?page={page}&per_page=100&sort=stars&direction=desc";
                var response = await MakeGitHubApiCall<List<dynamic>>(url);
                
                if (!response.IsSuccess)
                    break;

                if (response.Data.Count == 0)
                    break;

                foreach (var repo in response.Data)
                {
                    repositories.Add(new Repository
                    {
                        Name = repo.name,
                        FullName = repo.full_name,
                        Description = repo.description ?? "",
                        StargazersCount = repo.stargazers_count ?? 0,
                        ForksCount = repo.forks_count ?? 0,
                        HtmlUrl = repo.html_url ?? "",
                        Language = repo.language ?? "",
                        IsFork = repo.fork ?? false,
                        OwnerLogin = repo.owner?.login ?? "",
                        IsOrganization = false
                    });
                }

                page++;
                await Task.Delay(100);
            }

            return repositories;
        }

        static async Task<List<Repository>> GetUserOrganizationRepositories(string username)
        {
            var repositories = new List<Repository>();
            
            // 獲取用戶參與的組織
            var orgsUrl = $"https://api.github.com/users/{username}/orgs";
            var orgsResponse = await MakeGitHubApiCall<List<dynamic>>(orgsUrl);
            
            if (!orgsResponse.IsSuccess)
                return repositories;

            foreach (var org in orgsResponse.Data)
            {
                var orgLogin = org.login;
                
                // 獲取組織的倉庫
                var orgReposUrl = $"https://api.github.com/orgs/{orgLogin}/repos?sort=stars&direction=desc&per_page=100";
                var orgReposResponse = await MakeGitHubApiCall<List<dynamic>>(orgReposUrl);
                
                if (orgReposResponse.IsSuccess)
                {
                    foreach (var repo in orgReposResponse.Data.Take(10)) // 每個組織最多10個倉庫
                    {
                        // 檢查用戶是否為前五名貢獻者
                        var contributorsUrl = $"https://api.github.com/repos/{repo.full_name}/contributors?per_page=5";
                        var contributorsResponse = await MakeGitHubApiCall<List<dynamic>>(contributorsUrl);
                        
                        if (contributorsResponse.IsSuccess)
                        {
                            // 檢查用戶是否在前五名貢獻者中
                            if(contributorsResponse.Data==null)
                            {
                                Console.WriteLine($"警告: 無法獲取 {repo.full_name} 的貢獻者資料，可能是API限制或倉庫不存在");
                                continue;
                            }
                            var isTopFiveContributor = contributorsResponse.Data.Any(c => c.login == username);
                            if (isTopFiveContributor)
                            {
                                repositories.Add(new Repository
                                {
                                    Name = repo.name,
                                    FullName = repo.full_name,
                                    Description = repo.description ?? "",
                                    StargazersCount = repo.stargazers_count ?? 0,
                                    ForksCount = repo.forks_count ?? 0,
                                    HtmlUrl = repo.html_url ?? "",
                                    Language = repo.language ?? "",
                                    IsFork = repo.fork ?? false,
                                    OwnerLogin = orgLogin,
                                    IsOrganization = true
                                });
                            }
                        }
                        
                        await Task.Delay(50);
                    }
                }
                
                await Task.Delay(100);
            }

            return repositories;
        }

        static async Task<GitHubApiResponse<T>> MakeGitHubApiCall<T>(string url)
        {
            int retryCount = 0;
            const int maxRetries = 3;

            while (retryCount < maxRetries)
            {
                try
                {
                    var response = await httpClient.GetAsync(url);
                    var content = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var data = JsonConvert.DeserializeObject<T>(content);
                        return new GitHubApiResponse<T>
                        {
                            Data = data!,
                            IsSuccess = true,
                            RemainingRequests = int.Parse(response.Headers.GetValues("X-RateLimit-Remaining").FirstOrDefault() ?? "0"),
                            ResetTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(response.Headers.GetValues("X-RateLimit-Reset").FirstOrDefault() ?? "0")).DateTime
                        };
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        // 401 Unauthorized - Token 問題
                        var errorMsg = "GitHub API Token 驗證失敗。請檢查：\n" +
                                      "1. Token 是否正確（應以 ghp_ 或 github_pat_ 開頭）\n" +
                                      "2. Token 是否已過期\n" +
                                      "3. Token 是否有適當的權限（至少需要 public_repo 權限）\n" +
                                      "4. 請到 https://github.com/settings/tokens 檢查或重新產生 Token";
                        
                        return new GitHubApiResponse<T>
                        {
                            IsSuccess = false,
                            ErrorMessage = errorMsg
                        };
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        // API限制，等待5分鐘後重試
                        var resetTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(response.Headers.GetValues("X-RateLimit-Reset").FirstOrDefault() ?? "0")).DateTime;
                        var waitTime = resetTime - DateTime.UtcNow;
                        
                        if (waitTime.TotalSeconds > 0)
                        {
                            Console.WriteLine($"GitHub API限制，等待 {waitTime.TotalMinutes:F1} 分鐘後重試...");
                            await Task.Delay((int)waitTime.TotalMilliseconds + 1000);
                        }
                        else
                        {
                            await Task.Delay(300000); // 等待5分鐘
                        }
                        
                        retryCount++;
                        continue;
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable || 
                             response.StatusCode == System.Net.HttpStatusCode.BadGateway ||
                             response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                    {
                        // 服務不可用錯誤，重試
                        retryCount++;
                        Console.WriteLine($"GitHub API 服務不可用 ({response.StatusCode})，第 {retryCount} 次重試...");
                        
                        if (retryCount >= maxRetries)
                        {
                            Console.WriteLine("GitHub API 服務持續不可用，程序即將停止");
                            Environment.Exit(1);
                        }
                        
                        await Task.Delay(5000 * retryCount); // 遞增等待時間
                        continue;
                    }
                    else
                    {
                        // 檢查是否是服務不可用的錯誤訊息
                        if (content.Contains("No server is currently available") || 
                            content.Contains("service your request"))
                        {
                            retryCount++;
                            Console.WriteLine($"GitHub API 服務不可用，第 {retryCount} 次重試...");
                            
                            if (retryCount >= maxRetries)
                            {
                                Console.WriteLine("GitHub API 服務持續不可用，程序即將停止");
                                Environment.Exit(1);
                            }
                            
                            await Task.Delay(5000 * retryCount); // 遞增等待時間
                            continue;
                        }
                        
                        return new GitHubApiResponse<T>
                        {
                            IsSuccess = false,
                            ErrorMessage = $"HTTP {response.StatusCode}: {content}"
                        };
                    }
                }
                catch (HttpRequestException ex)
                {
                    retryCount++;
                    Console.WriteLine($"網路連線問題: {ex.Message}，第 {retryCount} 次重試...");
                    
                    if (retryCount >= maxRetries)
                    {
                        Console.WriteLine("網路連線持續有問題，程序即將停止");
                        Environment.Exit(1);
                    }
                    
                    await Task.Delay(5000 * retryCount); // 遞增等待時間
                }
                catch (TaskCanceledException ex)
                {
                    retryCount++;
                    Console.WriteLine($"請求超時: {ex.Message}，第 {retryCount} 次重試...");
                    
                    if (retryCount >= maxRetries)
                    {
                        Console.WriteLine("請求持續超時，程序即將停止");
                        Environment.Exit(1);
                    }
                    
                    await Task.Delay(5000 * retryCount); // 遞增等待時間
                }
                catch (Exception ex)
                {
                    retryCount++;
                    Console.WriteLine($"API 調用異常: {ex.Message}，第 {retryCount} 次重試...");
                    
                    if (retryCount >= maxRetries)
                    {
                        return new GitHubApiResponse<T>
                        {
                            IsSuccess = false,
                            ErrorMessage = ex.Message
                        };
                    }
                    
                    await Task.Delay(1000 * retryCount); // 指數退避
                }
            }

            return new GitHubApiResponse<T>
            {
                IsSuccess = false,
                ErrorMessage = "達到最大重試次數"
            };
        }

        static async Task<bool> ValidateGitHubToken()
        {
            try
            {
                // 直接使用 httpClient 進行驗證，不使用 MakeGitHubApiCall 避免重複錯誤處理
                var response = await httpClient.GetAsync("https://api.github.com/user");
                
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Token 驗證失敗: {content}");
                    return false;
                }
                else
                {
                    Console.WriteLine($"Token 驗證遇到問題: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Token 驗證時發生異常: {ex.Message}");
                return false;
            }
        }

        static void ClearHttpClientHeaders()
        {
            try
            {
                httpClient.DefaultRequestHeaders.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"清理 HTTP headers 時發生異常: {ex.Message}");
            }
        }


        static string GenerateMarkdown(List<GitHubUser> users)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("# 台灣知名GitHub用戶排名");
            sb.AppendLine();
            sb.AppendLine("> 本排名基於以下指標計算：");
            sb.AppendLine("> - 個人追蹤數量");
            sb.AppendLine("> - 個人專案Star數量");
            sb.AppendLine("> - 個人專案Fork數量");
            sb.AppendLine("> - 組織貢獻專案的Star和Fork數量");
            sb.AppendLine();
            sb.AppendLine($"**更新時間**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"**總計用戶數**: {users.Count}");
            sb.AppendLine();

            // 生成表格標題
            sb.AppendLine("| 排名 | Total Influence | 開發者 | Followers | Personal Projects | Top Contributed Projects |");
            sb.AppendLine("|------|-----------------|--------|-----------|-------------------|--------------------------|");

            for (int i = 0; i < users.Count; i++)
            {
                var user = users[i];
                var rank = i + 1;
                var totalInfluence = $"**{user.Score:F0}**";
                
                // 開發者資訊 (頭像 + 姓名 + 位置)
                var developerInfo = $"[<img src=\"{user.AvatarUrl}&s=32\" width=\"32\" height=\"32\" style=\"border-radius: 50%;\" />]({user.HtmlUrl})<br/>**[{user.Login}]({user.HtmlUrl})**<br/>{user.Name}";
                if (!string.IsNullOrEmpty(user.Location))
                {
                    developerInfo += $"<br/>📍 {user.Location}";
                }
                
                var followers = user.Followers.ToString("N0");
                
                // 個人專案資訊
                var personalProjects = "";
                if (user.TopRepositories.Any())
                {
                    var totalStars = user.TopRepositories.Sum(r => r.StargazersCount);
                    var totalForks = user.TopRepositories.Sum(r => r.ForksCount);
                    personalProjects = $"⭐ {totalStars:N0} 🍴 {totalForks:N0}<br/>📦 {user.TopRepositories.Count} 個專案<br/>";
                    
                    var topRepos = user.TopRepositories.Take(3).ToList();
                    for (int j = 0; j < topRepos.Count; j++)
                    {
                        var repo = topRepos[j];
                        personalProjects += $"• [{repo.Name}]({repo.HtmlUrl}) ({repo.StargazersCount:N0}⭐)";
                        if (j < topRepos.Count - 1)
                        {
                            personalProjects += "<br/>";
                        }
                    }
                }
                else
                {
                    personalProjects = "-";
                }
                
                // 組織貢獻專案資訊
                var contributedProjects = "";
                if (user.TopOrganizationRepositories.Any())
                {
                    var totalOrgStars = user.TopOrganizationRepositories.Sum(r => r.StargazersCount);
                    var totalOrgForks = user.TopOrganizationRepositories.Sum(r => r.ForksCount);
                    contributedProjects = $"⭐ {totalOrgStars:N0} 🍴 {totalOrgForks:N0}<br/>🏢 {user.TopOrganizationRepositories.Count} 個專案<br/>";
                    
                    var topOrgRepos = user.TopOrganizationRepositories.Take(3).ToList();
                    for (int j = 0; j < topOrgRepos.Count; j++)
                    {
                        var repo = topOrgRepos[j];
                        contributedProjects += $"• [{repo.Name}]({repo.HtmlUrl}) ({repo.StargazersCount:N0}⭐)";
                        if (j < topOrgRepos.Count - 1)
                        {
                            contributedProjects += "<br/>";
                        }
                    }
                }
                else
                {
                    contributedProjects = "-";
                }
                
                // 轉義管道符號以避免表格格式錯誤
                developerInfo = developerInfo.Replace("|", "\\|");
                personalProjects = personalProjects.Replace("|", "\\|");
                contributedProjects = contributedProjects.Replace("|", "\\|");
                
                sb.AppendLine($"| {rank} | {totalInfluence} | {developerInfo} | {followers} | {personalProjects} | {contributedProjects} |");
            }
            
            return sb.ToString();
        }
    }
}
