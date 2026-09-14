using gh_rotas_api.Models;

namespace gh_rotas_api.Services;

public interface ITokenService
{
    AccessToken Create(Guid userId);
}
