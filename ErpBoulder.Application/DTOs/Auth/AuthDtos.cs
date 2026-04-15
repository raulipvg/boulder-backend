namespace ErpBoulder.Application.DTOs.Auth;

public sealed record LoginRequestDto(string Email, string Password);

public sealed record RefreshTokenRequestDto(string RefreshToken);

public sealed record EmpresaContextDto(long EmpresaId, string NombreComercial);

public sealed record AuthUserDto(
    long UserId,
    string FullName,
    string Email,
    IReadOnlyCollection<string> RoleCodes,
    long? EmpresaId,
    string? EmpresaNombre);

public sealed record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    AuthUserDto User);
