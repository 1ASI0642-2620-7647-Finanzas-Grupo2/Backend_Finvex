# FINVEX Backend

API transaccional para créditos directos de comercios minoristas, desarrollada con .NET 9, ASP.NET Core, Entity Framework Core, MySQL, JWT y BCrypt.

## Arquitectura

```text
Finvex.Domain          Entidades y reglas de dominio
Finvex.Application     DTO, interfaces, validaciones y motor financiero
Finvex.Infrastructure  Persistencia, repositorios y auditoría
Finvex.API             Controladores, autorización, configuración HTTP y tareas periódicas
Finvex.Tests           Pruebas xUnit financieras y de límites
docs                   Datos académicos y registro de integración
```

Se conserva el esquema existente `finvexdb` de diez tablas y ocho claves foráneas. No ejecute migraciones, `DROP`, `TRUNCATE` ni recree la base sin autorización expresa.

## Requisitos

- .NET SDK 9.x.
- MySQL Server 8.x escuchando en `localhost:3306`.
- Base existente `finvexdb`.
- Node.js 18+ y npm 9+ únicamente para ejecutar el frontend.

## Configuración segura

Los secretos no se almacenan en `appsettings*.json`. Para desarrollo configure .NET User Secrets:

```powershell
dotnet user-secrets init --project Finvex.API
dotnet user-secrets set "ConnectionStrings:Finvex" "Server=localhost;Port=3306;Database=finvexdb;Uid=USUARIO_LOCAL;Pwd=CONTRASENA_LOCAL;MaximumPoolSize=100;" --project Finvex.API
dotnet user-secrets set "Jwt:Key" "CLAVE_ALEATORIA_DE_AL_MENOS_32_BYTES" --project Finvex.API
dotnet user-secrets set "SistemaAdmin:Usuario" "USUARIO_LOCAL" --project Finvex.API
dotnet user-secrets set "SistemaAdmin:Password" "CONTRASENA_LOCAL_SEGURA" --project Finvex.API
```

La API rechaza el arranque si no existe una conexión o si la clave JWT tiene menos de 32 bytes. Cambiar la configuración del usuario de sistema no cambia una cuenta ya sembrada.

En producción use un gestor de secretos o variables de entorno:

```powershell
$env:ConnectionStrings__Finvex="Server=SERVIDOR;Port=3306;Database=finvexdb;Uid=USUARIO;Pwd=CONTRASENA;MaximumPoolSize=100;"
$env:Jwt__Key="CLAVE_ALEATORIA_DE_AL_MENOS_32_BYTES"
$env:Jwt__Issuer="Finvex"
$env:Jwt__Audience="Finvex.Clients"
$env:SistemaAdmin__Usuario="USUARIO_SISTEMA"
$env:SistemaAdmin__Password="CONTRASENA_SEGURA"
$env:Cors__AllowedOrigins__0="https://frontend.example"
```

Credenciales que hayan aparecido en versiones anteriores del repositorio deben rotarse: contraseña MySQL, clave JWT y contraseña inicial de `AdminSistema`. No se reescribió el historial compartido.

## Compilar, probar y ejecutar

Desde la raíz:

```powershell
dotnet restore Finvex.sln
dotnet build Finvex.sln
dotnet test Finvex.sln
dotnet run --project Finvex.API --launch-profile http
```

Servicios locales:

| Servicio | Dirección |
| --- | --- |
| API | `http://localhost:5105` |
| Swagger | `http://localhost:5105/swagger` |
| Frontend | `http://localhost:5173` |
| MySQL | `localhost:3306` |

El frontend redirige `/api` y `/uploads` a la API. El origen local permitido se configura en `Cors:AllowedOrigins`.

## Roles y autenticación

| Rol | Claim de alcance | Responsabilidad |
| --- | --- | --- |
| `AdminSistema` | `AdminSistemaId` | Tiendas y auditoría global |
| `Admin` | `TiendaId` | Datos de su propia tienda |
| `Cliente` | `ClienteId`, `TiendaId` | Solo su propia información |

Rutas públicas:

- `POST /api/auth/register/admin`
- `POST /api/auth/login/admin`
- `POST /api/auth/login/cliente`
- `POST /api/auth/login/sistema`

El login de cliente necesita el contexto de tienda:

```json
{
  "usuario": "cliente.demo",
  "password": "Contrasena-Local!",
  "tiendaRuc": "20601234567"
}
```

Esto mantiene compatibilidad con la unicidad `TiendaId + Usuario`. El JWT del cliente incluye ambos identificadores y los controladores verifican pertenencia real.

## Endpoints principales

| Método | Ruta | Rol |
| --- | --- | --- |
| `GET/POST` | `/api/clientes` | Admin |
| `GET/PUT` | `/api/clientes/{id}` | Admin |
| `PUT` | `/api/clientes/{id}/baja|alta` | Admin |
| `POST` | `/api/clientes/{id}/compras` | Admin |
| `GET` | `/api/clientes/{id}/estado-cuenta` | Admin, Cliente propio |
| `GET` | `/api/clientes/{id}/pagos` | Admin, Cliente propio |
| `POST` | `/api/clientes/{id}/pagos` | Admin |
| `GET` | `/api/clientes/{id}/listado-pago` | Admin, Cliente propio |
| `POST` | `/api/clientes/{id}/listado-pago/generar` | Admin |
| `GET/POST/PUT` | `/api/productos...` | Admin |
| `GET/POST/PUT` | `/api/sistema/tiendas...` | AdminSistema |
| `GET` | `/api/auditoria` | Admin, AdminSistema |

Swagger documenta cuerpos y respuestas. La matriz completa con DTO TypeScript, errores y persistencia está en [docs/Integracion.md](docs/Integracion.md).

## Reglas de entrada

- DNI: ocho dígitos; RUC: once dígitos.
- Contraseña: mínimo seis caracteres.
- Días de corte y pago: enteros de 1 a 28.
- Hora de corte por defecto: `23:59:59`.
- Tasa compensatoria: mayor que cero.
- Tasa moratoria: mayor o igual a cero; cero usa la compensatoria para el cálculo de mora según la regla actual.
- Tasas enviadas como fracción: `0.36` representa 36 %.
- Moneda: `PEN` o `USD`.
- Plazo máximo: 1 a 36 meses.
- Una compra exige cliente/producto activos, modalidad permitida y crédito disponible.
- Un pago debe ser exactamente igual a la obligación exigible.

## Motor financiero

- Año 360 y mes 30.
- TNA: `TEM = TNA / 360 × 30`.
- TEA: `TEM = (1 + TEA)^(30/360) - 1`.
- `TED = (1 + TEM)^(1/30) - 1`.
- Cuotas con método francés vencido y gracia total capitalizada.
- Compras posteriores al corte pasan al ciclo siguiente.
- Mora separada y prelación: mora, interés compensatorio, capital.
- Montos a dos decimales y pagos exactos.

Los resultados de referencia están en [docs/DatosDePrueba.md](docs/DatosDePrueba.md). Las limitaciones legales e históricas están documentadas en [docs/Integracion.md](docs/Integracion.md); este software no debe presentarse como validación jurídica total.

## Listados automáticos

El servicio periódico trabaja con hora de Lima, detecta el último corte cerrado de los clientes activos y genera cada listado de forma idempotente. Así recupera el corte más reciente después de un reinicio; no recorre todos los meses omitidos durante una interrupción larga.

## Prueba de integración

Con MySQL y la API activos:

```powershell
.\scripts\SmokeIntegration.ps1
```

El script usa datos ficticios únicos y verifica Swagger, productos, clientes, ambos juegos financieros, cronograma, listado, pago exacto, historial persistido, separación entre tiendas y auditoría. No elimina ni reinicia información existente.

## Flujo de demostración

1. Inicie MySQL, API y frontend.
2. Registre una tienda ficticia e ingrese como Admin.
3. Cree productos y un cliente.
4. Registre una compra FinDeMes y otra en Cuotas.
5. Revise estado de cuenta, cronograma y listado.
6. Registre el monto exacto y verifique historial.
7. Ingrese como Cliente usando usuario, contraseña y RUC.
8. Compruebe aislamiento con una segunda tienda y revise auditoría.

## Limitaciones conocidas

- Las condiciones de crédito no quedan congeladas por compra; corregirlo requiere una ampliación aprobada del esquema.
- Dos pagos realmente simultáneos requieren una estrategia explícita de bloqueo o control de concurrencia.
- Los topes BCRP no se aplican automáticamente por fecha/moneda y el Juego 1 académico entra en conflicto con el límite moratorio revisado.
- El informe PDF solicitado no estuvo disponible durante la revisión; consulte el detalle en `docs/Integracion.md`.

## Publicación

```powershell
dotnet publish Finvex.API/Finvex.API.csproj -c Release -o publish
dotnet .\publish\Finvex.API.dll
```

Use HTTPS, un reverse proxy y almacenamiento persistente para `Finvex.API/wwwroot/uploads/productos`.
