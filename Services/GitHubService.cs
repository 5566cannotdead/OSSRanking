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

                        var detailResult = await GetUserDetailsAsync(searchUser.Login);
                        
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
                                Console.WriteLine($"   ✅ {detailResult.Data.Login}: {detailResult.Data.Followers} followers (符合條件)");
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

        private async Task<ApiResponse<GitHubUser>> GetUserDetailsAsync(string username)
        {
            try
            {
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
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var user = JsonSerializer.Deserialize<GitHubUser>(json);
                    
                    if (user != null)
                    {
                        user.LastFetched = DateTime.UtcNow;
                        return new ApiResponse<GitHubUser>
                        {
                            Success = true,
                            Data = user
                        };
                    }
                }
                
                return new ApiResponse<GitHubUser>
                {
                    Success = false,
                    ErrorMessage = $"HTTP {response.StatusCode}: {response.ReasonPhrase}"
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
