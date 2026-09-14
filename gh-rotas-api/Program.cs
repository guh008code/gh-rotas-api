using System.Security.Cryptography;
using System.Text;
using gh_rotas_api.Configuration;
using gh_rotas_api.Models;
using gh_rotas_api.Repositories;
using gh_rotas_api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

if (config["Database:Provider"] is not ("SqlServer" or "MySql"))
{
    throw new InvalidOperationException("Database:Provider deve ser SqlServer ou MySql.");
}

if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(config.GetConnectionString("DefaultConnection")))
{
    throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection.");
}

var temporaryJwtKey = builder.Environment.IsDevelopment() &&
    string.IsNullOrWhiteSpace(config["Jwt:Key"]);

if (temporaryJwtKey)
{
    config["Jwt:Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
}
if (Encoding.UTF8.GetByteCount(config["Jwt:Key"] ?? "") < 32 ||
    string.IsNullOrWhiteSpace(config["Jwt:Issuer"]) ||
    string.IsNullOrWhiteSpace(config["Jwt:Audience"]) ||
    config.GetValue<int>("Jwt:ExpirationMinutes") is < 1 or > 60)
{
    throw new InvalidOperationException(
        "Configure Jwt:Key (mínimo 32 bytes), Issuer, Audience e ExpirationMinutes (1-60).");
}

builder.Services.AddControllers();
builder.Services.AddProblemDetails();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.Configure<PasswordHasherOptions>(options => options.IterationCount = 100_000);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = config["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = config["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(config["Jwt:Key"]!)),
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        if (origins.Length > 0)
        {
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

builder.Services.AddRequestRateLimits();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GH Rotas API",
        Version = "v1",
        Description = "API de cadastro, login e consulta de perfil."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Faça login e informe somente o accessToken, sem o prefixo Bearer."
    });

    options.OperationFilter<gh_rotas_api.Configuration.BearerSecurityOperationFilter>();
});

var app = builder.Build();

app.UseExceptionHandler();

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.Headers.CacheControl = "no-store";
    }

    await next(context);
});

app.UseRouting();
app.UseCors();
app.UseRateLimiter();

if (temporaryJwtKey)
{
    app.Logger.LogWarning(
        "Chave JWT temporária de desenvolvimento. Os tokens deixam de valer ao reiniciar a API.");
}

if (app.Environment.IsDevelopment() &&
    string.IsNullOrWhiteSpace(config.GetConnectionString("DefaultConnection")))
{
    app.Logger.LogWarning(
        "Banco não configurado. Swagger disponível; configure ConnectionStrings:DefaultConnection para usar a autenticação.");

    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api/auth"))
        {
            await Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Banco de dados não configurado.",
                detail: "Configure ConnectionStrings:DefaultConnection e execute o script SQL do banco escolhido. Depois reinicie a API.")
                .ExecuteAsync(context);
            return;
        }

        await next(context);
    });
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "GH Rotas API v1");
        options.DocumentTitle = "GH Rotas API — Swagger";
        options.EnableTryItOutByDefault();
    });
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program
{
}
