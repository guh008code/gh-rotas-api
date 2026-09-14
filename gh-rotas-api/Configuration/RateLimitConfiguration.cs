using System.Globalization;
using System.Threading.RateLimiting;

namespace gh_rotas_api.Configuration;

public static class RateLimitConfiguration
{
    public static IServiceCollection AddRequestRateLimits(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)
                    ? Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds))
                    : 1;
                context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                await Results.Problem(
                    statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Muitas requisições. Aguarde antes de tentar novamente.")
                    .ExecuteAsync(context.HttpContext);
            };

            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                    RateLimitPartition.GetConcurrencyLimiter("server", _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = 32,
                        QueueLimit = 0
                    })),
                PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 120,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    })));

            options.AddPolicy("auth", context =>
                RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        });

        services.AddSingleton<LoginRateLimitFilter>();
        return services;
    }

    private static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown";
}
