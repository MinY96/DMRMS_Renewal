using System.IO;
using System.Text.Json;

namespace DM_RMS.Wpf.Services;

/// <summary>
/// 사용자별 DM RMS 설정을 LocalApplicationData에 JSON으로 저장합니다.
/// 설치 폴더가 읽기 전용이거나 ClickOnce로 배포되어도 설정이 유지됩니다.
/// </summary>
public sealed class AppSettingsService
{
    private static readonly object SyncRoot = new();
    private readonly string _settingsFilePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public AppSettingsService()
    {
        var settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DM_RMS");
        _settingsFilePath = Path.Combine(settingsDirectory, "settings.json");
    }

    public string? GetBackupPath()
    {
        lock (SyncRoot)
        {
            return Load().BackupPath;
        }
    }

    public void SaveBackupPath(string? backupPath)
    {
        try
        {
            lock (SyncRoot)
            {
                var settings = Load();
                settings.BackupPath = string.IsNullOrWhiteSpace(backupPath)
                    ? null
                    : Path.GetFullPath(backupPath);
                Save(settings);
            }
        }
        catch (ArgumentException)
        {
            // 잘못된 경로로 인해 프로그램 전체가 종료되지 않도록 기존 설정을 유지합니다.
        }
        catch (IOException)
        {
            // 설정 저장 실패는 Recipe 기능 자체를 중단시키지 않습니다.
        }
        catch (UnauthorizedAccessException)
        {
            // 사용자 프로필 폴더 쓰기 권한이 없을 때도 프로그램은 계속 동작합니다.
        }
    }

    private UserSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
                return new UserSettings();

            var json = File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize<UserSettings>(json, _jsonOptions)
                   ?? new UserSettings();
        }
        catch (JsonException)
        {
            return new UserSettings();
        }
        catch (IOException)
        {
            return new UserSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new UserSettings();
        }
    }

    private void Save(UserSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsFilePath)
            ?? throw new InvalidOperationException("설정 파일 경로를 계산할 수 없습니다.");
        Directory.CreateDirectory(directory);

        var tempPath = _settingsFilePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, _jsonOptions));
        File.Move(tempPath, _settingsFilePath, overwrite: true);
    }

    private sealed class UserSettings
    {
        public string? BackupPath { get; set; }
    }
}
