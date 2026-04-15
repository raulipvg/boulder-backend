namespace ErpBoulder.Application.Interfaces.Common;

public interface ICurrentUserContext
{
    long? UserId { get; }
    string? Email { get; }
    IReadOnlyCollection<string> Roles { get; }
    long? EmpresaId { get; }
    long? EmpresaObjetivoId { get; }

    bool IsAuthenticated();
    bool IsInRole(string roleCode);
}
