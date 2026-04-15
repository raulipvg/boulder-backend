namespace ErpBoulder.Application.Interfaces.Security;

using ErpBoulder.Application.DTOs.Auth;

public interface IJwtTokenService
{
    string CreateAccessToken(AuthUserDto user);
    string CreateRefreshToken(AuthUserDto user);
    AuthUserDto? ValidateRefreshToken(string refreshToken);
}
