namespace ErpBoulder.Application.Configuration;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "ErpBoulder";
    public string Audience { get; set; } = "ErpBoulder.Frontend";
    public string Key { get; set; } = "super-secret-development-key-change-me";
    public int AccessTokenMinutes { get; set; } = 480;
    public int RefreshTokenDays { get; set; } = 30;
}
