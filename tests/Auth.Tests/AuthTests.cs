using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using gh_rotas_api.Models;
using gh_rotas_api.Models.Responses;
using gh_rotas_api.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Provider", "SqlServer");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "unused-test-connection");
        builder.UseSetting("Jwt:Key", "test-only-secret-at-least-32-bytes-long-do-not-deploy");
        builder.UseSetting("Jwt:Issuer", "test-api");
        builder.UseSetting("Jwt:Audience", "test-client");
        builder.UseSetting("Jwt:ExpirationMinutes", "15");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IUserRepository>();
            services.AddSingleton<IUserRepository, MemoryUsers>();
        });
    }
}
public sealed class MemoryUsers : IUserRepository
{
    private readonly ConcurrentDictionary<string, User> users = new();
    public Task<bool> Create(User user, CancellationToken ct) => Task.FromResult(users.TryAdd(user.Email.ToUpperInvariant(), user));
    public Task<User?> FindByEmail(string email, CancellationToken ct) => Task.FromResult(users.GetValueOrDefault(email.Trim().ToUpperInvariant()));
    public Task<User?> FindById(Guid id, CancellationToken ct) => Task.FromResult(users.Values.FirstOrDefault(u => u.Id == id));
}
public sealed class AuthTests
{
    private static object Registration(string email = "ana@example.com") => new { name = "Ana Silva", email, phone = "+55 11 99999-1234", password = "Senha-forte-123!", birthDate = "1995-05-12" };

    [Fact]
    public async Task RegisterLoginAndProfile()
    {
        await using var app = new ApiFactory();
        using var client = app.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost")
        });
        var registered = await client.PostAsJsonAsync("/api/auth/cadastro", Registration());
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var profile = await registered.Content.ReadFromJsonAsync<PerfilResponse>();
        var stored = await app.Services.GetRequiredService<IUserRepository>().FindByEmail("ana@example.com", default);
        Assert.NotEqual("Senha-forte-123!", stored!.PasswordHash);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/auth/cadastro", Registration("ANA@example.com"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/perfil")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "ana@example.com",
            password = "incorreta"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "unknown@example.com",
            password = "incorreta"
        })).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "ANA@example.com",
            password = "Senha-forte-123!"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.Equal("Bearer", token.TokenType);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var me = await client.GetFromJsonAsync<PerfilResponse>("/api/auth/perfil");
        Assert.Equal(profile, me);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/perfil")).StatusCode);
    }

    [Fact]
    public async Task InvalidRegistrationIsRejected()
    {
        await using var app = new ApiFactory();
        using var client = app.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost")
        });
        var response = await client.PostAsJsonAsync("/api/auth/cadastro", new
        {
            name = " ",
            email = "bad",
            phone = "1",
            password = "short",
            birthDate = "2999-01-01"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        response = await client.PostAsJsonAsync("/api/auth/cadastro", new
        {
            name = "Ana",
            email = "ana@example.com",
            phone = "+5511999991234",
            password = "Senha-forte-123!"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LoginRateLimitIsEnforced()
    {
        await using var app = new ApiFactory();
        using var client = app.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost")
        });
        HttpResponseMessage? response = null;
        for (var i = 0; i < 11; i++)
        {
            response = await client.PostAsJsonAsync("/api/auth/login", new
            {
                email = "bad",
                password = ""
            });
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, response!.StatusCode);
    }
}


public sealed class TokenValidationTests
{
    [Theory]
    [InlineData("expired")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("signature")]
    public async Task UntrustedTokensAreRejected(string reason)
    {
        await using var app = new ApiFactory();
        using var client = app.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost")
        });
        var now = DateTime.UtcNow;
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: reason == "issuer" ? "other" : "test-api",
            audience: reason == "audience" ? "other" : "test-client",
            claims: new[] { new System.Security.Claims.Claim("sub", Guid.NewGuid().ToString()) },
            notBefore: now.AddHours(-1),
            expires: reason == "expired" ? now.AddMinutes(-5) : now.AddMinutes(5),
            signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(
                new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(
                    reason == "signature" ? "different-secret-at-least-32-bytes-long" : "test-only-secret-at-least-32-bytes-long-do-not-deploy")),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token));
        var response = await client.GetAsync("/api/auth/perfil");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, h => h.Parameter?.Contains("invalid_token") == true);
    }
}
