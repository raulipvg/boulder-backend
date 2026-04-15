namespace ErpBoulder.Api.Authorization;

using System.Security.Claims;
using ErpBoulder.Application.Interfaces.Common;

public sealed class CurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    private HttpContext? HttpContext => httpContextAccessor.HttpContext;

    public long? UserId => long.TryParse(HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var value) ? value : null;
    public string? Email => HttpContext?.User.FindFirstValue(ClaimTypes.Email);
    public IReadOnlyCollection<string> Roles => HttpContext?.User.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray() ?? [];
    public long? EmpresaId => long.TryParse(HttpContext?.User.FindFirstValue("empresa_id"), out var value) ? value : null;

    public long? EmpresaObjetivoId
    {
        get
        {
            if (!IsInRole("ADMIN_TOTAL"))
            {
                return null;
            }

            return long.TryParse(HttpContext?.Request.Headers["X-Empresa-Id"].FirstOrDefault(), out var value) ? value : null;
        }
    }

    public bool IsAuthenticated() => HttpContext?.User?.Identity?.IsAuthenticated == true;

    public bool IsInRole(string roleCode) => Roles.Contains(roleCode);
}
