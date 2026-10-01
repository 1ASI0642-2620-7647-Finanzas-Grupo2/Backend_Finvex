namespace Finvex.Application;

public static class HoraLima
{
    private static readonly TimeZoneInfo Zona = ObtenerZona();

    public static DateTime Ahora => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zona);

    public static DateTime Hoy => Ahora.Date;

    public static DateTime DesdeUtc(DateTime fechaUtc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(fechaUtc, DateTimeKind.Utc), Zona);

    public static DateTime AUtc(DateTime fechaLima) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(fechaLima, DateTimeKind.Unspecified), Zona);

    public static DateTime Normalizar(DateTime fecha) => fecha.Kind switch
    {
        DateTimeKind.Utc => DesdeUtc(fecha),
        DateTimeKind.Local => DesdeUtc(fecha.ToUniversalTime()),
        _ => fecha
    };

    private static TimeZoneInfo ObtenerZona()
    {
        foreach (var id in new[] { "America/Lima", "SA Pacific Standard Time" })
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zona)) return zona;
        return TimeZoneInfo.CreateCustomTimeZone("America/Lima", TimeSpan.FromHours(-5), "America/Lima", "America/Lima");
    }
}
