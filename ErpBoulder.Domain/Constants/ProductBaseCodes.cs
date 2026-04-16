namespace ErpBoulder.Domain.Constants;

public static class ProductBaseCodes
{
    public const string TicketIndividual = "TICKET_INDIVIDUAL";
    public const string PackTickets = "PACK_TICKETS";
    public const string LegacyPack10Tickets = "PACK_10_TICKETS";
    public const string MensualidadPorHorario = "MENSUALIDAD_POR_HORARIO";
    public const string MensualidadTodoHorario = "MENSUALIDAD_TODO_HORARIO";
    public const string Clases = "CLASES";
    public const string ArriendoZapatillas = "ARRIENDO_ZAPATILLAS";
    public const string ProductoCaja = "PRODUCTO_CAJA";

    public static bool IsPackTickets(string? code)
    {
        return string.Equals(code, PackTickets, StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, LegacyPack10Tickets, StringComparison.OrdinalIgnoreCase);
    }
}
