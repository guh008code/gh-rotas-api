namespace gh_rotas_api.Models.Responses;

public sealed record LoginResponse(string AccessToken, string TokenType, int ExpiresIn, PerfilResponse User);
