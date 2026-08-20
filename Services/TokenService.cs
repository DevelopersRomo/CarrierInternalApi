using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using InternalCarrierApp.API.Models;

namespace InternalCarrierApp.API.Services;

public interface ITokenService
{
    (string token, DateTime expires) GenerateToken(
        ApplicationUser user, string role, IEnumerable<string> plantCodes);
}

public class TokenService(IConfiguration config) : ITokenService
{
    public (string token, DateTime expires) GenerateToken(
        ApplicationUser user, string role, IEnumerable<string> plantCodes)
    {
        var jwt      = config.GetSection("JwtSettings");
        var key      = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["SecretKey"]!));
        var creds    = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires  = DateTime.UtcNow.AddHours(double.Parse(jwt["ExpiresInHours"]!));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email,          user.Email!),
            new(ClaimTypes.Name,           user.FullName),
            new(ClaimTypes.Role,           role),
            new("plants",                  string.Join(",", plantCodes)),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer:             jwt["Issuer"],
            audience:           jwt["Audience"],
            claims:             claims,
            expires:            expires,
            signingCredentials: creds
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
