using System.Text.Json;
using System.Net;
using TaiwanGitHubPopularUsers.Models;

namespace TaiwanGitHubPopularUsers.Services
{
    public class UserProjectService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _token;
        private const string ApiLimitMessage = "API 限制已達到";

        public UserProjectService(string token)
        {
            _token = token;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Taiwan-GitHub-Popular-Users");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"token {token}");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        }

        public async Task<ApiResponse<bool>> EnrichUserWithProjectsAsync(GitHubUser user)
        {
            try
            {
                Console.WriteLine($"📂 正在獲取 {user.Login} 的專案信息...");

                // 1. 獲取用戶所有個人倉庫
                var ownRepos = await GetAllUserRepositoriesAsync(user.Login);
                
                if (!ownRepos.Success && ownRepos.IsRateLimited)
                {
                    Console.WriteLine($"   🚫 獲取 {user.Login} 的倉庫時遇到 API 限制，停止後續處理");
                    return new ApiResponse<bool>
                    {
                        Success = false,
                        IsRateLimited = true,
                        ErrorMessage = "API 限制已達到，停止處理後續用戶"
                    };
                }
                
                // 2. 獲取用戶在貢獻專案中排名前五的專案
                var topContributedRepos = await GetUserTopContributedRepositoriesAsync(user.Login);
                
                if (!topContributedRepos.Success && topContributedRepos.IsRateLimited)
                {
                    Console.WriteLine($"   🚫 獲取 {user.Login} 的貢獻專案時遇到 API 限制，停止後續處理");
                    return new ApiResponse<bool>
                    {
                        Success = false,
                        IsRateLimited = true,
                        ErrorMessage = "API 限制已達到，停止處理後續用戶"
                    };
                }

                // 3. 合併所有符合條件的專案
                var allProjects = new List<UserProject>();
                
                // 添加所有個人專案
                if (ownRepos.Success && ownRepos.Data != null)
                {
                    allProjects.AddRange(ownRepos.Data);
                    Console.WriteLine($"   📊 個人專案: {ownRepos.Data.Count} 個");
                }
                
                // 添加排名前五的貢獻專案
                if (topContributedRepos.Success && topContributedRepos.Data != null)
                {
                    allProjects.AddRange(topContributedRepos.Data);
                    Console.WriteLine($"   🏆 前五貢獻專案: {topContributedRepos.Data.Count} 個");
                }

                // 按 Stars 排序並取前五名作為展示
                user.Projects = allProjects
                    .OrderByDescending(p => p.StargazersCount)
                    .Take(5)
                    .ToList();

                // 計算所有專案的總計（不限於展示的前五名）
                user.TotalStars = allProjects.Sum(p => p.StargazersCount);
                user.TotalForks = allProjects.Sum(p => p.ForksCount);

                Console.WriteLine($"   ✅ {user.Login}: 展示前 {user.Projects.Count} 個專案，總計 {user.TotalStars} stars, {user.TotalForks} forks");

                return new ApiResponse<bool>
                {
                    Success = true,
                    Data = true
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ 獲取 {user.Login} 專案信息時發生錯誤: {ex.Message}");
                return new ApiResponse<bool>
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 獲取用戶所有個人倉庫
        /// </summary>
        private async Task<ApiResponse<List<UserProject>>> GetAllUserRepositoriesAsync(string username)
        {
            try
            {
                var allProjects = new List<UserProject>();
                var page = 1;
                const int perPage = 100; // GitHub API 最大每頁數量
                
                while (true)
                {
                    var url = $"https://api.github.com/users/{username}/repos?per_page={perPage}&page={page}&sort=stars&direction=desc";
                    var httpResponse = await _httpClient.GetAsync(url);

                    if (httpResponse.StatusCode == HttpStatusCode.Forbidden)
                    {
                        return new ApiResponse<List<UserProject>>
                        {
                            Success = false,
                            IsRateLimited = true,
                            ErrorMessage = ApiLimitMessage
                        };
                    }

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        var json = await httpResponse.Content.ReadAsStringAsync();
                        var repos = JsonSerializer.Deserialize<List<GitHubRepository>>(json);

                        if (repos == null || repos.Count == 0)
                        {
                            break; // 沒有更多倉庫
                        }

                        // 過濾並轉換為 UserProject
                        var projects = repos.Where(r => !r.Name.StartsWith('.') && r.StargazersCount >= 0) // 包含所有星星數量，包括0
                            .Select(r => new UserProject
                            {
                                Name = r.Name,
                                FullName = r.FullName,
                                Description = r.Description,
                                StargazersCount = r.StargazersCount,
                                ForksCount = r.ForksCount,
                                Language = r.Language,
                                IsOwner = true,
                                Organization = null,
                                CreatedAt = r.CreatedAt,
                                UpdatedAt = r.UpdatedAt
                            }).ToList();

                        allProjects.AddRange(projects);
                        
                        // 如果返回的倉庫數量少於每頁數量，說明已經是最後一頁
                        if (repos.Count < perPage)
                        {
                            break;
                        }
                        
                        page++;
                        
                        // 避免請求過快
                        await Task.Delay(500);
                    }
                    else
                    {
                        return new ApiResponse<List<UserProject>>
                        {
                            Success = false,
                            ErrorMessage = $"HTTP {httpResponse.StatusCode}: {httpResponse.ReasonPhrase}"
                        };
                    }
                }

                return new ApiResponse<List<UserProject>>
                {
                    Success = true,
                    Data = allProjects
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<UserProject>>
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 獲取用戶在貢獻專案中排名前五的專案（優化版）
        /// </summary>
        private async Task<ApiResponse<List<UserProject>>> GetUserTopContributedRepositoriesAsync(string username)
        {
            try
            {
                var topContributedProjects = new List<UserProject>();
                
                // 1. 獲取用戶所屬的組織
                var orgsUrl = $"https://api.github.com/users/{username}/orgs";
                var orgsResponse = await _httpClient.GetAsync(orgsUrl);

                if (orgsResponse.StatusCode == HttpStatusCode.Forbidden)
                {
                    return new ApiResponse<List<UserProject>>
                    {
                        Success = false,
                        IsRateLimited = true,
                        ErrorMessage = ApiLimitMessage
                    };
                }

                if (!orgsResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"   📄 {username} 沒有加入任何組織");
                    return new ApiResponse<List<UserProject>>
                    {
                        Success = true,
                        Data = new List<UserProject>()
                    };
                }

                var orgsJson = await orgsResponse.Content.ReadAsStringAsync();
                var organizations = JsonSerializer.Deserialize<List<GitHubOrganization>>(orgsJson);

                if (organizations != null && organizations.Count > 0)
                {
                    Console.WriteLine($"   🏢 檢查 {Math.Min(organizations.Count, 20)} 個組織的貢獻專案...");
                    
                    // 增加檢查的組織數量到20個，並增加更好的錯誤處理
                    foreach (var org in organizations.Take(20))
                    {
                        try
                        {
                            Console.WriteLine($"     🔍 檢查組織: {org.Login}");
                            var orgTopRepos = await GetOrganizationTopRepositoriesWithContributorCheckAsync(org.Login, username);
                            
                            if (orgTopRepos.Success && orgTopRepos.Data != null && orgTopRepos.Data.Count > 0)
                            {
                                topContributedProjects.AddRange(orgTopRepos.Data);
                                Console.WriteLine($"       ✅ 在 {org.Login} 找到 {orgTopRepos.Data.Count} 個前五貢獻專案");
                            }
                            else if (orgTopRepos.IsRateLimited)
                            {
                                Console.WriteLine($"       🚫 檢查 {org.Login} 時遇到 API 限制");
                                return new ApiResponse<List<UserProject>>
                                {
                                    Success = false,
                                    IsRateLimited = true,
                                    ErrorMessage = ApiLimitMessage
                                };
                            }
                            else
                            {
                                Console.WriteLine($"       📄 在 {org.Login} 中未找到前五貢獻專案");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"       ❌ 檢查組織 {org.Login} 時發生錯誤: {ex.Message}");
                            continue; // 繼續檢查下一個組織
                        }

                        await Task.Delay(800); // 組織間延遲
                    }
                }
                else
                {
                    Console.WriteLine($"   📄 {username} 沒有加入任何組織");
                }

                // 按stars排序並返回最好的貢獻專案
                var result = topContributedProjects
                    .GroupBy(p => p.FullName) // 去重
                    .Select(g => g.First())
                    .OrderByDescending(p => p.StargazersCount)
                    .Take(8) // 增加數量到8個
                    .ToList();

                return new ApiResponse<List<UserProject>>
                {
                    Success = true,
                    Data = result
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<UserProject>>
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 獲取組織的頂級倉庫並檢查用戶是否在前五貢獻者中（優化版）
        /// </summary>
        private async Task<ApiResponse<List<UserProject>>> GetOrganizationTopRepositoriesWithContributorCheckAsync(string orgName, string username)
        {
            try
            {
                var reposUrl = $"https://api.github.com/orgs/{orgName}/repos?per_page=20&sort=stars&direction=desc";
                var response = await _httpClient.GetAsync(reposUrl);

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    return new ApiResponse<List<UserProject>>
                    {
                        Success = false,
                        IsRateLimited = true,
                        ErrorMessage = ApiLimitMessage
                    };
                }

                if (!response.IsSuccessStatusCode)
                {
                    return new ApiResponse<List<UserProject>>
                    {
                        Success = false,
                        ErrorMessage = $"HTTP {response.StatusCode}: {response.ReasonPhrase}"
                    };
                }

                var json = await response.Content.ReadAsStringAsync();
                var repos = JsonSerializer.Deserialize<List<GitHubRepository>>(json);

                var userTopProjects = new List<UserProject>();

                if (repos != null)
                {
                    var eligibleRepos = repos
                        .Where(r => r.StargazersCount > 0 ) // 排除fork的倉庫
                        .ToList();

                    Console.WriteLine($"       📊 {orgName} 有 {eligibleRepos.Count} 個符合條件的倉庫");

                    foreach (var repo in eligibleRepos)
                    {
                        try
                        {
                            // 檢查用戶是否在前五貢獻者中
                            var isTopContributor = await IsUserTopContributorAsync(repo.FullName, username);
                            
                            if (isTopContributor.Success && isTopContributor.Data)
                            {
                                var project = new UserProject
                                {
                                    Name = repo.Name,
                                    FullName = repo.FullName,
                                    Description = repo.Description,
                                    StargazersCount = repo.StargazersCount,
                                    ForksCount = repo.ForksCount,
                                    Language = repo.Language,
                                    IsOwner = false,
                                    Organization = orgName,
                                    CreatedAt = repo.CreatedAt,
                                    UpdatedAt = repo.UpdatedAt
                                };
                                
                                userTopProjects.Add(project);
                                Console.WriteLine($"         🏆 前五貢獻者: {repo.Name} ({repo.StargazersCount:N0} stars)");
                            }
                            
                            if (isTopContributor.IsRateLimited)
                            {
                                return new ApiResponse<List<UserProject>>
                                {
                                    Success = false,
                                    IsRateLimited = true,
                                    ErrorMessage = ApiLimitMessage
                                };
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"         ❌ 檢查 {repo.Name} 時發生錯誤: {ex.Message}");
                            continue; // 繼續檢查下一個倉庫
                        }
                        
                        await Task.Delay(700); // 倉庫間延遲
                    }
                }

                return new ApiResponse<List<UserProject>>
                {
                    Success = true,
                    Data = userTopProjects
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<UserProject>>
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 檢查用戶是否在特定倉庫的前五貢獻者中（優化版）
        /// </summary>
        private async Task<ApiResponse<bool>> IsUserTopContributorAsync(string repoFullName, string username)
        {
            try
            {
                var contributorsUrl = $"https://api.github.com/repos/{repoFullName}/contributors?per_page=5";
                var response = await _httpClient.GetAsync(contributorsUrl);

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    // 可能是私有倉庫或API限制
                    return new ApiResponse<bool>
                    {
                        Success = false,
                        IsRateLimited = true,
                        ErrorMessage = ApiLimitMessage
                    };
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    // 倉庫不存在或無權限訪問
                    return new ApiResponse<bool>
                    {
                        Success = true,
                        Data = false
                    };
                }

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var contributors = JsonSerializer.Deserialize<List<GitHubUser>>(json);

                    if (contributors != null && contributors.Count > 0)
                    {
                        var isTopContributor = contributors.Any(c => 
                            c.Login.Equals(username, StringComparison.OrdinalIgnoreCase));

                        if (isTopContributor)
                        {
                            var userRank = contributors.FindIndex(c => 
                                c.Login.Equals(username, StringComparison.OrdinalIgnoreCase)) + 1;
                            Console.WriteLine($"           🎯 {username} 在 {repoFullName} 排名第 {userRank}");
                        }

                        return new ApiResponse<bool>
                        {
                            Success = true,
                            Data = isTopContributor
                        };
                    }
                }

                return new ApiResponse<bool>
                {
                    Success = false,
                    ErrorMessage = $"HTTP {response.StatusCode}: {response.ReasonPhrase}"
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public void Dispose()
        {
            Dispose(true);
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
