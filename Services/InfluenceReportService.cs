using System.Text.Json;
using TaiwanGitHubPopularUsers.Models;

namespace TaiwanGitHubPopularUsers.Services
{
    public class InfluenceReportService
    {
        private readonly JsonSerializerOptions _jsonOptions;

        public InfluenceReportService()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public TaiwanInfluenceReport GenerateInfluenceReport(
            List<GitHubUser> users, 
            List<GitHubOrganization> organizations)
        {
            var report = new TaiwanInfluenceReport
            {
                TopDevelopers = users.OrderByDescending(u => u.Followers).Take(50).ToList(),
                TopOrganizations = organizations.OrderByDescending(o => o.TotalStars + o.Followers).Take(20).ToList(),
                Statistics = GenerateStatistics(users, organizations),
                GeneratedAt = DateTime.UtcNow
            };

            return report;
        }

        private TaiwanStatistics GenerateStatistics(
            List<GitHubUser> users, 
            List<GitHubOrganization> organizations)
        {
            var stats = new TaiwanStatistics
            {
                TotalDevelopers = users.Count,
                TotalOrganizations = organizations.Count,
                TotalFollowers = users.Sum(u => u.Followers) + organizations.Sum(o => o.Followers),
                TotalPublicRepos = users.Sum(u => u.PublicRepos) + organizations.Sum(o => o.PublicRepos),
                TotalStars = organizations.Sum(o => o.TotalStars),
                TotalForks = organizations.Sum(o => o.TotalForks)
            };

            // 地區分佈
            var allLocations = users.Where(u => !string.IsNullOrEmpty(u.Location))
                                   .Select(u => u.Location!)
                                   .Concat(organizations.Where(o => !string.IsNullOrEmpty(o.Location))
                                                      .Select(o => o.Location!));

            stats.LocationDistribution = allLocations
                .GroupBy(loc => loc, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count());

            return stats;
        }

        public async Task SaveReportAsync(TaiwanInfluenceReport report, string filePath = "taiwan_github_influence_report.json")
        {
            try
            {
                var json = JsonSerializer.Serialize(report, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json);
                Console.WriteLine($"📊 影響力報告已保存到: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 保存影響力報告時發生錯誤: {ex.Message}");
            }
        }

        public void PrintInfluenceReport(TaiwanInfluenceReport report)
        {
            Console.WriteLine("\n" + "=".PadRight(80, '='));
            Console.WriteLine("🇹🇼 台灣 GitHub 影響力報告");
            Console.WriteLine("=".PadRight(80, '='));
            
            PrintStatistics(report.Statistics);
            PrintTopDevelopers(report.TopDevelopers);
            PrintTopOrganizations(report.TopOrganizations);
            
            Console.WriteLine($"\n📅 報告生成時間: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine("=".PadRight(80, '='));
        }

        private void PrintStatistics(TaiwanStatistics stats)
        {
            Console.WriteLine("\n📈 整體統計");
            Console.WriteLine($"總開發者數量: {stats.TotalDevelopers:N0}");
            Console.WriteLine($"總組織數量: {stats.TotalOrganizations:N0}");
            Console.WriteLine($"總追蹤者數: {stats.TotalFollowers:N0}");
            Console.WriteLine($"總公開倉庫: {stats.TotalPublicRepos:N0}");
            Console.WriteLine($"總 Stars 數: {stats.TotalStars:N0}");
            Console.WriteLine($"總 Forks 數: {stats.TotalForks:N0}");

            if (stats.LocationDistribution.Count > 0)
            {
                Console.WriteLine("\n📍 地區分佈 (前 10 名):");
                var topLocations = stats.LocationDistribution
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(10);

                foreach (var location in topLocations)
                {
                    Console.WriteLine($"  {location.Key}: {location.Value} 位");
                }
            }
        }

        private void PrintTopDevelopers(List<GitHubUser> developers)
        {
            Console.WriteLine("\n👥 頂尖開發者 (前 20 名 - 按追蹤者排序)");
            Console.WriteLine("排名\t用戶名\t\t追蹤者\t倉庫數\t地區");
            Console.WriteLine("====\t======\t\t======\t======\t====");

            for (int i = 0; i < Math.Min(20, developers.Count); i++)
            {
                var dev = developers[i];
                var location = string.IsNullOrEmpty(dev.Location) ? "未知" : dev.Location;
                Console.WriteLine($"{i + 1,2}\t{dev.Login,-15}\t{dev.Followers,6}\t{dev.PublicRepos,6}\t{location}");
            }
        }

        private void PrintTopOrganizations(List<GitHubOrganization> organizations)
        {
            Console.WriteLine("\n🏢 頂尖組織 (前 10 名 - 按 Stars + 追蹤者排序)");
            Console.WriteLine("排名\t組織名\t\t追蹤者\tStars\tForks\t地區");
            Console.WriteLine("====\t======\t\t======\t=====\t=====\t====");

            for (int i = 0; i < Math.Min(10, organizations.Count); i++)
            {
                var org = organizations[i];
                var location = string.IsNullOrEmpty(org.Location) ? "未知" : org.Location;
                var influence = org.TotalStars + org.Followers;
                Console.WriteLine($"{i + 1,2}\t{org.Login,-15}\t{org.Followers,6}\t{org.TotalStars,5}\t{org.TotalForks,5}\t{location}");
            }
        }

        public async Task<TaiwanInfluenceReport?> LoadReportAsync(string filePath = "taiwan_github_influence_report.json")
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return null;
                }

                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<TaiwanInfluenceReport>(json, _jsonOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 載入影響力報告時發生錯誤: {ex.Message}");
                return null;
            }
        }
    }
}
