namespace Finvex.Application;

public static class ValidacionesEntrada
{
    public const int PasswordMinimo = 8;
    public const int UsuarioMinimo = 5;
    public const int UsuarioMaximo = 50;
    public const int NombreMinimo = 2;
    public const int NombreMaximo = 100;
    public const int GiroMaximo = 150;
    public const int MarcaMaxima = 100;
    public const int DescripcionProductoMinima = 2;
    public const int DescripcionProductoMaxima = 100;
    public const int UnidadMedidaMinima = 2;
    public const int UnidadMedidaMaxima = 20;

    public static string? ValidarTienda(RegistrarAdminRequest request)
    {
        if (!EsDocumentoNumerico(request.Ruc, 11)) return "El RUC debe tener 11 dígitos.";
        if (!TieneLongitud(request.RazonSocial, NombreMinimo, NombreMaximo))
            return $"La razón social debe tener entre {NombreMinimo} y {NombreMaximo} caracteres.";
        if (!TieneLongitud(request.Giro, 1, GiroMaximo)) return $"El giro del negocio debe tener entre 1 y {GiroMaximo} caracteres.";
        if (!TieneLongitud(request.Usuario, UsuarioMinimo, UsuarioMaximo))
            return $"El usuario debe tener entre {UsuarioMinimo} y {UsuarioMaximo} caracteres.";
        if (request.Password is null || request.Password.Length < PasswordMinimo)
            return $"La contraseña debe tener al menos {PasswordMinimo} caracteres.";
        return null;
    }

    public static string? ValidarClienteNuevo(RegistrarClienteRequest request)
    {
        if (!EsDocumentoNumerico(request.Dni, 8)) return "El DNI debe tener 8 dígitos.";
        if (!TieneLongitud(request.Nombres, NombreMinimo, NombreMaximo))
            return $"Los nombres completos deben tener entre {NombreMinimo} y {NombreMaximo} caracteres.";
        if (!TieneLongitud(request.Usuario, UsuarioMinimo, UsuarioMaximo))
            return $"El usuario debe tener entre {UsuarioMinimo} y {UsuarioMaximo} caracteres.";
        if (request.Password is null || request.Password.Length < PasswordMinimo)
            return $"La contraseña debe tener al menos {PasswordMinimo} caracteres.";
        return null;
    }

    public static string? ValidarClienteActualizado(ActualizarClienteRequest request)
    {
        if (!EsDocumentoNumerico(request.Dni, 8)) return "El DNI debe tener 8 dígitos.";
        if (!TieneLongitud(request.Nombres, NombreMinimo, NombreMaximo))
            return $"Los nombres completos deben tener entre {NombreMinimo} y {NombreMaximo} caracteres.";
        if (request.Password is not null && request.Password.Length < PasswordMinimo)
            return $"La contraseña debe tener al menos {PasswordMinimo} caracteres.";
        return null;
    }

    public static string? ValidarProducto(ProductoRequest request)
    {
        if (!TieneLongitud(request.Marca, 1, MarcaMaxima)) return $"La marca debe tener entre 1 y {MarcaMaxima} caracteres.";
        if (!TieneLongitud(request.Descripcion, DescripcionProductoMinima, DescripcionProductoMaxima))
            return $"La descripción debe tener entre {DescripcionProductoMinima} y {DescripcionProductoMaxima} caracteres.";
        if (!TieneLongitud(request.UnidadMedida, UnidadMedidaMinima, UnidadMedidaMaxima))
            return $"La unidad de medida debe tener entre {UnidadMedidaMinima} y {UnidadMedidaMaxima} caracteres.";
        if (request.Proveedor?.Trim().Length > 150) return "El proveedor no debe superar 150 caracteres.";
        if (request.PrecioContado <= 0 || request.PrecioLista <= 0) return "Los precios deben ser mayores que cero.";
        if (!request.PermiteFinDeMes && !request.PermiteCuotas) return "El producto debe permitir al menos una modalidad de crédito.";
        return null;
    }

    private static bool EsDocumentoNumerico(string? valor, int longitud) =>
        valor is not null && valor.Trim().Length == longitud && valor.Trim().All(char.IsDigit);

    private static bool TieneLongitud(string? valor, int minimo, int maximo)
    {
        var longitud = valor?.Trim().Length ?? 0;
        return longitud >= minimo && longitud <= maximo;
    }
}
