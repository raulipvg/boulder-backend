namespace ErpBoulder.Infrastructure.Services;

using BCrypt.Net;
using ErpBoulder.Application.DTOs.Auth;
using ErpBoulder.Application.Interfaces.Common;
using ErpBoulder.Application.Interfaces.Security;
using ErpBoulder.Application.Interfaces.Services;
using ErpBoulder.Domain.Constants;
using ErpBoulder.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public sealed class AuthService(
    ErpBoulderDbContext dbContext,
    IJwtTokenService jwtTokenService,
    ICurrentUserContext currentUser) : IAuthService
{
    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken)
    {
        var usuario = await dbContext.Usuarios
            .Include(x => x.Persona)
            .Include(x => x.Roles)
                .ThenInclude(x => x.Rol)
            .Include(x => x.Roles)
                .ThenInclude(x => x.Empresa)
            .FirstOrDefaultAsync(x => x.EmailLogin == request.Email, cancellationToken)
            ?? throw new InvalidOperationException("Credenciales inválidas.");

        if (!BCrypt.Verify(request.Password, usuario.PasswordHash))
        {
            throw new InvalidOperationException("Credenciales inválidas.");
        }

        if (!string.Equals(usuario.Estado, "activo", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("El usuario no está activo.");
        }

        usuario.UltimoAccesoAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var userDto = BuildAuthUser(usuario);

        return new AuthResponseDto(
            jwtTokenService.CreateAccessToken(userDto),
            jwtTokenService.CreateRefreshToken(userDto),
            userDto);
    }

    public async Task<AuthResponseDto> RefreshAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken)
    {
        var tokenUser = jwtTokenService.ValidateRefreshToken(request.RefreshToken)
            ?? throw new InvalidOperationException("Refresh token inválido.");

        var usuario = await dbContext.Usuarios
            .Include(x => x.Persona)
            .Include(x => x.Roles).ThenInclude(x => x.Rol)
            .Include(x => x.Roles).ThenInclude(x => x.Empresa)
            .FirstOrDefaultAsync(x => x.UsuarioId == tokenUser.UserId, cancellationToken)
            ?? throw new InvalidOperationException("Usuario no encontrado.");

        var userDto = BuildAuthUser(usuario);

        return new AuthResponseDto(
            jwtTokenService.CreateAccessToken(userDto),
            jwtTokenService.CreateRefreshToken(userDto),
            userDto);
    }

    public async Task<AuthUserDto> MeAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new InvalidOperationException("Usuario no autenticado.");

        var usuario = await dbContext.Usuarios
            .Include(x => x.Persona)
            .Include(x => x.Roles).ThenInclude(x => x.Rol)
            .Include(x => x.Roles).ThenInclude(x => x.Empresa)
            .FirstOrDefaultAsync(x => x.UsuarioId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Usuario no encontrado.");

        return BuildAuthUser(usuario);
    }

    private static AuthUserDto BuildAuthUser(Domain.Entities.Administracion.Usuario usuario)
    {
        var activeRoles = usuario.Roles.Where(x => x.Activo).ToArray();
        var roles = activeRoles.Select(x => x.Rol.Codigo).Distinct().ToArray();
        var empresaRol = activeRoles.FirstOrDefault(x => x.EmpresaId.HasValue && x.Rol.Codigo != RoleCodes.AdminTotal);

        return new AuthUserDto(
            usuario.UsuarioId,
            usuario.Persona.NombreCompleto,
            usuario.EmailLogin,
            roles,
            empresaRol?.EmpresaId,
            empresaRol?.Empresa?.NombreComercial);
    }
}
