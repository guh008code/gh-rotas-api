using gh_rotas_api.Models;

namespace gh_rotas_api.Repositories;

public interface IUserRepository
{
    Task<User?> FindByEmail(string email, CancellationToken ct);
    Task<User?> FindById(Guid id, CancellationToken ct);
    Task<bool> Create(User user, CancellationToken ct);
}
