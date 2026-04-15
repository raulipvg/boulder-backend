namespace ErpBoulder.Infrastructure.Services;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ErpBoulder.Application.Configuration;
using ErpBoulder.Application.DTOs.Auth;
using ErpBoulder.Application.Interfaces.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

public sealed class JwtTokenService(IOptions<JwtSettings> options) : IJwtTokenService
{
    private readonly JwtSettings _settings = options.Value;

    public string CreateAccessToken(AuthUserDto user)
    {
        return CreateToken(user, DateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes), "access");
    }

    public string CreateRefreshToken(AuthUserDto user)
    {
        return CreateToken(user, DateTime.UtcNow.AddDays(_settings.RefreshTokenDays), "refresh");
    }

    public AuthUserDto? ValidateRefreshToken(string refreshToken)
    {
        var tokenHandler = new JwtSecurityTokenHandler();

        try
        {
            var principal = tokenHandler.ValidateToken(refreshToken, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _settings.Issuer,
                ValidAudience = _settings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key)),
                ClockSkew = TimeSpan.Zero
            }, out _);

            if (principal.FindFirst("token_type")?.Value != "refresh")
            {
                return null;
            }

            var roles = principal.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray();

            return new AuthUserDto(
                long.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0"),
                principal.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty,
                principal.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
                roles,
                long.TryParse(principal.FindFirst("empresa_id")?.Value, out var empresaId) ? empresaId : null,
                principal.FindFirst("empresa_nombre")?.Value);
        }
        catch
        {
            return null;
        }
    }

    private string CreateToken(AuthUserDto user, DateTime expiresUtc, string tokenType)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new("token_type", tokenType)
        };

        claims.AddRange(user.RoleCodes.Select(role => new Claim(ClaimTypes.Role, role)));

        if (user.EmpresaId.HasValue)
        {
            claims.Add(new Claim("empresa_id", user.EmpresaId.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(user.EmpresaNombre))
        {
            claims.Add(new Claim("empresa_nombre", user.EmpresaNombre));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expiresUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
