using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using gh_rotas_api.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace gh_rotas_api.Configuration;

// Limita o e-mail normalizado independentemente do IP, inclusive para contas inexistentes.
public sealed class LoginRateLimitFilter : IAsyncActionFilter, IDisposable
{
    private readonly PartitionedRateLimiter<string> limiter =
        PartitionedRateLimiter.Create<string, string>(email =>
            RateLimitPartition.GetFixedWindowLimiter(email, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.ActionArguments.Values.OfType<LoginRequest>().FirstOrDefault();
        if (request is null || !context.ModelState.IsValid)
        {
            await next();
            return;
        }

        var key = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(request.Email.Trim().ToUpperInvariant())));

        using var lease = limiter.AttemptAcquire(key);
        if (!lease.IsAcquired)
        {
            var seconds = lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)
                ? Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds))
                : 300;

            context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Muitas tentativas de login. Aguarde antes de tentar novamente."
            })
            {
                StatusCode = StatusCodes.Status429TooManyRequests
            };
            return;
        }

        await next();
    }

    public void Dispose() => limiter.Dispose();
}
