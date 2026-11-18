using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Fundoo.User.Infrastructure.Security;

public class ServiceAuthOptions
{
    public string TokenEndpoint { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string Scope { get; set; } = "";
}

internal sealed class OAuthTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
}

public sealed class ClientCredentialsTokenProvider(IHttpClientFactory httpFactory, ServiceAuthOptions options)
{
    public const string HttpClientName = "oauth-token-client";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTime _expiresUtc;

    public async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_token is not null && DateTime.UtcNow < _expiresUtc) return _token;

        await _gate.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTime.UtcNow < _expiresUtc) return _token;

            using var request = new HttpRequestMessage(HttpMethod.Post, options.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = options.ClientId,
                    ["client_secret"] = options.ClientSecret,
                    ["scope"] = options.Scope
                })
            };

            using var response = await httpFactory.CreateClient(HttpClientName).SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<OAuthTokenResponse>(cancellationToken: ct)
                       ?? throw new HttpRequestException("Token endpoint returned an empty body.");
            if (string.IsNullOrEmpty(body.AccessToken))
                throw new HttpRequestException("Token endpoint did not return an access_token.");

            _token = body.AccessToken;
            _expiresUtc = DateTime.UtcNow.AddSeconds(Math.Max(body.ExpiresIn - 30, 10));
            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate() => _token = null;
}

public sealed class ClientCredentialsHandler(ClientCredentialsTokenProvider tokens) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetTokenAsync(ct));
        var response = await base.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized) tokens.Invalidate();
        return response;
    }
}