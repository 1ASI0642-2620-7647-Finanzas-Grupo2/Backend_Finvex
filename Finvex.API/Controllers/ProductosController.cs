using System.Security.Claims;
using Finvex.Application;
using Finvex.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finvex.API.Controllers;

[ApiController]
[Route("api/productos")]
[Authorize(Roles = "Admin")]
public sealed class ProductosController(
    IProductoRepository productos,
    IUnitOfWork unitOfWork,
    IWebHostEnvironment environment,
    IAuditoriaService auditoria) : ControllerBase
{
    private const long TamanoMaximoImagen = 2 * 1024 * 1024;
    private const string RutaImagenes = "/uploads/productos/";

    private static readonly Dictionary<string, string> TiposImagen = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    /// <summary>Lista los productos de la tienda del administrador autenticado. Por defecto solo los activos.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<ProductoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyCollection<ProductoResponse>>> Listar([FromQuery] bool incluirInactivos, CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirstValue("TiendaId"), out var tiendaId)) return Unauthorized();
        var lista = await productos.ListarPorTiendaAsync(tiendaId, incluirInactivos, cancellationToken);
        return Ok(lista.Select(CrearRespuesta).ToArray());
    }

    /// <summary>Obtiene un producto de la tienda.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ProductoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductoResponse>> Obtener(long id, CancellationToken cancellationToken)
    {
        var producto = await ObtenerDeTiendaAsync(id, cancellationToken);
        return producto is null ? NotFound("Producto no encontrado.") : Ok(CrearRespuesta(producto));
    }

    /// <summary>Registra un producto en la tienda. Los precios se expresan en la moneda de la tienda con 2 decimales.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProductoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProductoResponse>> Registrar(ProductoRequest request, CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirstValue("TiendaId"), out var tiendaId)) return Unauthorized();
        var error = Validar(request);
        if (error is not null) return BadRequest(error);
        var producto = new Producto { TiendaId = tiendaId, Activo = true };
        Asignar(producto, request);
        await productos.AgregarAsync(producto, cancellationToken);
        auditoria.Registrar(User, AccionAuditoria.Alta, nameof(Producto), tiendaId, detalle: $"{producto.Descripcion}, precio lista {producto.PrecioLista}.",
            completarAlGuardar: operacion => operacion.EntidadId = producto.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return StatusCode(StatusCodes.Status201Created, CrearRespuesta(producto));
    }

    /// <summary>Actualiza los datos de un producto de la tienda.</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ProductoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductoResponse>> Actualizar(long id, ProductoRequest request, CancellationToken cancellationToken)
    {
        var producto = await ObtenerDeTiendaAsync(id, cancellationToken);
        if (producto is null) return NotFound("Producto no encontrado.");
        var error = Validar(request);
        if (error is not null) return BadRequest(error);
        Asignar(producto, request);
        auditoria.Registrar(User, AccionAuditoria.Edicion, nameof(Producto), producto.TiendaId, producto.Id,
            $"{producto.Descripcion}, precio lista {producto.PrecioLista}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(CrearRespuesta(producto));
    }

    /// <summary>Da de baja lógica a un producto: no podrá usarse en nuevas compras.</summary>
    [HttpPut("{id:long}/baja")]
    [ProducesResponseType(typeof(ProductoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<ProductoResponse>> DarDeBaja(long id, CancellationToken cancellationToken) =>
        CambiarEstado(id, false, cancellationToken);

    /// <summary>Reactiva un producto dado de baja.</summary>
    [HttpPut("{id:long}/alta")]
    [ProducesResponseType(typeof(ProductoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<ProductoResponse>> DarDeAlta(long id, CancellationToken cancellationToken) =>
        CambiarEstado(id, true, cancellationToken);

    /// <summary>Sube la imagen de un producto (multipart, campo archivo). Acepta JPG, PNG o WEBP de hasta 2 MB y la publica en /uploads/productos.</summary>
    [HttpPost("{id:long}/imagen")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(TamanoMaximoImagen + 64 * 1024)]
    [ProducesResponseType(typeof(ProductoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductoResponse>> SubirImagen(long id, IFormFile archivo, CancellationToken cancellationToken)
    {
        var producto = await ObtenerDeTiendaAsync(id, cancellationToken);
        if (producto is null) return NotFound("Producto no encontrado.");
        if (archivo is null || archivo.Length == 0) return BadRequest("Debe adjuntar una imagen.");
        if (archivo.Length > TamanoMaximoImagen) return BadRequest("La imagen no debe superar 2 MB.");
        if (!TiposImagen.TryGetValue(archivo.ContentType, out var extension) || !await TieneFirmaValidaAsync(archivo, extension, cancellationToken))
            return BadRequest("Solo se permiten imágenes JPG, PNG o WEBP.");

        var carpeta = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads", "productos");
        Directory.CreateDirectory(carpeta);
        var nombre = $"{Guid.NewGuid():N}{extension}";
        await using (var destino = System.IO.File.Create(Path.Combine(carpeta, nombre)))
            await archivo.CopyToAsync(destino, cancellationToken);

        var anterior = producto.ImagenUrl;
        producto.ImagenUrl = RutaImagenes + nombre;
        auditoria.Registrar(User, AccionAuditoria.Edicion, nameof(Producto), producto.TiendaId, producto.Id, $"Imagen actualizada: {producto.ImagenUrl}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (anterior is not null && anterior.StartsWith(RutaImagenes, StringComparison.Ordinal))
        {
            var rutaAnterior = Path.Combine(carpeta, Path.GetFileName(anterior));
            if (System.IO.File.Exists(rutaAnterior)) System.IO.File.Delete(rutaAnterior);
        }
        return Ok(CrearRespuesta(producto));
    }

    private async Task<ActionResult<ProductoResponse>> CambiarEstado(long id, bool activo, CancellationToken cancellationToken)
    {
        var producto = await ObtenerDeTiendaAsync(id, cancellationToken);
        if (producto is null) return NotFound("Producto no encontrado.");
        producto.Activo = activo;
        auditoria.Registrar(User, activo ? AccionAuditoria.Reactivacion : AccionAuditoria.Baja, nameof(Producto), producto.TiendaId, producto.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(CrearRespuesta(producto));
    }

    private async Task<Producto?> ObtenerDeTiendaAsync(long id, CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirstValue("TiendaId"), out var tiendaId)) return null;
        var producto = await productos.ObtenerAsync(id, cancellationToken);
        return producto is not null && producto.TiendaId == tiendaId ? producto : null;
    }

    private static string? Validar(ProductoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Marca) || string.IsNullOrWhiteSpace(request.Descripcion) || string.IsNullOrWhiteSpace(request.UnidadMedida))
            return "Marca, descripción y unidad de medida son obligatorias.";
        if (request.PrecioContado <= 0 || request.PrecioLista <= 0) return "Los precios deben ser mayores que cero.";
        if (!request.PermiteFinDeMes && !request.PermiteCuotas) return "El producto debe permitir al menos una modalidad de crédito.";
        return null;
    }

    private static void Asignar(Producto producto, ProductoRequest request)
    {
        producto.Proveedor = string.IsNullOrWhiteSpace(request.Proveedor) ? null : request.Proveedor.Trim();
        producto.Marca = request.Marca.Trim();
        producto.Descripcion = request.Descripcion.Trim();
        producto.UnidadMedida = request.UnidadMedida.Trim();
        producto.PrecioContado = decimal.Round(request.PrecioContado, 2, MidpointRounding.AwayFromZero);
        producto.PrecioLista = decimal.Round(request.PrecioLista, 2, MidpointRounding.AwayFromZero);
        producto.PermiteFinDeMes = request.PermiteFinDeMes;
        producto.PermiteCuotas = request.PermiteCuotas;
    }

    private static async Task<bool> TieneFirmaValidaAsync(IFormFile archivo, string extension, CancellationToken cancellationToken)
    {
        var cabecera = new byte[12];
        await using var stream = archivo.OpenReadStream();
        var leidos = await stream.ReadAtLeastAsync(cabecera, cabecera.Length, false, cancellationToken);
        if (leidos < cabecera.Length) return false;
        return extension switch
        {
            ".jpg" => cabecera[0] == 0xFF && cabecera[1] == 0xD8 && cabecera[2] == 0xFF,
            ".png" => cabecera[0] == 0x89 && cabecera[1] == 0x50 && cabecera[2] == 0x4E && cabecera[3] == 0x47,
            ".webp" => cabecera[0] == 0x52 && cabecera[1] == 0x49 && cabecera[2] == 0x46 && cabecera[3] == 0x46 &&
                       cabecera[8] == 0x57 && cabecera[9] == 0x45 && cabecera[10] == 0x42 && cabecera[11] == 0x50,
            _ => false
        };
    }

    private static ProductoResponse CrearRespuesta(Producto producto) => new(
        producto.Id, producto.Proveedor, producto.Marca, producto.Descripcion, producto.UnidadMedida, producto.ImagenUrl,
        producto.PrecioContado, producto.PrecioLista, producto.PermiteFinDeMes, producto.PermiteCuotas, producto.Activo);
}
