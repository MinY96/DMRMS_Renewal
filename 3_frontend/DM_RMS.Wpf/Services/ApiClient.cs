using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using DM_RMS.Wpf.Models;

namespace DM_RMS.Wpf.Services;


public sealed class DownloadFileResult
{
    public byte[] Content { get; init; } = Array.Empty<byte>();
    public string FileName { get; init; } = "download.bin";
}

public sealed class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiClient(string baseUrl, int TimeoutSecond = 300)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(TimeoutSecond) };        
    }

    public async Task<ApiResponse<T>> GetAsync<T>(string route, CancellationToken ct = default)
    {
        using var response = await _httpClient.GetAsync(route, ct);
        return await ReadEnvelope<T>(response, ct);
    }

    public async Task<ApiResponse<T>> PostAsync<T>(string route, object body, CancellationToken ct = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(route, body, _jsonOptions, ct);
        return await ReadEnvelope<T>(response, ct);
    }

    public async Task<ApiResponse<T>> PutAsync<T>(string route, object body, CancellationToken ct = default)
    {
        using var response = await _httpClient.PutAsJsonAsync(route, body, _jsonOptions, ct);
        return await ReadEnvelope<T>(response, ct);
    }

    public async Task<ApiResponse<T>> DeleteAsync<T>(string route, CancellationToken ct = default)
    {
        using var response = await _httpClient.DeleteAsync(route, ct);
        return await ReadEnvelope<T>(response, ct);
    }

    public async Task<byte[]> DownloadFileAsync(string route, CancellationToken ct = default)
    {
        return await _httpClient.GetByteArrayAsync(route, ct);
    }

    public async Task<DownloadFileResult> PostDownloadFileAsync(string route, object body, CancellationToken ct = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(route, body, _jsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync(ct);
            var envelope = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(json, _jsonOptions);
            var message = envelope?.Errors.FirstOrDefault()?.Message ?? envelope?.Message ?? response.ReasonPhrase ?? "파일 요청 실패";
            throw new InvalidOperationException(message);
        }

        var content = await response.Content.ReadAsByteArrayAsync(ct);
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                       ?? response.Content.Headers.ContentDisposition?.FileName
                       ?? "recipe_backup.zip";
        fileName = fileName.Trim('"');
        return new DownloadFileResult { Content = content, FileName = fileName };
    }

    private async Task<ApiResponse<T>> ReadEnvelope<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var json = await response.Content.ReadAsStringAsync(ct);
        var envelope = JsonSerializer.Deserialize<ApiResponse<T>>(json, _jsonOptions)
            ?? throw new InvalidOperationException("API 응답 역직렬화 실패");

        if (!response.IsSuccessStatusCode || !envelope.Success)
        {
            var err = envelope.Errors.FirstOrDefault()?.Message ?? envelope.Message;
            throw new InvalidOperationException(err);
        }
        return envelope;
    }
}
