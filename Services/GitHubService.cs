using System.Text.Json;
using System.Net;
using TaiwanGitHubPopularUsers.Models;

namespace TaiwanGitHubPopularUsers.Services
{
    public class GitHubService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _token;
        private readonly ProgressService _progressService;

        // 台灣相關的地區關鍵字
        private readonly List<string> _taiwanLocations = new()
        {
            "Taiwan", 
            "Taipei", 
            "New Taipei",
            "Taoyuan", 
            "Taichung",
            "Tainan", 
            "Kaohsiung",
            "Hsinchu", 
            "Keelung", 
            "Chiayi", 
            "Changhua", 
            "Yunlin", 
            "Nantou", 
            "Pingtung", 
            "Yilan", 
            "Hualien", 
            "Taitung", 
            "Penghu", 
            "Kinmen", 
            "Matsu", 
        };

        public GitHubService(string token, ProgressService progressService)
        {
            _token = token;
            _progressService = progressService;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Taiwan-GitHub-Popular-Users");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"token {token}");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        }

        public async Task<ApiResponse<List<GitHubUser>>> SearchTaiwanUsersAsync(RunProgress progress)
        {
            var allUsers = new List<GitHubUser>();
            var remainingLocations = _progressService.GetRemainingLocations(progress, _taiwanLocations);

            Console.WriteLine($"🔍 開始搜尋剩餘的 {remainingLocations.Count} 個地區");
            Console.WriteLine($"📊 本次運行 API 請求限制: {progress.MaxApiRequestsPerRun}");

            foreach (var location in remainingLocations)
            {
                // 檢查 API 請求次數限制
                if (progress.ApiRequestCount >= progress.MaxApiRequestsPerRun)
                {
                    await _progressService.MarkApiLimitReachedAsync(progress);
                    return new ApiResponse<List<GitHubUser>>
                    {
                        Success = true,
                        Data = allUsers.OrderByDescending(u => u.Followers).ToList(),
                        ErrorMessage = $"已達到本次運行的 API 請求限制 ({progress.MaxApiRequestsPerRun})"
                    };
                }

                // 檢查是否遇到 GitHub API 限制
                if (progress.EncounteredRateLimit && progress.RateLimitResetTime.HasValue)
                {
                    if (DateTime.UtcNow < progress.RateLimitResetTime.Value)
                    {
                        return new ApiResponse<List<GitHubUser>>
                        {
                            Success = false,
                            IsRateLimited = true,
                            ErrorMessage = "GitHub API 限制尚未重置",
                            RateLimitResetTime = progress.RateLimitResetTime,
                            Data = allUsers
                        };
                    }
                    else
                    {
                        // GitHub API 限制已重置
                        progress.EncounteredRateLimit = false;
                        progress.RateLimitResetTime = null;
                        await _progressService.SaveProgressAsync(progress);
                    }
                }

                try
                {
                    // 檢查是否還可以發送 API 請求
                    if (!_progressService.IncrementApiRequestCount(progress))
                    {
                        await _progressService.MarkApiLimitReachedAsync(progress);
                        return new ApiResponse<List<GitHubUser>>
                        {
                            Success = true,
                            Data = allUsers.OrderByDescending(u => u.Followers).ToList(),
                            ErrorMessage = $"已達到本次運行的 API 請求限制 ({progress.MaxApiRequestsPerRun})"
                        };
                    }

                    var searchResult = await SearchLocationAsync(location);
                    
                    if (!searchResult.Success)
                    {
                        if (searchResult.IsRateLimited)
                        {
                            await _progressService.MarkRateLimitEncounteredAsync(progress, searchResult.RateLimitResetTime);
                            return new ApiResponse<List<GitHubUser>>
                            {
                                Success = false,
                                IsRateLimited = true,
                                ErrorMessage = "遇到 GitHub API 限制，已保存進度",
                                RateLimitResetTime = searchResult.RateLimitResetTime,
                                Data = allUsers
                            };
                        }
                        else
                        {
                            await _progressService.MarkLocationFailedAsync(progress, location, searchResult.ErrorMessage ?? "未知錯誤");
                            continue;
                        }
                    }

                    // ⚠️ 重要：搜索 API 返回的 followers 通常為 0，需要獲取詳細信息
                    var searchedUsers = searchResult.Data ?? new List<GitHubUser>();
                    var totalUsersFound = searchedUsers.Count;
                    Console.WriteLine($"   📊 地區 '{location}' 搜尋結果: 總共 {totalUsersFound} 位用戶");
                    Console.WriteLine($"   📥 正在獲取用戶詳細信息（包含真實的 followers 數量）...");
                    
                    // 獲取用戶詳細信息
                    var locationUsers = new List<GitHubUser>();
                    foreach (var searchUser in searchedUsers.Take(30)) // 限制每個地區最多處理 30 個用戶
                    {
                        // 檢查 API 請求次數限制
                        if (progress.ApiRequestCount >= progress.MaxApiRequestsPerRun)
                        {
                            Console.WriteLine($"   ⚠️  達到 API 請求限制，停止處理地區 '{location}'");
                            await _progressService.MarkApiLimitReachedAsync(progress);
                            return new ApiResponse<List<GitHubUser>>
                            {
                                Success = true,
                                Data = allUsers.OrderByDescending(u => u.Followers).ToList(),
                                ErrorMessage = $"已達到本次運行的 API 請求限制 ({progress.MaxApiRequestsPerRun})"
                            };
                        }

                        // 檢查是否還可以發送 API 請求
                        if (!_progressService.IncrementApiRequestCount(progress))
                        {
                            await _progressService.MarkApiLimitReachedAsync(progress);
                            return new ApiResponse<List<GitHubUser>>
                            {
                                Success = true,
                                Data = allUsers.OrderByDescending(u => u.Followers).ToList(),
                                ErrorMessage = $"已達到本次運行的 API 請求限制 ({progress.MaxApiRequestsPerRun})"
                            };
                        }

                        var detailResult = await GetUserDetailsWithProjectsAsync(searchUser.Login);
                        
                        if (!detailResult.Success)
                        {
                            if (detailResult.IsRateLimited)
                            {
                                await _progressService.MarkRateLimitEncounteredAsync(progress, detailResult.RateLimitResetTime);
                                return new ApiResponse<List<GitHubUser>>
                                {
                                    Success = false,
                                    IsRateLimited = true,
                                    ErrorMessage = "遇到 GitHub API 限制，已保存進度",
                                    RateLimitResetTime = detailResult.RateLimitResetTime,
                                    Data = allUsers
                                };
                            }
                            continue;
                        }

                        if (detailResult.Data != null)
                        {
                            // 檢查真實的 followers 數量
                            if (detailResult.Data.Followers >= 10 && !allUsers.Any(u => u.Id == detailResult.Data.Id))
                            {
                                locationUsers.Add(detailResult.Data);
                                allUsers.Add(detailResult.Data);
                                Console.WriteLine($"   ✅ {detailResult.Data.Login}: {detailResult.Data.Followers} followers, {detailResult.Data.TotalStars} stars, {detailResult.Data.TotalForks} forks (已包含專案信息)");
                            }
                            else if (detailResult.Data.Followers < 10)
                            {
                                Console.WriteLine($"   ℹ️  {detailResult.Data.Login}: {detailResult.Data.Followers} followers (不符合條件)");
                                break;
                            }
                        }

                        // 避免 API 限制
                        await Task.Delay(500);
                    }
                    
                    await _progressService.MarkLocationCompletedAsync(progress, location, locationUsers.Count);
                    
                    // 地區間延遲
                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    await _progressService.MarkLocationFailedAsync(progress, location, ex.Message);
                }
            }

            // 如果沒有達到 API 限制且完成了所有地區，標記為完成
            if (progress.ApiRequestCount < progress.MaxApiRequestsPerRun)
            {
                await _progressService.MarkCompletedAsync(progress);
            }

            return new ApiResponse<List<GitHubUser>>
            {
                Success = true,
                Data = allUsers.OrderByDescending(u => u.Followers).ToList()
            };
        }

        private async Task<ApiResponse<List<GitHubUser>>> SearchLocationAsync(string location)
        {
            try
            {
                var searchUrl = $"https://api.github.com/search/users?q=location:{Uri.EscapeDataString(location)}&per_page=100&sort=followers&order=desc";
                
                Console.WriteLine($"🔍 正在搜尋地區: {location}");
                Console.WriteLine($"   🌐 搜尋 URL: {searchUrl}");
                
                var response = await _httpClient.GetAsync(searchUrl);
                
                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    var rateLimitResetHeader = response.Headers.GetValues("X-RateLimit-Reset").FirstOrDefault();
                    DateTime? resetTime = null;
                    
                    if (long.TryParse(rateLimitResetHeader, out long resetUnixTime))
                    {
                        resetTime = DateTimeOffset.FromUnixTimeSeconds(resetUnixTime).UtcDateTime;
                    }

                    return new ApiResponse<List<GitHubUser>>
                    {
                        Success = false,
                        IsRateLimited = true,
                        ErrorMessage = "API 限制已達到 (403 rate limit exceeded)",
                        RateLimitResetTime = resetTime
                    };
                }
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var searchResult = JsonSerializer.Deserialize<GitHubSearchResponse>(json);
                    
                    Console.WriteLine($"   ✅ API 響應成功，解析到 {searchResult?.Items?.Count ?? 0} 位用戶");
                    
                    return new ApiResponse<List<GitHubUser>>
                    {
                        Success = true,
                        Data = searchResult?.Items ?? new List<GitHubUser>()
                    };
                }
                else
                {
                    Console.WriteLine($"   ❌ API 響應失敗: {response.StatusCode} - {response.ReasonPhrase}");
                    return new ApiResponse<List<GitHubUser>>
                    {
                        Success = false,
                        ErrorMessage = $"HTTP {response.StatusCode}: {response.ReasonPhrase}"
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ 搜尋地區 '{location}' 時發生例外: {ex.Message}");
                return new ApiResponse<List<GitHubUser>>
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 獲取用戶詳細信息並同時處理專案信息
        /// </summary>
        private async Task<ApiResponse<GitHubUser>> GetUserDetailsWithProjectsAsync(string username)
        {
            try
            {
                // 1. 獲取基本用戶信息
                var userUrl = $"https://api.github.com/users/{username}";
                var response = await _httpClient.GetAsync(userUrl);
                
                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    var rateLimitResetHeader = response.Headers.GetValues("X-RateLimit-Reset").FirstOrDefault();
                    DateTime? resetTime = null;
                    
                    if (long.TryParse(rateLimitResetHeader, out long resetUnixTime))
                    {
                        resetTime = DateTimeOffset.FromUnixTimeSeconds(resetUnixTime).UtcDateTime;
                    }

                    return new ApiResponse<GitHubUser>
                    {
                        Success = false,
                        IsRateLimited = true,
                        ErrorMessage = "API 限制已達到 (403 rate limit exceeded)",
                        RateLimitResetTime = resetTime
                    };
                }
                
                if (!response.IsSuccessStatusCode)
                {
                    return new ApiResponse<GitHubUser>
                    {
                        Success = false,
                        ErrorMessage = $"HTTP {response.StatusCode}: {response.ReasonPhrase}"
                    };
                }

                var json = await response.Content.ReadAsStringAsync();
                var user = JsonSerializer.Deserialize<GitHubUser>(json);
                
                if (user == null)
                {
                    return new ApiResponse<GitHubUser>
                    {
                        Success = false,
                        ErrorMessage = "無法解析用戶信息"
                    };
                }

                user.LastFetched = DateTime.UtcNow;

                // 2. 同時獲取用戶的專案信息
                Console.WriteLine($"      📂 正在獲取 {username} 的專案信息...");
                
                // 獲取個人倉庫（限制前50個，按stars排序）
                var personalProjects = await GetUserPersonalProjectsAsync(username);
                if (personalProjects.IsRateLimited)
                {
                    return new ApiResponse<GitHubUser>
                    {
                        Success = false,
                        IsRateLimited = true,
                        ErrorMessage = "獲取專案信息時遇到 API 限制"
                    };
                }

                // 獲取貢獻專案（限制檢查前3個組織）
                var contributedProjects = await GetUserContributedProjectsAsync(username);
                if (contributedProjects.IsRateLimited)
                {
                    return new ApiResponse<GitHubUser>
                    {
                        Success = false,
                        IsRateLimited = true,
                        ErrorMessage = "獲取貢獻專案信息時遇到 API 限制"
                    };
                }

                // 3. 合併所有專案信息
                var allProjects = new List<UserProject>();
                
                if (personalProjects.Success && personalProjects.Data != null)
                {
                    allProjects.AddRange(personalProjects.Data);
                }
                
                if (contributedProjects.Success && contributedProjects.Data != null)
                {
                    allProjects.AddRange(contributedProjects.Data);
                }

                // 按 Stars 排序並取前5名作為展示
                user.Projects = allProjects
                    .OrderByDescending(p => p.StargazersCount)
                    .Take(5)
                    .ToList();

                // 計算總計
                user.TotalStars = allProjects.Sum(p => p.StargazersCount);
                user.TotalForks = allProjects.Sum(p => p.ForksCount);

                Console.WriteLine($"      ✅ {username}: {user.Projects.Count} 個展示專案，總計 {user.TotalStars} stars, {user.TotalForks} forks");

                return new ApiResponse<GitHubUser>
                {
                    Success = true,
                    Data = user
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<GitHubUser>
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 獲取用戶個人專案（簡化版）
        /// </summary>
        private async Task<ApiResponse<List<UserProject>>> GetUserPersonalProjectsAsync(string username)
        {
            try
            {
                var url = $"https://api.github.com/users/{username}/repos?per_page=50&sort=stars&direction=desc";
                var response = await _httpClient.GetAsync(url);

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

                    var projects = repos?.Where(r => !r.Name.StartsWith('.') && r.StargazersCount >= 0)
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

        /// <summary>
        /// 獲取用戶貢獻專案（簡化版）
        /// </summary>
        private async Task<ApiResponse<List<UserProject>>> GetUserContributedProjectsAsync(string username)
        {
            try
            {
                var contributedProjects = new List<UserProject>();
                
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

                if (organizations != null && organizations.Count > 0)
                {
                    // 限制只檢查前3個組織以減少API請求
                    foreach (var org in organizations.Take(3))
                    {
                        var orgRepos = await GetOrganizationTopRepositoriesAsync(org.Login);
                        
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
                        else if (orgRepos.IsRateLimited)
                        {
                            return new ApiResponse<List<UserProject>>
                            {
                                Success = false,
                                IsRateLimited = true,
                                ErrorMessage = "API 限制已達到"
                            };
                        }

                        await Task.Delay(300); // 避免 API 限制
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

        /// <summary>
        /// 獲取組織的頂級倉庫（簡化版）
        /// </summary>
        private async Task<ApiResponse<List<UserProject>>> GetOrganizationTopRepositoriesAsync(string orgName)
        {
            try
            {
                var reposUrl = $"https://api.github.com/orgs/{orgName}/repos?per_page=10&sort=stars&direction=desc";
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
