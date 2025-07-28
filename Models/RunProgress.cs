using System.Text.Json;

namespace TaiwanGitHubPopularUsers.Models
{
    public class RunProgress
    {
        public DateTime LastRunTime { get; set; }
        public List<string> CompletedLocations { get; set; } = new();
        public List<string> FailedLocations { get; set; } = new();
        public int TotalUsersFound { get; set; }
        public bool IsCompleted { get; set; }
        public string? LastError { get; set; }
        public DateTime? RateLimitResetTime { get; set; }
        public bool EncounteredRateLimit { get; set; }
        public int ApiRequestCount { get; set; }
        public int MaxApiRequestsPerRun { get; set; } = 50;
        public bool ReachedApiLimit { get; set; }
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsRateLimited { get; set; }
        public DateTime? RateLimitResetTime { get; set; }
    }
}
