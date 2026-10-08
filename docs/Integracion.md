# FINVEX — registro de integración

Fecha de validación: 8 de octubre de 2026.

## Resultado

La solución fue compilada y probada contra MySQL local en el puerto 3306, con API en `http://localhost:5105` y frontend en `http://localhost:5173`. No se modificó el esquema, no se agregaron migraciones y no se ejecutaron operaciones destructivas sobre `finvexdb`.

Resultados reproducibles:

- Backend: compilación sin errores ni advertencias; 38 pruebas xUnit aprobadas.
- Frontend: `npm ci` y `npm run build` aprobados.
- Swagger: accesible en `/swagger`.
- Juego 1: cuota `328.97`; pago con mora `331.67`.
- Juego 2: pago con mora `277.73`; la tercera compra quedó en el ciclo siguiente.
- Historial: un pago recuperado desde MySQL mediante la API.
- Separación: accesos cruzados entre dos tiendas rechazados con HTTP 403 para Admin y Cliente.
- Auditoría: dos operaciones de pago verificadas.
- Navegador: accesos Admin y Cliente, estado de cuenta y navegación sin errores críticos de consola.

El script `scripts/SmokeIntegration.ps1` crea exclusivamente datos ficticios y no elimina datos existentes.

## Matriz frontend/backend

Los nombres indicados corresponden al JSON camelCase que consume TypeScript.

| Vista/operación | Método y ruta | Rol | Entrada principal | Respuesta / DTO TypeScript | Errores y tratamiento visual | Persistencia |
| --- | --- | --- | --- | --- | --- | --- |
| Registro de tienda | `POST /api/auth/register/admin` | Público | `ruc, razonSocial, giro, usuario, password` | `RegistroResponse` | 400 de validación/duplicado; mensaje del API | Tiendas y credencial Admin |
| Login Admin | `POST /api/auth/login/admin` | Público | `usuario, password` | `LoginResponse` | 401; alerta en formulario | Auditoría de login |
| Login Cliente | `POST /api/auth/login/cliente` | Público | `usuario, password, tiendaRuc` | `LoginResponse` con `tiendaId` y `clienteId` | 400 sin RUC, 401 credenciales; mensaje en formulario | Auditoría de login |
| Login Sistema | `POST /api/auth/login/sistema` | Público | `usuario, password` | `LoginResponse` | 401; alerta en formulario | Auditoría de login |
| Clientes | `GET /api/clientes` | Admin | — | `ClienteListItem[]` | 401/403; alerta y estado vacío | Lectura |
| Crear cliente | `POST /api/clientes` | Admin | `RegistrarClienteRequest` | `RegistroResponse` | 400 con validaciones 1–28, tasas, DNI y plazo | Cliente y auditoría |
| Detalle de cliente | `GET /api/clientes/{id}` | Admin | Identificador | `ClienteDetalle` | 404/403; mensaje de error | Lectura |
| Editar cliente | `PUT /api/clientes/{id}` | Admin | `ActualizarClienteRequest` | `ClienteDetalle` | 400/404/403; mensaje del API | Cliente y auditoría |
| Baja/reactivación | `PUT /api/clientes/{id}/baja|alta` | Admin | Identificador | 204 | 404/403; confirmación/error | Cliente y auditoría |
| Productos | `GET /api/productos` | Admin | `incluirInactivos` | `Producto[]` | 401/403; alerta/estado vacío | Lectura |
| Crear/editar producto | `POST /api/productos`, `PUT /api/productos/{id}` | Admin | `ProductoRequest` | `Producto` | 400/404/403; mensaje del API | Producto y auditoría |
| Imagen de producto | `POST /api/productos/{id}/imagen` | Admin | multipart `archivo` JPG/PNG/WEBP ≤2 MB | `Producto` | 400 por formato/tamaño; mensaje | Archivo y URL en producto |
| Compra | `POST /api/clientes/{id}/compras` | Admin | `CrearCompraRequest` | `Compra` | 400 por crédito, inactividad, plazo o modalidad; mensaje | Compra, cronograma y auditoría |
| Estado de cuenta | `GET /api/clientes/{id}/estado-cuenta` | Admin, Cliente propio | Identificador | `EstadoCuenta` con `moneda` | 403 cruzado, 404; alerta/estado vacío | Lectura calculada |
| Historial de pagos | `GET /api/clientes/{id}/pagos` | Admin, Cliente propio | Identificador | `PagoHistorial[]` | 403 cruzado, 404; alerta/estado vacío | Lectura de Pagos |
| Pago exacto | `POST /api/clientes/{id}/pagos` | Admin | `monto, fechaPago` | `PagoResponse` | 400 por monto parcial/excedente; se muestra total indicado | Pago, saldos y auditoría |
| Consultar listado | `GET /api/clientes/{id}/listado-pago?fechaCorte=` | Admin, Cliente propio | Corte opcional | `ListadoPago` | 403/404/400; mensaje y estado vacío | Lee guardado o calcula |
| Generar listado | `POST /api/clientes/{id}/listado-pago/generar?fechaCorte=` | Admin | Corte opcional | `ListadoPago` | 400 si ciclo no cerró; mensaje | ListadoPago e ítems, idempotente |
| Tiendas | `GET|POST /api/sistema/tiendas` | AdminSistema | Alta: `RegistrarAdminRequest` | `Tienda[]` / `Tienda` | 400/401/403; alerta | Tienda y auditoría |
| Baja/reactivación tienda | `PUT /api/sistema/tiendas/{id}/baja|alta` | AdminSistema | Identificador | 204 | 404; mensaje | Tienda y auditoría |
| Auditoría | `GET /api/auditoria` | Admin, AdminSistema | filtros y paginación | `PaginaResponse<Operacion>` | 400/403; alerta | Lectura |

Todos los endpoints con datos de tienda validan el `TiendaId` del JWT en el backend. El cliente valida además su `ClienteId`; no se confía en ocultar controles en la interfaz.

## Reglas financieras verificadas

- Año comercial 360 y mes comercial 30.
- TEA: `TEM = (1 + TEA)^(30/360) - 1`.
- TNA del proyecto: `TEM = TNA / 360 × 30`.
- `TED = (1 + TEM)^(1/30) - 1`.
- Método francés vencido, gracia total capitalizada y ajuste de última cuota.
- Compra posterior al día y hora de corte trasladada al ciclo siguiente.
- Mora separada, pago exacto y prelación: mora, interés compensatorio, capital.
- Redondeo monetario a dos decimales y tasas con precisión suficiente para los resultados académicos.

## Seguridad y secretos

La conexión MySQL, la clave JWT y la credencial inicial de `AdminSistema` se retiraron de los archivos versionados. En desarrollo se cargan mediante .NET User Secrets y en despliegue mediante variables de entorno. La API falla al iniciar si faltan la conexión o una clave JWT de al menos 32 bytes.

Como esos valores estuvieron versionados previamente, deben rotarse fuera del repositorio:

1. contraseña del usuario MySQL expuesto;
2. clave de firma JWT expuesta;
3. contraseña inicial de `AdminSistema` expuesta.

No se reescribió el historial compartido de Git.

## Marco legal y afirmaciones del informe

El backend no valida todavía topes legales del BCRP. A octubre de 2026, la publicación oficial del BCRP para mayo–octubre de 2026 señala una tasa máxima compensatoria convencional de 114.13 % anual en soles y 99.84 % en dólares. La Circular 0008-2021-BCRP dispone, para operaciones entre personas ajenas al sistema financiero, que la tasa moratoria máxima equivale al 15 % de la máxima compensatoria y se aplica adicionalmente.

Fuentes oficiales:

- https://www.bcrp.gob.pe/docs/Transparencia/Notas-Informativas/2026/nota-informativa-2026-04-07.pdf
- https://www.bcrp.gob.pe/docs/Transparencia/Normas-Legales/Circulares/2021/circular-0008-2021-bcrp.pdf
- https://tasamaxima.bcrp.gob.pe/tasa-max-interes/web/public/index.php?accion=consultaPASF

El Juego 1 académico usa una TEA moratoria de 80 %, superior al 15 % de 114.13 % (aproximadamente 17.1195 %). Por ello no se agregó un tope rígido que rompería el escenario exigido. El informe debe cambiar cualquier afirmación de “cumplimiento legal” por una descripción de la ausencia de validación y de la necesidad de definir el ámbito jurídico exacto.

Recomendación pendiente de autorización: mantener límites configurables por moneda y periodo de vigencia, convertir correctamente tasas nominales/efectivas y congelar en cada compra el límite aplicable. Esto exige diseño y posiblemente cambios de esquema.

## Limitaciones que requieren autorización

1. **Condiciones históricas.** Tasas, corte y pago pertenecen al cliente; editarlos puede alterar cálculos de obligaciones previas. La solución correcta requiere guardar un snapshot por compra/listado y modificar el esquema.
2. **Concurrencia de pagos.** El proceso es transaccional, pero dos solicitudes simultáneas pueden competir antes del guardado. Se recomienda bloqueo de fila o token de concurrencia, lo que requiere una decisión de persistencia.
3. **Cortes tras interrupciones largas.** El servicio recupera el último corte cerrado al reiniciar y evita duplicados; no recorre automáticamente todos los meses omitidos.
4. **Topes BCRP.** No están aplicados por el conflicto descrito y porque deben versionarse por fecha, moneda y tipo de tasa.
5. **Informe fuente.** El archivo `1ASI0642-2620-7647_Informe_Grupo 2 (8).pdf` no estuvo disponible en los repositorios ni en los adjuntos accesibles. Esta validación usa el enunciado entregado, las pruebas y `docs/DatosDePrueba.md`; debe repetirse la comparación textual cuando se aporte el PDF.
6. **Dependencias frontend.** `npm audit` reporta 10 vulnerabilidades (4 moderadas y 6 altas), ninguna crítica. Las correcciones propuestas implican versiones mayores y deben probarse aparte.

## Reproducción

```powershell
# Backend
dotnet restore Finvex.sln
dotnet build Finvex.sln
dotnet test Finvex.sln
dotnet run --project Finvex.API --launch-profile http

# En otra terminal, frontend
npm ci
npm run dev

# Smoke real (con la API y MySQL locales activos)
.\scripts\SmokeIntegration.ps1
```

El smoke acepta parámetros para la URL base y usa identificadores únicos para no reemplazar registros existentes.
