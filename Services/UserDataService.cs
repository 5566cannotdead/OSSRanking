using System.Text.Json;
using TaiwanGitHubPopularUsers.Models;

namespace TaiwanGitHubPopularUsers.Services
{
    public class UserDataService
    {
        private readonly string _dataFilePath;
        private readonly JsonSerializerOptions _jsonOptions;

        public UserDataService(string dataFilePath = "Users.json")
        {
            _dataFilePath = dataFilePath;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task<List<GitHubUser>> LoadExistingUsersAsync()
        {
            if (!File.Exists(_dataFilePath))
            {
                Console.WriteLine("用戶數據文件不存在，將創建新文件");
                return new List<GitHubUser>();
            }

            try
            {
                var json = await File.ReadAllTextAsync(_dataFilePath);
                var users = JsonSerializer.Deserialize<List<GitHubUser>>(json, _jsonOptions);
                Console.WriteLine($"載入了 {users?.Count ?? 0} 位現有用戶");
                return users ?? new List<GitHubUser>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"載入用戶數據時發生錯誤: {ex.Message}");
                return new List<GitHubUser>();
            }
        }

        public async Task SaveUsersAsync(List<GitHubUser> users)
        {
            try
            {
                var json = JsonSerializer.Serialize(users, _jsonOptions);
                await File.WriteAllTextAsync(_dataFilePath, json);
                Console.WriteLine($"已保存 {users.Count} 位用戶到 {_dataFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存用戶數據時發生錯誤: {ex.Message}");
            }
        }

        public async Task<List<GitHubUser>> MergeAndUpdateUsersAsync(List<GitHubUser> newUsers)
        {
            var existingUsers = await LoadExistingUsersAsync();
            var mergedUsers = new Dictionary<long, GitHubUser>();

            // 添加現有用戶
            foreach (var user in existingUsers)
            {
                mergedUsers[user.Id] = user;
            }

            // 更新或添加新用戶
            int newCount = 0;
            int updatedCount = 0;

            foreach (var newUser in newUsers)
            {
                if (mergedUsers.ContainsKey(newUser.Id))
                {
                    // 更新現有用戶
                    var existingUser = mergedUsers[newUser.Id];
                    if (IsUserDataDifferent(existingUser, newUser))
                    {
                        mergedUsers[newUser.Id] = newUser;
                        updatedCount++;
                    }
                }
                else
                {
                    // 添加新用戶
                    mergedUsers[newUser.Id] = newUser;
                    newCount++;
                }
            }

            var result = mergedUsers.Values.OrderByDescending(u => u.Followers).ToList();
            
            Console.WriteLine($"數據合併完成: 新增 {newCount} 位用戶，更新 {updatedCount} 位用戶");
            
            await SaveUsersAsync(result);
            return result;
        }

        private bool IsUserDataDifferent(GitHubUser existing, GitHubUser newUser)
        {
            return existing.Followers != newUser.Followers ||
                   existing.Following != newUser.Following ||
                   existing.PublicRepos != newUser.PublicRepos ||
                   existing.Location != newUser.Location ||
                   existing.Company != newUser.Company ||
                   existing.Bio != newUser.Bio ||
                   existing.Blog != newUser.Blog ||
                   existing.Name != newUser.Name;
        }

        public void PrintUserSummary(List<GitHubUser> users)
        {
            Console.WriteLine("\n=== 台灣 GitHub 知名開發者清單 ===");
            Console.WriteLine($"總計: {users.Count} 位開發者");
            Console.WriteLine("排名前 20 位:");
            Console.WriteLine("排名\t用戶名\t\t追踪者\t地區\t\t姓名");
            Console.WriteLine("====\t======\t\t======\t====\t\t====");

            for (int i = 0; i < Math.Min(20, users.Count); i++)
            {
                var user = users[i];
                var location = string.IsNullOrEmpty(user.Location) ? "未知" : user.Location;
                var name = string.IsNullOrEmpty(user.Name) ? "未提供" : user.Name;
                
                Console.WriteLine($"{i + 1,2}\t{user.Login,-15}\t{user.Followers,6}\t{location,-12}\t{name}");
            }

            if (users.Count > 20)
            {
                Console.WriteLine($"... 還有 {users.Count - 20} 位開發者");
            }

            Console.WriteLine($"\n數據更新時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }
    }
}
