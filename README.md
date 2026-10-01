# Finvex Backend

Backend transaccional para la gestión de créditos directos (fiados) en comercios minoristas peruanos.

## Stack

- .NET 9 / ASP.NET Core Web API
- Entity Framework Core
- MySQL 8+
- Pomelo EntityFrameworkCore MySQL
- JWT Bearer Authentication
- BCrypt para contraseñas
- Swagger / OpenAPI
- Clean Architecture: Domain, Application, Infrastructure y API

## Estructura

```text
Finvex.Domain          Entidades, enums y excepciones de dominio
Finvex.Application     DTOs, interfaces y motor financiero
Finvex.Infrastructure  DbContext, repositorios, auditoría y configuración EF Core
Finvex.API             Controladores, JWT, Swagger, CORS, archivos estáticos y generador diario de listados
Finvex.Tests           Pruebas xUnit del motor financiero
docs                   Datos de prueba para el informe (DatosDePrueba.md)
```

## Requisitos

Instalar:

- .NET SDK 9.x
- MySQL Server 8.x o compatible
- MySQL Workbench, opcional para administrar la base de datos

Verificar .NET:

```powershell
dotnet --version
```

El proyecto está configurado para `net9.0` porque es la versión disponible en el entorno actual.

## Configuración de MySQL

Iniciar el servicio de MySQL y verificar que escuche en el puerto `3306`.

La aplicación usa esta conexión por defecto en `Finvex.API/appsettings.json`:

```text
Server=localhost;Port=3306;Database=FinvexDB;Uid=root;Pwd=123456789;MaximumPoolSize=100;
```

La API ejecuta `EnsureCreated()` al arrancar. Si el usuario `root` tiene permisos suficientes, crea automáticamente la base `FinvexDB` y las tablas:

- `Tiendas`
- `Clientes`
- `Compras`
- `Cronogramas`
- `Pagos`
- `AdministradoresSistema`
- `Productos`
- `ListadosPago`
- `ItemsListadoPago`
- `Operaciones`

También se puede crear manualmente desde MySQL Workbench:

```sql
CREATE DATABASE IF NOT EXISTS FinvexDB;
```

> Para producción, reemplazar `EnsureCreated()` por migraciones EF Core y no almacenar credenciales ni claves JWT directamente en el repositorio.

### Reinicio de la base de datos tras actualizar el modelo

`EnsureCreated()` solo crea la base si no existe y nunca modifica tablas existentes. Esta versión agrega columnas (`Tiendas.Activo`; `Clientes.Moneda`, `MaxMeses`, `HoraCorte`; `Compras.ProductoId`, `Cantidad`) y tablas nuevas, por lo que una base creada con la versión anterior debe recrearse. Se pierden los datos existentes.

1. Detener el backend.
2. Ejecutar en MySQL:

```sql
DROP DATABASE FinvexDB;
CREATE DATABASE FinvexDB;
```

3. Iniciar el backend. `EnsureCreated()` crea todas las tablas y se siembra el administrador del sistema.

## Configuración adicional

Los valores de desarrollo están en `Finvex.API/appsettings.Development.json`. **Cambiarlos al desplegar.**

```json
{
  "SistemaAdmin": {
    "Usuario": "sistema",
    "Password": "Sistema-Dev-2026"
  },
  "Cors": {
    "AllowedOrigins": [ "http://localhost:5173" ]
  }
}
```

- `SistemaAdmin`: al arrancar se crea el administrador del sistema (rol `AdminSistema`) si no existe un usuario con ese nombre. Si falta la configuración, no se siembra y se registra un warning en el log. Cambiar la contraseña en la configuración no actualiza un administrador ya creado.
- `Cors:AllowedOrigins`: orígenes permitidos para el front. Si no se configura, se usa `http://localhost:5173`.

## Ejecutar localmente

Desde la raíz de la solución:

```powershell
dotnet restore
dotnet build Finvex.sln
dotnet run --project Finvex.API --launch-profile http
```

Swagger:

```text
http://localhost:5105/swagger
```

La API también tiene un perfil HTTPS en `https://localhost:7184`, sujeto a que el certificado de desarrollo esté instalado.

Para detener la aplicación:

```text
Ctrl + C
```

Si el puerto `5105` está ocupado:

```powershell
Get-NetTCPConnection -LocalPort 5105
Stop-Process -Id ID_DEL_PROCESO -Force
```

## Autenticación

Las rutas de autenticación son públicas. Las contraseñas se almacenan usando BCrypt y nunca deben guardarse en texto plano.

### Registrar una tienda / administrador

```http
POST /api/auth/register/admin
Content-Type: application/json
```

```json
{
  "ruc": "20123456789",
  "razonSocial": "Bodega Finvex SAC",
  "giro": "Comercio minorista",
  "usuario": "admin@finvex.com",
  "password": "CambiarEstaPassword"
}
```

El RUC y el usuario deben ser únicos.

### Iniciar sesión como administrador

```http
POST /api/auth/login/admin
Content-Type: application/json
```

```json
{
  "usuario": "admin@finvex.com",
  "password": "CambiarEstaPassword"
}
```

La respuesta incluye un JWT con rol `Admin` y claim `TiendaId`.

### Registrar un cliente

Requiere un token de administrador:

```http
POST /api/clientes
Authorization: Bearer TOKEN_ADMIN
Content-Type: application/json
```

```json
{
  "dni": "12345678",
  "nombres": "Juan Perez",
  "limiteCredito": 1500.00,
  "tipoTasa": "Efectiva",
  "tasaCompensatoria": 0.45,
  "tasaMoratoria": 0.10,
  "diaCorte": 25,
  "diaPago": 28,
  "usuario": "juan.perez",
  "password": "CambiarEstaPassword"
}
```

El cliente se vincula automáticamente con el `TiendaId` del token. El DNI y el usuario deben ser únicos dentro de la tienda.

### Iniciar sesión como cliente

```http
POST /api/auth/login/cliente
Content-Type: application/json
```

```json
{
  "usuario": "juan.perez",
  "password": "CambiarEstaPassword"
}
```

La respuesta incluye un JWT con rol `Cliente` y claim `ClienteId`.

### Iniciar sesión como administrador del sistema

```http
POST /api/auth/login/sistema
Content-Type: application/json
```

```json
{
  "usuario": "sistema",
  "password": "Sistema-Dev-2026"
}
```

La respuesta incluye un JWT con rol `AdminSistema` y claim `AdminSistemaId`.

### Roles

| Rol | Claim | Alcance |
|---|---|---|
| `AdminSistema` | `AdminSistemaId` | Alta, baja y reactivación de tiendas y auditoría global |
| `Admin` | `TiendaId` | Clientes, productos, compras, pagos, listados y auditoría de su propia tienda |
| `Cliente` | `ClienteId` | Su estado de cuenta y su listado de pago |

Una tienda dada de baja no puede iniciar sesión. Un cliente dado de baja tampoco puede iniciar sesión ni registrar compras.

### Formato de los datos

- Los enums se envían y se devuelven como texto (`"Efectiva"`, `"FinDeMes"`, `"Cuotas"`, `"PEN"`), y en la entrada también se aceptan números.
- Las tasas se guardan como fracción: `0.05` = 5 %.
- `horaCorte` usa el formato `hh:mm:ss`.
- La fecha actual del sistema es la hora de Lima (America/Lima, UTC−5). Si una compra no trae `fechaCompra`, se usan la fecha y la hora actuales de Lima.

## Endpoints protegidos

En Swagger, ejecutar primero un login, copiar el campo `token`, pulsar **Authorize** y pegarlo con el formato:

```text
Bearer TOKEN_JWT
```

| Método | Ruta | Rol | Descripción |
|---|---|---|---|
| POST | `/api/auth/register/admin` | Público | Registra una tienda administradora |
| POST | `/api/auth/login/admin` | Público | Login de administrador |
| POST | `/api/auth/login/cliente` | Público | Login de cliente |
| POST | `/api/clientes` | Admin | Registra un cliente en la tienda autenticada |
| POST | `/api/clientes/{clienteId}/compras` | Admin | Registra una compra y genera cronograma si corresponde |
| GET | `/api/clientes/{clienteId}/estado-cuenta` | Admin, Cliente | Consulta compras, intereses, mora y deuda exigible |
| POST | `/api/clientes/{clienteId}/pagos` | Admin | Registra un pago exacto con prelación Mora, Interés y Capital |
| POST | `/api/auth/login/sistema` | Público | Login del administrador del sistema |
| GET | `/api/clientes` | Admin | Lista los clientes de la tienda con su deuda de capital |
| GET | `/api/clientes/{clienteId}` | Admin | Detalle y condiciones de crédito del cliente |
| PUT | `/api/clientes/{clienteId}` | Admin | Actualiza datos y condiciones de crédito |
| PUT | `/api/clientes/{clienteId}/baja` | Admin | Baja lógica del cliente |
| PUT | `/api/clientes/{clienteId}/alta` | Admin | Reactiva al cliente |
| GET | `/api/clientes/{clienteId}/listado-pago?fechaCorte=AAAA-MM-DD` | Admin, Cliente | Listado de pago del ciclo, calculado a hoy |
| POST | `/api/clientes/{clienteId}/listado-pago/generar?fechaCorte=AAAA-MM-DD` | Admin | Guarda el listado de un ciclo cerrado (idempotente) |
| GET | `/api/productos?incluirInactivos=false` | Admin | Lista los productos de la tienda |
| GET | `/api/productos/{id}` | Admin | Detalle de un producto |
| POST | `/api/productos` | Admin | Registra un producto |
| PUT | `/api/productos/{id}` | Admin | Actualiza un producto |
| PUT | `/api/productos/{id}/baja` | Admin | Baja lógica del producto |
| PUT | `/api/productos/{id}/alta` | Admin | Reactiva el producto |
| POST | `/api/productos/{id}/imagen` | Admin | Sube la imagen (multipart, campo `archivo`; JPG, PNG o WEBP de hasta 2 MB) |
| GET | `/api/sistema/tiendas` | AdminSistema | Lista las tiendas |
| POST | `/api/sistema/tiendas` | AdminSistema | Registra una tienda con RUC, razón social, giro, usuario y contraseña |
| PUT | `/api/sistema/tiendas/{id}/baja` | AdminSistema | Baja lógica de la tienda |
| PUT | `/api/sistema/tiendas/{id}/alta` | AdminSistema | Reactiva la tienda |
| GET | `/api/auditoria?desde=&hasta=&accion=&pagina=1&tamanoPagina=20` | Admin, AdminSistema | Operaciones auditadas, paginadas |

Los administradores solo pueden operar clientes y productos de su propia tienda. Los clientes solo pueden consultar su propio estado de cuenta y su listado de pago.

### Campos nuevos opcionales

- `POST /api/clientes` acepta al final `moneda` (`PEN` por defecto), `maxMeses` (1 a 36, 1 por defecto) y `horaCorte` (`23:59:59` por defecto). Se valida que la tasa compensatoria sea mayor que 0, que la moratoria sea mayor o igual a 0 (0 significa que se usa la compensatoria), que el día de corte y el de pago estén entre 1 y 28, y que el plazo máximo esté entre 1 y 36.
- `POST /api/clientes/{clienteId}/compras` acepta al final `productoId` y `cantidad` (1 por defecto). Con `productoId`, el precio es `PrecioLista × cantidad` y la descripción es la del producto. El producto debe pertenecer a la tienda, estar activo y permitir la modalidad. Sin `productoId`, la compra funciona como antes.

### Listado de pago

- `fechaCorte` es opcional. Si se omite, se usa el último corte cerrado; cualquier fecha se asocia al corte de su ciclo.
- Ítems:
  - `Compra`: las compras del ciclo ordenadas por fecha, con capital, días e interés compensatorio. Las compras en cuotas se muestran como informativas, con monto 0.
  - `Cuota`: las cuotas que vencen en la fecha de pago del ciclo.
  - `InteresMora`: un ítem aparte llamado "Intereses por mora" cuando hay días de mora.
- Un servicio en segundo plano revisa cada 15 minutos, en hora de Lima, a los clientes cuyo corte es hoy y genera su listado una vez pasada su hora de corte.

### Auditoría

Se registran los logins (correctos y fallidos), las altas, bajas, reactivaciones y ediciones de tiendas, clientes y productos, y las compras y los pagos. Cada operación se guarda en la misma transacción que la acción auditada. Valores de `accion`: `LoginCorrecto`, `LoginFallido`, `Alta`, `Edicion`, `Baja`, `Reactivacion`, `Compra` y `Pago`. Los filtros `desde` y `hasta` se interpretan en hora de Lima y ambos son inclusivos.

## Reglas financieras

El motor financiero usa año comercial de 360 días y mes comercial de 30 días.

- TNA: `TEM = (TNA / 360) * 30`
- TEA: `TEM = (1 + TEA)^(30 / 360) - 1`
- TED: `TED = (1 + TEM)^(1 / 30) - 1`
- Método francés para cuotas constantes.
- Capitalización de intereses durante la gracia total.
- Interés compensatorio calculado por días.
- Interés moratorio después de la fecha de vencimiento.
- Prelación de pagos: mora, interés compensatorio y capital.
- Ciclo: el corte es el día `DiaCorte` a la `HoraCorte` del cliente. Una compra posterior al corte pasa al ciclo del mes siguiente.
- Fecha de pago P: en el mismo mes del corte si `DiaPago >= DiaCorte`; si no, en el mes siguiente.
- FinDeMes: `interés = saldo × ((1 + TED)^días − 1)`, donde los días van de la compra a P. Vence en P.
- Cuotas: los días de gracia van de la compra a P. El capital capitalizado es `precio × (1 + TED)^días`, y las cuotas vencen en P + 1 mes, P + 2 meses, etc.
- Mora: `total vencido × ((1 + TEDmora)^díasMora − 1)`, calculada por cada vencimiento.
- Los pagos deben ser exactos e iguales al total exigible a la fecha de pago. Se rechazan los pagos parciales, los excedentes y los pagos sin monto exigible.
- Estado de cuenta: `totalExigible` es el exigible a hoy si hay vencidos y, si no, el monto de la próxima fecha de pago. `fechaCorte` es el corte del ciclo abierto.

Los casos numéricos completos están en `docs/DatosDePrueba.md`.

## Despliegue

### Publicar

En la máquina de despliegue:

```powershell
dotnet restore
dotnet publish Finvex.API/Finvex.API.csproj -c Release -o publish
```

### Configurar producción

Usar variables de entorno en lugar de contraseñas dentro de `appsettings.json`:

```powershell
$env:ConnectionStrings__Finvex="Server=SERVIDOR_MYSQL;Port=3306;Database=FinvexDB;Uid=USUARIO;Pwd=CONTRASENA;MaximumPoolSize=100;"
$env:Jwt__Key="CLAVE_JWT_LARGA_Y_SEGURA"
$env:Jwt__Issuer="Finvex"
$env:Jwt__Audience="Finvex.Clients"
$env:Jwt__ExpirationMinutes="60"
$env:SistemaAdmin__Usuario="USUARIO_ADMIN_SISTEMA"
$env:SistemaAdmin__Password="CONTRASENA_SEGURA"
$env:Cors__AllowedOrigins__0="https://DOMINIO_DEL_FRONT"
$env:ASPNETCORE_ENVIRONMENT="Production"
```

Ejecutar la publicación:

```powershell
dotnet publish Finvex.API/Finvex.API.csproj -c Release -o publish
dotnet .\publish\Finvex.API.dll
```

Para un servidor Linux, el último comando sería:

```bash
dotnet publish/Finvex.API.dll
```

Configurar un reverse proxy como IIS, Nginx o Apache para exponer la API con HTTPS.

## Solución de problemas

### Falta el runtime

Si aparece `Framework 'Microsoft.NETCore.App', version '9.0.0'`, instalar el SDK o runtime de .NET 9:

```text
https://dotnet.microsoft.com/download/dotnet/9.0
```

### Puerto ocupado

Usar otro puerto temporalmente:

```powershell
dotnet run --project Finvex.API --urls "http://localhost:5200"
```

Swagger quedará en:

```text
http://localhost:5200/swagger
```

### MySQL no conecta

Comprobar:

1. Que el servicio MySQL esté iniciado.
2. Que el puerto sea `3306`.
3. Que el usuario tenga permisos sobre `FinvexDB`.
4. Que la contraseña coincida con la configuración.
5. Que MySQL permita conexiones desde el servidor de la API.

## Validación

```powershell
dotnet build Finvex.sln --no-restore
dotnet test Finvex.Tests
```

La compilación debe finalizar sin errores y todas las pruebas deben pasar.

Las imágenes de productos se guardan en `Finvex.API/wwwroot/uploads/productos` y se sirven en `/uploads/productos/...`. En producción, esa carpeta debe persistir entre despliegues.
