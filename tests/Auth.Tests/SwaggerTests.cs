using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Xunit;

public sealed class SwaggerTests
{
    [Fact]
    public async Task SwaggerExposesAuthEndpointsAndBearerForPerfil()
    {
        await using var app = new ApiFactory().WithWebHostBuilder(
            builder => builder.UseEnvironment("Development"));
        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        var html = await client.GetStringAsync("/swagger/index.html");
        Assert.Contains("swagger-ui", html);

        using var document = JsonDocument.Parse(
            await client.GetStringAsync("/swagger/v1/swagger.json"));
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.GetProperty("/api/auth/cadastro").TryGetProperty("post", out var cadastro));
        Assert.True(paths.GetProperty("/api/auth/login").TryGetProperty("post", out var login));
        Assert.True(paths.GetProperty("/api/auth/perfil").TryGetProperty("get", out var perfil));
        Assert.False(cadastro.TryGetProperty("security", out _));
        Assert.False(login.TryGetProperty("security", out _));
        Assert.True(perfil.GetProperty("security")[0].TryGetProperty("Bearer", out _));
    }
}
