using System.Net;
using System.Net.Http.Json;
using Xunit;

public sealed class SecurityTests
{
    [Fact]
    public async Task LoginLimitUsesNormalizedEmailAndReturnsRetryAfter()
    {
        await using var app = new ApiFactory();
        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login",
                new { email = "ana@example.com", password = "wrong-password" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var limited = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "ANA@EXAMPLE.COM", password = "wrong-password" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
        Assert.True(limited.Headers.CacheControl?.NoStore);

        var other = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "other@example.com", password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, other.StatusCode);
    }

    [Fact]
    public async Task GlobalLimitProtectsPerfilEvenWithoutToken()
    {
        await using var app = new ApiFactory();
        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        for (var attempt = 0; attempt < 120; attempt++)
        {
            using var response = await client.GetAsync("/api/auth/perfil");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using var limited = await client.GetAsync("/api/auth/perfil");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
    }

    [Fact]
    public async Task ForgedForwardedHeadersDoNotBypassIpLimit()
    {
        await using var app = new ApiFactory();
        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        for (var attempt = 0; attempt < 11; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login");
            request.Headers.Add("X-Forwarded-For", $"203.0.113.{attempt + 1}");
            request.Content = JsonContent.Create(new { email = "invalid", password = "" });
            using var response = await client.SendAsync(request);
            Assert.Equal(attempt < 10 ? HttpStatusCode.BadRequest : HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }
}
