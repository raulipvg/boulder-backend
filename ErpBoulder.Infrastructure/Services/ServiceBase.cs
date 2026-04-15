namespace ErpBoulder.Infrastructure.Services;

using System.Globalization;
using System.Text.Json;
using ErpBoulder.Application.Interfaces.Common;
using ErpBoulder.Domain.Constants;
using ErpBoulder.Domain.Entities.Operacion;
using ErpBoulder.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public abstract class ServiceBase(ErpBoulderDbContext dbContext, ICurrentUserContext currentUser)
{
    protected ErpBoulderDbContext DbContext { get; } = dbContext;
    protected ICurrentUserContext CurrentUser { get; } = currentUser;

    protected long GetRequiredUserId()
    {
        return CurrentUser.UserId ?? throw new InvalidOperationException("Usuario no autenticado.");
    }

    protected long GetRequiredEmpresaId()
    {
        if (CurrentUser.IsInRole(RoleCodes.AdminTotal))
        {
            return CurrentUser.EmpresaObjetivoId ?? throw new InvalidOperationException("Debe seleccionar una empresa objetivo.");
        }

        return CurrentUser.EmpresaId ?? throw new InvalidOperationException("El usuario no tiene empresa asociada.");
    }

    protected long? GetOptionalEmpresaId()
    {
        return CurrentUser.IsInRole(RoleCodes.AdminTotal) ? CurrentUser.EmpresaObjetivoId : CurrentUser.EmpresaId;
    }

    protected async Task<string> GetTipoDiaAsync(DateOnly fecha, CancellationToken cancellationToken)
    {
        if (fecha.DayOfWeek == DayOfWeek.Sunday || await DbContext.Feriados.AnyAsync(x => x.Fecha == fecha, cancellationToken))
        {
            return "DOM_FEST";
        }

        return fecha.DayOfWeek switch
        {
            DayOfWeek.Monday => "LUN",
            DayOfWeek.Tuesday => "MAR",
            DayOfWeek.Wednesday => "MIE",
            DayOfWeek.Thursday => "JUE",
            DayOfWeek.Friday => "VIE",
            DayOfWeek.Saturday => "SAB",
            _ => "DOM_FEST"
        };
    }

    protected async Task AuditAsync(string entidad, long? entidadId, string accion, object detalle, long? empresaId, CancellationToken cancellationToken)
    {
        DbContext.AuditoriaEventos.Add(new AuditoriaEvento
        {
            EmpresaId = empresaId,
            UsuarioId = CurrentUser.UserId,
            Entidad = entidad,
            EntidadId = entidadId,
            Accion = accion,
            FechaHora = DateTimeOffset.UtcNow,
            DetalleJson = JsonSerializer.Serialize(detalle)
        });

        await DbContext.SaveChangesAsync(cancellationToken);
    }

    protected string GenerateComprobanteNumber()
    {
        return $"V-{DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-4)).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}-{Random.Shared.Next(1000, 9999)}";
    }
}
