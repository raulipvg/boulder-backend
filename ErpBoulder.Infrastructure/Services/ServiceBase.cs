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
    protected static readonly string[] TipoDiaOrder = ["LUN", "MAR", "MIE", "JUE", "VIE", "SAB", "DOM"];
    protected sealed record ChileTimeContext(
        DateTimeOffset NowLocal,
        DateOnly TodayLocal,
        TimeOnly CurrentTimeLocal,
        short CurrentDayOfWeek,
        DateTimeOffset StartOfDayUtc,
        DateTimeOffset EndOfDayUtc);

    protected ErpBoulderDbContext DbContext { get; } = dbContext;
    protected ICurrentUserContext CurrentUser { get; } = currentUser;

    protected static TimeZoneInfo GetChileTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Pacific SA Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                throw new InvalidOperationException("No se encontró la zona horaria de Chile en el sistema.");
            }
            catch (InvalidTimeZoneException)
            {
                throw new InvalidOperationException("La zona horaria de Chile es inválida en el sistema.");
            }
        }
        catch (InvalidTimeZoneException)
        {
            throw new InvalidOperationException("La zona horaria de Chile es inválida en el sistema.");
        }
    }

    protected static ChileTimeContext GetChileTimeContext()
    {
        var chileTimeZone = GetChileTimeZone();
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, chileTimeZone);

        var localDate = nowLocal.Date;
        var startOfDayLocal = DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified);
        var endOfDayLocal = startOfDayLocal.AddDays(1);

        var startOfDayUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(startOfDayLocal, chileTimeZone), TimeSpan.Zero);
        var endOfDayUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(endOfDayLocal, chileTimeZone), TimeSpan.Zero);

        return new ChileTimeContext(
            nowLocal,
            DateOnly.FromDateTime(nowLocal.DateTime),
            TimeOnly.FromDateTime(nowLocal.DateTime),
            GetDayOfWeekNumber(nowLocal.DayOfWeek),
            startOfDayUtc,
            endOfDayUtc);
    }

    protected static short GetDayOfWeekNumber(DateOnly date)
    {
        return GetDayOfWeekNumber(date.DayOfWeek);
    }

    protected static short GetDayOfWeekNumber(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => (short)1,
            DayOfWeek.Tuesday => (short)2,
            DayOfWeek.Wednesday => (short)3,
            DayOfWeek.Thursday => (short)4,
            DayOfWeek.Friday => (short)5,
            DayOfWeek.Saturday => (short)6,
            _ => (short)7,
        };
    }

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
            return "DOM";
        }

        return fecha.DayOfWeek switch
        {
            DayOfWeek.Monday => "LUN",
            DayOfWeek.Tuesday => "MAR",
            DayOfWeek.Wednesday => "MIE",
            DayOfWeek.Thursday => "JUE",
            DayOfWeek.Friday => "VIE",
            DayOfWeek.Saturday => "SAB",
            _ => "DOM"
        };
    }

    protected static string NormalizeTipoDiaCsv(string? tipoDiaCsv)
    {
        if (string.IsNullOrWhiteSpace(tipoDiaCsv))
        {
            throw new InvalidOperationException("Debe seleccionar al menos un día para la tarifa.");
        }

        var requestedCodes = tipoDiaCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToUpperInvariant())
            .Select(x => x is "DOM_FEST" or "FEST" ? "DOM" : x)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        if (requestedCodes.Count == 0)
        {
            throw new InvalidOperationException("Debe seleccionar al menos un día para la tarifa.");
        }

        var invalidCode = requestedCodes.FirstOrDefault(code => !TipoDiaOrder.Contains(code, StringComparer.Ordinal));
        if (!string.IsNullOrWhiteSpace(invalidCode))
        {
            throw new InvalidOperationException($"El código de día '{invalidCode}' no es válido.");
        }

        var ordered = TipoDiaOrder.Where(code => requestedCodes.Contains(code)).ToArray();
        return string.Join(',', ordered);
    }

    protected static bool HasTipoDiaIntersection(string tipoDiaCsvA, string tipoDiaCsvB)
    {
        var first = tipoDiaCsvA
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

        return tipoDiaCsvB
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(first.Contains);
    }

    protected static bool CsvContainsCode(string csv, string code)
    {
        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(x => x.Equals(code, StringComparison.Ordinal));
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
        return $"V-{GetChileTimeContext().NowLocal.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}-{Random.Shared.Next(1000, 9999)}";
    }
}
