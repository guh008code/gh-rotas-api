using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using gh_rotas_api.Models;
using Microsoft.IdentityModel.Tokens;

namespace gh_rotas_api.Services;

public sealed class TokenService(IConfiguration configuration) : ITokenService
{
    public AccessToken Create(Guid userId)
    {
        var minutes = configuration.GetValue<int>("Jwt:ExpirationMinutes");
        var now = DateTime.UtcNow;
        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            },
            notBefore: now,
            expires: now.AddMinutes(minutes),
            signingCredentials: new SigningCredentials(
                signingKey,
                SecurityAlgorithms.HmacSha256));

        return new AccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            minutes * 60);
    }
}
