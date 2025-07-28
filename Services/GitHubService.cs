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
            "Taiwan", "台灣", "臺灣",
            "Taipei", "台北", "臺北",
            "New Taipei", "新北",
            "Taoyuan", "桃園",
            "Taichung", "台中", "臺中",
            "Tainan", "台南", "臺南",
            "Kaohsiung", "高雄",
            "Hsinchu", "新竹",
            "Keelung", "基隆",
            "Chiayi", "嘉義",
            "Changhua", "彰化",
            "Yunlin", "雲林",
            "Nantou", "南投",
            "Pingtung", "屏東",
            "Yilan", "宜蘭",
            "Hualien", "花蓮",
            "Taitung", "台東", "臺東",
            "Penghu", "澎湖",
            "Kinmen", "金門",
            "Matsu", "馬祖"
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

            foreach (var location in remainingLocations)
            {
                // 檢查是否遇到 API 限制
                if (progress.EncounteredRateLimit && progress.RateLimitResetTime.HasValue)
                {
                    if (DateTime.UtcNow < progress.RateLimitResetTime.Value)
                    {
                        return new ApiResponse<List<GitHubUser>>
                        {
                            Success = false,
                            IsRateLimited = true,
                            ErrorMessage = "API 限制尚未重置",
                            RateLimitResetTime = progress.RateLimitResetTime,
                            Data = allUsers
                        };
                    }
                    else
                    {
                        // API 限制已重置
                        progress.EncounteredRateLimit = false;
                        progress.RateLimitResetTime = null;
                        await _progressService.SaveProgressAsync(progress);
                    }
                }

                try
                {
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
                                ErrorMessage = "遇到 API 限制，已保存進度",
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

                    var qualifiedUsers = searchResult.Data?.Where(u => u.Followers >= 100).ToList() ?? new List<GitHubUser>();
                    
                    // 獲取用戶詳細信息
                    var locationUsers = new List<GitHubUser>();
                    foreach (var user in qualifiedUsers)
                    {
                        var detailResult = await GetUserDetailsAsync(user.Login);
                        
                        if (!detailResult.Success)
                        {
                            if (detailResult.IsRateLimited)
                            {
                                await _progressService.MarkRateLimitEncounteredAsync(progress, detailResult.RateLimitResetTime);
                                return new ApiResponse<List<GitHubUser>>
                                {
                                    Success = false,
                                    IsRateLimited = true,
                                    ErrorMessage = "遇到 API 限制，已保存進度",
                                    RateLimitResetTime = detailResult.RateLimitResetTime,
                                    Data = allUsers
                                };
                            }
                            continue;
                        }

                        if (detailResult.Data != null && !allUsers.Any(u => u.Id == detailResult.Data.Id))
                        {
                            locationUsers.Add(detailResult.Data);
                            allUsers.Add(detailResult.Data);
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

            // 標記完成
            await _progressService.MarkCompletedAsync(progress);

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
                    
                    return new ApiResponse<List<GitHubUser>>
                    {
                        Success = true,
                        Data = searchResult?.Items ?? new List<GitHubUser>()
                    };
                }
                else
                {
                    return new ApiResponse<List<GitHubUser>>
                    {
                        Success = false,
                        ErrorMessage = $"HTTP {response.StatusCode}: {response.ReasonPhrase}"
                    };
                }
            }
            catch (Exception ex)
            {
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
