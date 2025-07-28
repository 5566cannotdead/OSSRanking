using System.Text.Json;
using System.Net;
using TaiwanGitHubPopularUsers.Models;

namespace TaiwanGitHubPopularUsers.Services
{
    public class UserProjectService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _token;

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

                // 1. 獲取用戶自己的倉庫（按 stars 排序）
                var ownRepos = await GetUserRepositoriesAsync(user.Login);
                
                // 如果遇到 API 限制，立即返回
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
                
                // 2. 獲取用戶貢獻的組織倉庫
                var contributedRepos = await GetUserContributedRepositoriesAsync(user.Login);
                
                // 如果遇到 API 限制，立即返回
                if (!contributedRepos.Success && contributedRepos.IsRateLimited)
                {
                    Console.WriteLine($"   🚫 獲取 {user.Login} 的組織貢獻時遇到 API 限制，停止後續處理");
                    return new ApiResponse<bool>
                    {
                        Success = false,
                        IsRateLimited = true,
                        ErrorMessage = "API 限制已達到，停止處理後續用戶"
                    };
                }

                // 3. 合併並選擇前三個最有影響力的專案
                var allProjects = new List<UserProject>();
                
                if (ownRepos.Success && ownRepos.Data != null)
                {
                    allProjects.AddRange(ownRepos.Data);
                }
                
                if (contributedRepos.Success && contributedRepos.Data != null)
                {
                    allProjects.AddRange(contributedRepos.Data);
                }

                // 按 Stars 排序並取前三名
                user.Projects = allProjects
                    .OrderByDescending(p => p.StargazersCount)
                    .Take(3)
                    .ToList();

                // 計算總計
                user.TotalStars = user.Projects.Sum(p => p.StargazersCount);
                user.TotalForks = user.Projects.Sum(p => p.ForksCount);

                Console.WriteLine($"   ✅ {user.Login}: 找到 {user.Projects.Count} 個主要專案，總計 {user.TotalStars} stars, {user.TotalForks} forks");

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

        private async Task<ApiResponse<List<UserProject>>> GetUserRepositoriesAsync(string username)
        {
            try
            {
                var reposUrl = $"https://api.github.com/users/{username}/repos?per_page=50&sort=stars&direction=desc";
                var response = await _httpClient.GetAsync(reposUrl);

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    return new ApiResponse<List<UserProject>>
                    {
                        Success = false,
                        IsRateLimited = true,
                        ErrorMessage = "API 限制已達到"
                    };
                }

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var repos = JsonSerializer.Deserialize<List<GitHubRepository>>(json);

                    var projects = repos?.Where(r => !r.Name.StartsWith(".") && r.StargazersCount > 0)
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
                        }).ToList() ?? new List<UserProject>();

                    return new ApiResponse<List<UserProject>>
                    {
                        Success = true,
                        Data = projects
                    };
                }

                return new ApiResponse<List<UserProject>>
                {
                    Success = false,
                    ErrorMessage = $"HTTP {response.StatusCode}: {response.ReasonPhrase}"
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

        private async Task<ApiResponse<List<UserProject>>> GetUserContributedRepositoriesAsync(string username)
        {
            try
            {
                // 獲取用戶所屬的組織
                var orgsUrl = $"https://api.github.com/users/{username}/orgs";
                var orgsResponse = await _httpClient.GetAsync(orgsUrl);

                if (orgsResponse.StatusCode == HttpStatusCode.Forbidden)
                {
                    return new ApiResponse<List<UserProject>>
                    {
                        Success = false,
                        IsRateLimited = true,
                        ErrorMessage = "API 限制已達到"
                    };
                }

                if (!orgsResponse.IsSuccessStatusCode)
                {
                    return new ApiResponse<List<UserProject>>
                    {
                        Success = true,
                        Data = new List<UserProject>()
                    };
                }

                var orgsJson = await orgsResponse.Content.ReadAsStringAsync();
                var organizations = JsonSerializer.Deserialize<List<GitHubOrganization>>(orgsJson);

                var contributedProjects = new List<UserProject>();

                if (organizations != null)
                {
                    // 限制只檢查前 5 個組織以避免過多 API 請求
                    foreach (var org in organizations.Take(5))
                    {
                        var orgRepos = await GetOrganizationTopRepositoriesAsync(org.Login);
                        
                        // 如果遇到 API 限制，立即返回
                        if (!orgRepos.Success && orgRepos.IsRateLimited)
                        {
                            return new ApiResponse<List<UserProject>>
                            {
                                Success = false,
                                IsRateLimited = true,
                                ErrorMessage = "API 限制已達到"
                            };
                        }
                        
                        if (orgRepos.Success && orgRepos.Data != null)
                        {
                            // 為組織倉庫添加組織標記
                            foreach (var project in orgRepos.Data)
                            {
                                project.IsOwner = false;
                                project.Organization = org.Login;
                            }
                            
                            contributedProjects.AddRange(orgRepos.Data);
                        }

                        await Task.Delay(500); // 避免 API 限制
                    }
                }

                return new ApiResponse<List<UserProject>>
                {
                    Success = true,
                    Data = contributedProjects.OrderByDescending(p => p.StargazersCount).Take(10).ToList()
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

        private async Task<ApiResponse<List<UserProject>>> GetOrganizationTopRepositoriesAsync(string orgName)
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
                        ErrorMessage = "API 限制已達到"
                    };
                }

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var repos = JsonSerializer.Deserialize<List<GitHubRepository>>(json);

                    var projects = repos?.Where(r => r.StargazersCount > 50) // 只包含有一定影響力的專案
                        .Take(3) // 每個組織只取前 3 個
                        .Select(r => new UserProject
                        {
                            Name = r.Name,
                            FullName = r.FullName,
                            Description = r.Description,
                            StargazersCount = r.StargazersCount,
                            ForksCount = r.ForksCount,
                            Language = r.Language,
                            IsOwner = false,
                            Organization = orgName,
                            CreatedAt = r.CreatedAt,
                            UpdatedAt = r.UpdatedAt
                        }).ToList() ?? new List<UserProject>();

                    return new ApiResponse<List<UserProject>>
                    {
                        Success = true,
                        Data = projects
                    };
                }

                return new ApiResponse<List<UserProject>>
                {
                    Success = false,
                    ErrorMessage = $"HTTP {response.StatusCode}: {response.ReasonPhrase}"
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
