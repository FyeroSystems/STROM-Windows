using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using STROM.Models;

namespace STROM.Services;

public class DirigeraClient
{
    private readonly HttpClient _http;

    public string HubIp { get; set; } = "";
    public string Token { get; set; } = "";

    public DirigeraClient()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
    }

    private string BaseUrl => $"https://{HubIp}:8443/v1";

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, $"{BaseUrl}{path}");
        if (!string.IsNullOrEmpty(Token))
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);
        return req;
    }

    public async Task<List<JsonObject>> GetDevicesAsync()
    {
        var req = CreateRequest(HttpMethod.Get, "/devices");
        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        return JsonNode.Parse(json)?.AsArray().Select(n => n!.AsObject()).ToList() ?? new();
    }

    public async Task PatchDeviceAsync(string id, object body)
    {
        var req = CreateRequest(HttpMethod.Patch, $"/devices/{id}");
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        await _http.SendAsync(req);
    }

    public async Task SetOnAsync(string id, bool isOn)
    {
        await PatchDeviceAsync(id, new[] { new { attributes = new { isOn } } });
    }

    public async Task SetLightLevelAsync(string id, int level)
    {
        await PatchDeviceAsync(id, new[] { new { attributes = new { lightLevel = level } } });
    }

    public async Task SetColorTemperatureAsync(string id, int temp)
    {
        await PatchDeviceAsync(id, new[] { new { attributes = new { colorTemperature = temp } } });
    }

    public async Task SetColorAsync(string id, double hue, double saturation)
    {
        await PatchDeviceAsync(id, new[] { new { attributes = new { colorHue = hue, colorSaturation = saturation } } });
    }

    public async Task SetControllerNameAsync(string id, string name)
    {
        await PatchDeviceAsync(id, new[] { new { attributes = new { customName = name } } });
    }

    public async Task UpdateRemoteLinksAsync(string deviceId, List<string> links)
    {
        await PatchDeviceAsync(deviceId, new[] { new { remoteLinks = links } });
    }

    public async Task<(string code, string verifier)> StartTokenGenerationAsync()
    {
        var verifier = GenerateCodeVerifier();
        var challenge = CodeChallenge(verifier);

        var url = $"{BaseUrl}/oauth/authorize?audience=homesmart.local&response_type=code&code_challenge={Uri.EscapeDataString(challenge)}&code_challenge_method=S256";
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        var resp = await _http.SendAsync(req);
        var json = JsonNode.Parse(await resp.Content.ReadAsStringAsync());
        var code = json?["code"]?.ToString() ?? "";
        return (code, verifier);
    }

    public async Task<string> FinishTokenGenerationAsync(string code, string verifier)
    {
        var hostname = Environment.MachineName;
        var body = $"code={Uri.EscapeDataString(code)}&name={Uri.EscapeDataString(hostname)}&grant_type=authorization_code&code_verifier={Uri.EscapeDataString(verifier)}";
        var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/oauth/token")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded")
        };
        var resp = await _http.SendAsync(req);
        var json = JsonNode.Parse(await resp.Content.ReadAsStringAsync());
        return json?["access_token"]?.ToString() ?? "";
    }

    private static string GenerateCodeVerifier()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
        var bytes = RandomNumberGenerator.GetBytes(128);
        var sb = new StringBuilder(128);
        foreach (var b in bytes)
            sb.Append(alphabet[b % alphabet.Length]);
        return sb.ToString();
    }

    private static string CodeChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(verifier));
        return Convert.ToBase64String(hash)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}

public static class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "STROM", "settings.json");

    public static (string HubIp, string Token, List<Outlet> Outlets) Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var data = JsonSerializer.Deserialize<SettingsData>(json);
                if (data != null)
                    return (data.HubIp ?? "", data.Token ?? "", data.Outlets ?? new());
            }
        }
        catch { }
        return ("", "", new());
    }

    public static void Save(string hubIp, string token, List<Outlet> outlets)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var data = new SettingsData { HubIp = hubIp, Token = token, Outlets = outlets };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private class SettingsData
    {
        public string? HubIp { get; set; }
        public string? Token { get; set; }
        public List<Outlet>? Outlets { get; set; }
    }
}
