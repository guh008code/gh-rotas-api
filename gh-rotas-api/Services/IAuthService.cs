using gh_rotas_api.Models.Requests;
using gh_rotas_api.Models.Responses;

namespace gh_rotas_api.Services;

public interface IAuthService
{
    Task<PerfilResponse?> Cadastrar(CadastroRequest request, CancellationToken ct);
    Task<LoginResponse?> Login(LoginRequest request, CancellationToken ct);
    Task<PerfilResponse?> ObterPerfil(Guid id, CancellationToken ct);
}
