namespace gh_rotas_api.Models;

public sealed record User(Guid Id, string Name, string Email, string Phone, string PasswordHash, DateOnly BirthDate);
