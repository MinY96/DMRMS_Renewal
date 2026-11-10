using System.Reflection;
using System.Runtime.InteropServices;
using DM_RMS.Wpf.Models;

namespace DM_RMS.Wpf.Services;

public static class ClientEnvironmentInfo
{
    public static UsageSessionStartRequest CreateUsageStartRequest(UserSession session, string sessionId)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        return new UsageSessionStartRequest
        {
            SessionId = sessionId,
            Fab = session.Fab,
            UserId = session.UserId,
            DisplayName = session.DisplayName,
            Role = session.Role,
            PcName = Environment.MachineName,
            OsUser = Environment.UserName,
            OsVersion = RuntimeInformation.OSDescription,
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            AppVersion = version
        };
    }
}
