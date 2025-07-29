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
        private static readonly string[] TaiwanLocations = {
            "Taiwan", "Taipei", "New Taipei", "Taoyuan", "Taichung", "Tainan", "Kaohsiung", 
            "Hsinchu", "Keelung", "Chiayi", "Changhua", "Yunlin", "Nantou", "Pingtung", 
            "Yilan", "Hualien", "Taitung", "Penghu", "Kinmen", "Matsu"
        };

        private static readonly string[] SearchQueries = {
            "location:Taiwan",
            "location:Taipei",
            "location:\"New Taipei\"",
            "location:Taoyuan",
            "location:Taichung",
            "location:Tainan",
            "location:Kaohsiung",
            "location:Hsinchu",
            "location:Keelung",
            "location:Chiayi",
            "location:Changhua",
            "location:Yunlin",
            "location:Nantou",
            "location:Pingtung",
            "location:Yilan",
            "location:Hualien",
            "location:Taitung",
            "location:Penghu",
            "location:Kinmen",
            "location:Matsu"
        };

        static async Task Main(string[] args)
        {
            Console.WriteLine("台灣知名GitHub用戶排名系統");
            Console.WriteLine("正在搜尋台灣地區的GitHub用戶...");

            // 設定GitHub API headers
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Taiwan-Popular-Developers");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

            var allUsers = new List<GitHubUser>();
            var processedUsers = new HashSet<string>();

            // 搜尋每個地區的用戶
            foreach (var query in SearchQueries)
            {
                Console.WriteLine($"搜尋地區: {query}");
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

            Console.WriteLine($"找到 {allUsers.Count} 個台灣地區的GitHub用戶");

            // 計算每個用戶的分數
            foreach (var user in allUsers)
            {
                await CalculateUserScore(user);
            }

            // 按分數排序
            var rankedUsers = allUsers.OrderByDescending(u => u.Score).ToList();

            // 生成Markdown
            var markdown = GenerateMarkdown(rankedUsers);
            
            // 儲存到檔案
            await File.WriteAllTextAsync("taiwan_popular_developers.md", markdown, Encoding.UTF8);
            
            Console.WriteLine("排名已生成並儲存到 taiwan_popular_developers.md");
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
            const int maxPages = 10; // 每個查詢最多10頁

            while (page <= maxPages)
            {
                var url = $"https://api.github.com/search/users?q={Uri.EscapeDataString(query)}&sort=followers&order=desc&page={page}&per_page=100";
                
                var response = await MakeGitHubApiCall<dynamic>(url);
                
                if (!response.IsSuccess)
                {
                    Console.WriteLine($"搜尋用戶時發生錯誤: {response.ErrorMessage}");
                    break;
                }

                var items = response.Data.items;
                if (items == null || items.Count == 0)
                    break;

                foreach (var item in items)
                {
                    var user = new GitHubUser
                    {
                        Login = item.login,
                        Name = item.name ?? item.login,
                        Location = item.location ?? "",
                        Followers = item.followers ?? 0,
                        PublicRepos = item.public_repos ?? 0,
                        AvatarUrl = item.avatar_url ?? "",
                        HtmlUrl = item.html_url ?? ""
                    };

                    // 檢查是否為台灣地區用戶
                    if (IsTaiwanLocation(user.Location))
                    {
                        users.Add(user);
                    }
                }

                page++;
                await Task.Delay(100); // 避免API限制
            }

            return users;
        }

        static async Task CalculateUserScore(GitHubUser user)
        {
            Console.WriteLine($"計算用戶分數: {user.Login}");

            // 獲取用戶詳細資訊
            var userDetail = await GetUserDetail(user.Login);
            if (userDetail != null)
            {
                user.Name = userDetail.Name;
                user.Bio = userDetail.Bio;
                user.CreatedAt = userDetail.CreatedAt;
            }

            // 獲取用戶的頂級倉庫
            var repositories = await GetUserRepositories(user.Login);
            var topRepos = repositories.OrderByDescending(r => r.StargazersCount + r.ForksCount).Take(5).ToList();
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
            score += user.TopRepositories.Sum(r => r.StargazersCount * 2.0 + r.ForksCount * 1.0);
            
            // 組織貢獻個人能排在前五名的專案 star + fork
            score += user.TopOrganizationRepositories.Sum(r => r.StargazersCount * 1.5 + r.ForksCount * 0.75);

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
                        // 檢查用戶是否為貢獻者
                        var contributorsUrl = $"https://api.github.com/repos/{repo.full_name}/contributors";
                        var contributorsResponse = await MakeGitHubApiCall<List<dynamic>>(contributorsUrl);
                        
                        if (contributorsResponse.IsSuccess)
                        {
                            var isContributor = contributorsResponse.Data.Any(c => c.login == username);
                            if (isContributor)
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
                    else
                    {
                        return new GitHubApiResponse<T>
                        {
                            IsSuccess = false,
                            ErrorMessage = $"HTTP {response.StatusCode}: {content}"
                        };
                    }
                }
                catch (Exception ex)
                {
                    retryCount++;
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

        static bool IsTaiwanLocation(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
                return false;

            var locationLower = location.ToLower();
            return TaiwanLocations.Any(taiwanLocation => 
                locationLower.Contains(taiwanLocation.ToLower()));
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

            for (int i = 0; i < users.Count; i++)
            {
                var user = users[i];
                sb.AppendLine($"## {i + 1}. {user.Name} (@{user.Login})");
                sb.AppendLine();
                
                if (!string.IsNullOrEmpty(user.Bio))
                {
                    sb.AppendLine($"**簡介**: {user.Bio}");
                    sb.AppendLine();
                }
                
                sb.AppendLine($"**位置**: {user.Location}");
                sb.AppendLine($"**追蹤者**: {user.Followers:N0}");
                sb.AppendLine($"**公開倉庫**: {user.PublicRepos}");
                sb.AppendLine($"**總分**: {user.Score:F0}");
                sb.AppendLine($"**GitHub**: [{user.HtmlUrl}]({user.HtmlUrl})");
                sb.AppendLine();
                
                if (user.TopRepositories.Any())
                {
                    sb.AppendLine("### 個人熱門專案");
                    sb.AppendLine();
                    sb.AppendLine("| 專案名稱 | 描述 | Star | Fork | 語言 |");
                    sb.AppendLine("|---------|------|------|------|------|");
                    
                    foreach (var repo in user.TopRepositories.Take(5))
                    {
                        var description = string.IsNullOrEmpty(repo.Description) ? "-" : repo.Description.Replace("|", "\\|");
                        sb.AppendLine($"| [{repo.Name}]({repo.HtmlUrl}) | {description} | {repo.StargazersCount:N0} | {repo.ForksCount:N0} | {repo.Language ?? "-"} |");
                    }
                    sb.AppendLine();
                }
                
                if (user.TopOrganizationRepositories.Any())
                {
                    sb.AppendLine("### 組織貢獻專案");
                    sb.AppendLine();
                    sb.AppendLine("| 專案名稱 | 組織 | 描述 | Star | Fork | 語言 |");
                    sb.AppendLine("|---------|------|------|------|------|------|");
                    
                    foreach (var repo in user.TopOrganizationRepositories.Take(5))
                    {
                        var description = string.IsNullOrEmpty(repo.Description) ? "-" : repo.Description.Replace("|", "\\|");
                        sb.AppendLine($"| [{repo.Name}]({repo.HtmlUrl}) | {repo.OwnerLogin} | {description} | {repo.StargazersCount:N0} | {repo.ForksCount:N0} | {repo.Language ?? "-"} |");
                    }
                    sb.AppendLine();
                }
                
                sb.AppendLine("---");
                sb.AppendLine();
            }
            
            return sb.ToString();
        }
    }
}
