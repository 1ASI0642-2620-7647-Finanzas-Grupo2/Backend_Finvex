using System.Security.Claims;
using Finvex.Application;
using Finvex.Domain;

namespace Finvex.API;

public static class AuditoriaExtensions
{
    public static void Registrar(this IAuditoriaService auditoria, ClaimsPrincipal usuario, string accion, string entidad,
        long? tiendaId, long? entidadId = null, string? detalle = null, Action<Operacion>? completarAlGuardar = null)
    {
        var actor = usuario.FindFirstValue(ClaimTypes.NameIdentifier) ?? usuario.FindFirstValue("sub");
        auditoria.Registrar(new Operacion
        {
            TiendaId = tiendaId,
            ActorRol = usuario.FindFirstValue(ClaimTypes.Role) ?? "Anonimo",
            ActorId = long.TryParse(actor, out var actorId) ? actorId : null,
            Accion = accion,
            Entidad = entidad,
            EntidadId = entidadId,
            Detalle = detalle
        }, completarAlGuardar);
    }
}
