namespace ErpBoulder.Application.Interfaces.Services;

using ErpBoulder.Application.DTOs.Auth;

public interface IAuthService
{
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken);
    Task<AuthResponseDto> RefreshAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken);
    Task<AuthUserDto> MeAsync(CancellationToken cancellationToken);
}
