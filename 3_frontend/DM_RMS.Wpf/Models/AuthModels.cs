using System.Text.Json.Serialization;

namespace DM_RMS.Wpf.Models;

public sealed class LoginRequest
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

public sealed class RegisterRequest
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class LoginResult
{
    [JsonPropertyName("fab")]
    public string Fab { get; set; } = "fab1";

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;
}

public sealed class RegisterResult
{
    [JsonPropertyName("registered")]
    public bool Registered { get; set; }

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;
}

public sealed class UserSession
{
    public string Fab { get; init; } = "fab1";
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string ApiBaseUrl { get; init; } = string.Empty;
    public string UsageSessionId { get; init; } = string.Empty;
}


public sealed class UsageSessionStartRequest
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("fab")]
    public string Fab { get; set; } = string.Empty;

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("pc_name")]
    public string PcName { get; set; } = string.Empty;

    [JsonPropertyName("os_user")]
    public string OsUser { get; set; } = string.Empty;

    [JsonPropertyName("os_version")]
    public string OsVersion { get; set; } = string.Empty;

    [JsonPropertyName("process_architecture")]
    public string ProcessArchitecture { get; set; } = string.Empty;

    [JsonPropertyName("app_version")]
    public string AppVersion { get; set; } = string.Empty;
}

public sealed class UsageSessionEndRequest
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;
}

public sealed class UsageSessionResult
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("recorded_at")]
    public string RecordedAt { get; set; } = string.Empty;
}
