using gh_rotas_api.Configuration;
using System.IdentityModel.Tokens.Jwt;
using gh_rotas_api.Models.Requests;
using gh_rotas_api.Models.Responses;
using gh_rotas_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace gh_rotas_api.Controllers;

[ApiController]
[RequestSizeLimit(16 * 1024)]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [AllowAnonymous, HttpPost("cadastro"), EnableRateLimiting("auth")]
    public async Task<ActionResult<PerfilResponse>> Cadastro(CadastroRequest request, CancellationToken ct)
    {
        var perfil = await authService.Cadastrar(request, ct);
        if (perfil is null)
        {
            return Conflict(new
            {
                message = "E-mail já cadastrado."
            });
        }

        return StatusCode(StatusCodes.Status201Created, perfil);
    }

    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("auth")]
    [ServiceFilter(typeof(LoginRateLimitFilter))]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var login = await authService.Login(request, ct);
        if (login is null)
        {
            return Unauthorized(new
            {
                message = "E-mail ou senha inválidos."
            });
        }

        Response.Headers.CacheControl = "no-store";
        return Ok(login);
    }

    [Authorize, HttpGet("perfil")]
    public async Task<ActionResult<PerfilResponse>> Perfil(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id))
        {
            return Unauthorized();
        }

        var perfil = await authService.ObterPerfil(id, ct);
        Response.Headers.CacheControl = "no-store";
        return perfil is null ? Unauthorized() : Ok(perfil);
    }
}
