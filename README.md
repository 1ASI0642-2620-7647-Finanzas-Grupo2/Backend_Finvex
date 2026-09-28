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
Finvex.Infrastructure  DbContext, repositorios y configuración EF Core
Finvex.API             Controladores, JWT, Swagger y composición
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

También se puede crear manualmente desde MySQL Workbench:

```sql
CREATE DATABASE IF NOT EXISTS FinvexDB;
```

> Para producción, reemplazar `EnsureCreated()` por migraciones EF Core y no almacenar credenciales ni claves JWT directamente en el repositorio.

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
| POST | `/api/clientes/{clienteId}/pagos` | Admin | Registra pago con prelación Mora, Interés y Capital |

Los administradores solo pueden operar clientes pertenecientes a su propia tienda. Los clientes solo pueden consultar su propio estado de cuenta.

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
```

La compilación debe finalizar sin errores.
