using System.Net;
using Microsoft.AspNetCore.Hosting;
using Xunit;

public sealed class StartupTests
{
    [Fact]
    public async Task DevelopmentWithoutDatabaseOrKeyCanOpenSwagger()
    {
        await using var app = new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "");
            builder.UseSetting("Jwt:Key", "");
        });

        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode);

        var response = await client.PostAsync("/api/auth/login", null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("ConnectionStrings:DefaultConnection", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("ConnectionStrings:DefaultConnection")]
    [InlineData("Jwt:Key")]
    public void ProductionRequiresConfiguration(string setting)
    {
        using var app = new ApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting(setting, "");
        });

        Assert.Throws<InvalidOperationException>(() => app.CreateClient());
    }
}
