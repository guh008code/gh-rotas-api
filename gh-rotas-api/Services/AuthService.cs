using gh_rotas_api.Models;
using gh_rotas_api.Models.Requests;
using gh_rotas_api.Models.Responses;
using gh_rotas_api.Repositories;
using Microsoft.AspNetCore.Identity;

namespace gh_rotas_api.Services;

public sealed class AuthService(
    IUserRepository users,
    IPasswordHasher<User> hasher,
    ITokenService tokens) : IAuthService
{
    // Mantém a verificação de hash mesmo quando o e-mail não está cadastrado.
    private static readonly User Dummy = new(Guid.Empty, "", "", "", "", new DateOnly(2000, 1, 1));
    private static readonly string DummyHash = new PasswordHasher<User>().HashPassword(Dummy, Guid.NewGuid().ToString());

    public async Task<PerfilResponse?> Cadastrar(CadastroRequest request, CancellationToken ct)
    {
        var user = new User(Guid.NewGuid(), request.Name.Trim(), request.Email.Trim(),
            request.Phone.Trim(), "", request.BirthDate!.Value);
        user = user with
        {
            PasswordHash = hasher.HashPassword(user, request.Password)
        };

        return await users.Create(user, ct) ? ToPerfil(user) : null;
    }

    public async Task<LoginResponse?> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmail(request.Email, ct);
        var result = hasher.VerifyHashedPassword(user ?? Dummy, user?.PasswordHash ?? DummyHash, request.Password);
        if (user is null || result == PasswordVerificationResult.Failed)
        {
            return null;
        }

        var token = tokens.Create(user.Id);
        return new LoginResponse(token.Value, "Bearer", token.ExpiresIn, ToPerfil(user));
    }

    public async Task<PerfilResponse?> ObterPerfil(Guid id, CancellationToken ct)
    {
        var user = await users.FindById(id, ct);
        return user is null ? null : ToPerfil(user);
    }

    private static PerfilResponse ToPerfil(User user) => new(user.Id, user.Name, user.Email, user.Phone);
}
