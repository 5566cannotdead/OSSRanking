using System.Text.Json.Serialization;

namespace TaiwanGitHubPopularUsers.Models
{
    public class GitHubOrganization
    {
        [JsonPropertyName("login")]
        public string Login { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("company")]
        public string? Company { get; set; }

        [JsonPropertyName("blog")]
        public string? Blog { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("bio")]
        public string? Bio { get; set; }

        [JsonPropertyName("public_repos")]
        public int PublicRepos { get; set; }

        [JsonPropertyName("followers")]
        public int Followers { get; set; }

        [JsonPropertyName("following")]
        public int Following { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }

        // 計算屬性
        public int TotalStars { get; set; }
        public int TotalForks { get; set; }
        public DateTime LastFetched { get; set; } = DateTime.UtcNow;
    }

    public class GitHubRepository
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("full_name")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("stargazers_count")]
        public int StargazersCount { get; set; }

        [JsonPropertyName("forks_count")]
        public int ForksCount { get; set; }

        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    public class TaiwanInfluenceReport
    {
        public List<GitHubUser> TopDevelopers { get; set; } = new();
        public List<GitHubOrganization> TopOrganizations { get; set; } = new();
        public TaiwanStatistics Statistics { get; set; } = new();
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    public class TaiwanStatistics
    {
        public int TotalDevelopers { get; set; }
        public int TotalOrganizations { get; set; }
        public int TotalStars { get; set; }
        public int TotalForks { get; set; }
        public int TotalFollowers { get; set; }
        public int TotalPublicRepos { get; set; }
        public Dictionary<string, int> LocationDistribution { get; set; } = new();
        public Dictionary<string, int> LanguageDistribution { get; set; } = new();
    }
}
