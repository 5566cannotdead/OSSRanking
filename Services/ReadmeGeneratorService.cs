using System.Text;
using TaiwanGitHubPopularUsers.Models;

namespace TaiwanGitHubPopularUsers.Services
{
    public class ReadmeGeneratorService
    {
        public async Task GenerateReadmeAsync(List<GitHubUser> users, string outputPath = "README.md")
        {
            try
            {
                Console.WriteLine("\n📄 === 生成 README.md ===");
                Console.WriteLine("正在為用戶數據生成 Markdown 表格...");

                // 過濾出有專案信息的用戶並按綜合影響力排序
                var usersWithProjects = users.Where(u => u.Projects != null && u.Projects.Count > 0).ToList();
                
                // 按照 followers + totalStars + totalForks 倒序排名
                var rankedUsers = usersWithProjects
                    .Select(u => new
                    {
                        User = u,
                        TotalInfluence = u.Followers + u.TotalStars + u.TotalForks
                    })
                    .OrderByDescending(x => x.TotalInfluence)
                    .Select(x => x.User)
                    .ToList();

                var sb = new StringBuilder();
                
                // 添加標題和說明
                sb.AppendLine("# 🇹🇼 台灣 GitHub 知名開發者排行榜");
                sb.AppendLine();
                sb.AppendLine("這個專案統計了台灣地區在 GitHub 上的知名開發者，包含個人專案和組織貢獻專案的影響力統計。");
                sb.AppendLine();
                sb.AppendLine($"## 📊 統計摘要");
                sb.AppendLine();
                sb.AppendLine($"- **總開發者數量**: {users.Count:N0} 位");
                sb.AppendLine($"- **有專案信息**: {usersWithProjects.Count:N0} 位");
                sb.AppendLine($"- **總 Stars**: {usersWithProjects.Sum(u => u.TotalStars):N0}");
                sb.AppendLine($"- **總 Forks**: {usersWithProjects.Sum(u => u.TotalForks):N0}");
                sb.AppendLine($"- **總 Followers**: {users.Sum(u => u.Followers):N0}");
                sb.AppendLine($"- **更新時間**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();
                
                // 添加排名說明
                sb.AppendLine("## 🏆 排名依據");
                sb.AppendLine();
                sb.AppendLine("排名按照 **Followers + Personal Stars + Personal Forks + Contributed Stars + Contributed Forks** 的總和進行倒序排列。");
                sb.AppendLine();
                sb.AppendLine("### 統計項目說明");
                sb.AppendLine("- **Personal Projects**: 用戶個人擁有的所有專案");
                sb.AppendLine("- **Top Contributed**: 用戶在組織專案中排名前三的貢獻專案");
                sb.AppendLine("- **Total Influence**: Followers + Personal Stars + Personal Forks + Contributed Stars + Contributed Forks");
                sb.AppendLine();

                // 添加表格標題
                sb.AppendLine("## 📋 開發者排行榜");
                sb.AppendLine();
                sb.AppendLine("| 排名 | 開發者 | Followers | Personal Projects | Top Contributed Projects | Total Influence |");
                sb.AppendLine("|------|--------|-----------|-------------------|--------------------------|-----------------|");

                // 生成表格內容
                for (int i = 0; i < rankedUsers.Count; i++)
                {
                    var user = rankedUsers[i];
                    var rank = i + 1;
                    
                    // 分離個人專案和貢獻專案
                    var personalProjects = user.Projects?.Where(p => p.IsOwner).ToList() ?? new List<UserProject>();
                    var contributedProjects = user.Projects?.Where(p => !p.IsOwner).ToList() ?? new List<UserProject>();
                    
                    var personalStars = personalProjects.Sum(p => p.StargazersCount);
                    var personalForks = personalProjects.Sum(p => p.ForksCount);
                    var contributedStars = contributedProjects.Sum(p => p.StargazersCount);
                    var contributedForks = contributedProjects.Sum(p => p.ForksCount);
                    
                    var totalInfluence = user.Followers + personalStars + personalForks + contributedStars + contributedForks;

                    // 用戶鏈接和頭像 - 統一小格式
                    var userAvatar = $"<img src=\"{user.AvatarUrl}&s=32\" width=\"32\" height=\"32\" style=\"border-radius: 50%;\" />";
                    var userLink = $"[{userAvatar}]({user.HtmlUrl})";
                    var userInfo = $"**[{user.Login}]({user.HtmlUrl})**";
                    if (!string.IsNullOrEmpty(user.Name))
                    {
                        userInfo += $"<br/>{user.Name}";
                    }
                    if (!string.IsNullOrEmpty(user.Location))
                    {
                        userInfo += $"<br/>📍 {user.Location}";
                    }

                    // 個人專案信息
                    var personalInfo = $"⭐ {personalStars:N0} 🍴 {personalForks:N0}";
                    if (personalProjects.Count > 0)
                    {
                        personalInfo += $"<br/>📦 {personalProjects.Count} 個專案";
                        // 顯示前 3 個最受歡迎的個人專案
                        var topPersonal = personalProjects.OrderByDescending(p => p.StargazersCount).Take(3);
                        foreach (var project in topPersonal)
                        {
                            if (project.StargazersCount > 0)
                            {
                                personalInfo += $"<br/>• [{project.Name}]({GetProjectUrl(project)}) ({project.StargazersCount:N0}⭐)";
                            }
                        }
                    }

                    // 貢獻專案信息
                    var contributedInfo = $"⭐ {contributedStars:N0} 🍴 {contributedForks:N0}";
                    if (contributedProjects.Count > 0)
                    {
                        contributedInfo += $"<br/>🏢 {contributedProjects.Count} 個專案";
                        // 顯示前 3 個貢獻的組織專案
                        var topContributed = contributedProjects.OrderByDescending(p => p.StargazersCount).Take(3);
                        foreach (var project in topContributed)
                        {
                            if (project.StargazersCount > 0)
                            {
                                contributedInfo += $"<br/>• [{project.Name}]({GetProjectUrl(project)}) ({project.StargazersCount:N0}⭐)";
                            }
                        }
                    }

                    sb.AppendLine($"| {rank} | {userLink}<br/>{userInfo} | {user.Followers:N0} | {personalInfo} | {contributedInfo} | **{totalInfluence:N0}** |");
                }

                // 添加結尾信息
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine("## 📝 說明");
                sb.AppendLine();
                sb.AppendLine("- 本排行榜統計台灣地區 GitHub 用戶的公開數據");
                sb.AppendLine("- 數據來源：GitHub API");
                sb.AppendLine("- 統計條件：Followers >= 100");
                sb.AppendLine("- 個人專案：用戶擁有的所有公開倉庫");
                sb.AppendLine("- 貢獻專案：用戶在組織中排名前三的貢獻專案");
                sb.AppendLine("- 排名依據：Followers + Personal Stars + Personal Forks + Contributed Stars + Contributed Forks");
                sb.AppendLine();
                sb.AppendLine("## 🔄 更新頻率");
                sb.AppendLine();
                sb.AppendLine("本排行榜會定期更新，數據更新時間請參考上方的統計摘要。");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine($"*最後更新時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}*");

                // 寫入文件
                await File.WriteAllTextAsync(outputPath, sb.ToString(), Encoding.UTF8);
                
                Console.WriteLine($"✅ README.md 生成完成！");
                Console.WriteLine($"📄 文件路径: {Path.GetFullPath(outputPath)}");
                Console.WriteLine($"📊 包含 {rankedUsers.Count} 位開發者的詳細信息");
                
                // 顯示前 10 名
                Console.WriteLine($"\n🏆 前 10 名開發者:");
                for (int i = 0; i < Math.Min(10, rankedUsers.Count); i++)
                {
                    var user = rankedUsers[i];
                    var totalInfluence = user.Followers + user.TotalStars + user.TotalForks;
                    Console.WriteLine($"   {i + 1:D2}. {user.Login} - 總影響力: {totalInfluence:N0} (Followers: {user.Followers:N0}, Stars: {user.TotalStars:N0}, Forks: {user.TotalForks:N0})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 生成 README.md 時發生錯誤: {ex.Message}");
                Console.WriteLine($"錯誤詳情: {ex.StackTrace}");
                throw;
            }
        }

        private string GetProjectUrl(UserProject project)
        {
            if (!string.IsNullOrEmpty(project.FullName))
            {
                return $"https://github.com/{project.FullName}";
            }
            return $"https://github.com/{project.Organization}/{project.Name}";
        }
    }
}
