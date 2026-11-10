using System.IO;
using System.Text.Json;

namespace DM_RMS.Wpf.Services;

public sealed class ApiHostProvider
{
    private const string ConfigFileName = "api-hosts.json";
    private readonly IReadOnlyDictionary<string, string> _hosts;

    public ApiHostProvider()
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"API Host 설정 파일을 찾을 수 없습니다: {configPath}");
        }

        string json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<ApiHostConfiguration>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("API Host 설정 파일을 읽을 수 없습니다.");

        _hosts = new Dictionary<string, string>(config.ApiHosts, StringComparer.OrdinalIgnoreCase);
    }

    public string GetBaseUrl(string fab)
    {
        string normalizedFab = (fab ?? string.Empty).Trim().ToLowerInvariant();
        if (!_hosts.TryGetValue(normalizedFab, out string? baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException($"{normalizedFab}에 대한 API Host가 설정되지 않았습니다.");
        }

        return baseUrl.TrimEnd('/');
    }

    private sealed class ApiHostConfiguration
    {
        public Dictionary<string, string> ApiHosts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
