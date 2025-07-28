using System.Text.Json;
using System.Net;
using TaiwanGitHubPopularUsers.Models;

namespace TaiwanGitHubPopularUsers.Services
{
    public class OrganizationService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _token;

        public OrganizationService(string token)
        {
            _token = token;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Taiwan-GitHub-Popular-Users");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"token {token}");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        }

        public async Task<List<GitHubOrganization>> SearchTaiwanOrganizationsAsync(int maxResults = 50)
        {
            var organizations = new List<GitHubOrganization>();
            var taiwanLocations = new[]
            {
                "Taiwan", "台灣", "臺灣", "Taipei", "台北", "臺北",
                "Taichung", "台中", "臺中", "Kaohsiung", "高雄",
                "Tainan", "台南", "臺南", "Hsinchu", "新竹"
            };

            foreach (var location in taiwanLocations)
            {
                if (organizations.Count >= maxResults) break;

                try
                {
                    Console.WriteLine($"🏢 搜尋組織地區: {location}");
                    var searchUrl = $"https://api.github.com/search/users?q=location:{Uri.EscapeDataString(location)}+type:org&per_page=30&sort=followers&order=desc";
                    
                    var response = await _httpClient.GetAsync(searchUrl);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var searchResult = JsonSerializer.Deserialize<GitHubSearchResponse>(json);
                        
                        if (searchResult?.Items != null)
                        {
                            foreach (var orgUser in searchResult.Items.Take(10))
                            {
                                if (organizations.Count >= maxResults) break;
                                
                                var org = await GetOrganizationDetailsAsync(orgUser.Login);
                                if (org != null && !organizations.Any(o => o.Id == org.Id))
                                {
                                    // 獲取組織的倉庫統計
                                    await GetOrganizationRepositoryStatsAsync(org);
                                    organizations.Add(org);
                                    
                                    Console.WriteLine($"   ✅ {org.Login}: {org.Followers} followers, {org.TotalStars} stars");
                                }
                                
                                await Task.Delay(1000);
                            }
                        }
                    }
                    
                    await Task.Delay(2000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ 搜尋組織 {location} 時發生錯誤: {ex.Message}");
                }
            }

            return organizations.OrderByDescending(o => o.TotalStars + o.Followers).ToList();
        }

        private async Task<GitHubOrganization?> GetOrganizationDetailsAsync(string orgName)
        {
            try
            {
                var orgUrl = $"https://api.github.com/orgs/{orgName}";
                var response = await _httpClient.GetAsync(orgUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var org = JsonSerializer.Deserialize<GitHubOrganization>(json);
                    if (org != null)
                    {
                        org.LastFetched = DateTime.UtcNow;
                    }
                    return org;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ 獲取組織 {orgName} 詳細信息時發生錯誤: {ex.Message}");
            }
            
            return null;
        }

        private async Task GetOrganizationRepositoryStatsAsync(GitHubOrganization org)
        {
            try
            {
                var reposUrl = $"https://api.github.com/orgs/{org.Login}/repos?per_page=100&sort=stars&direction=desc";
                var response = await _httpClient.GetAsync(reposUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var repos = JsonSerializer.Deserialize<List<GitHubRepository>>(json);
                    
                    if (repos != null)
                    {
                        org.TotalStars = repos.Sum(r => r.StargazersCount);
                        org.TotalForks = repos.Sum(r => r.ForksCount);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️  獲取組織 {org.Login} 倉庫統計時發生錯誤: {ex.Message}");
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
