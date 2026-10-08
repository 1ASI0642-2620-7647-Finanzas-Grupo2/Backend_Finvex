param([string]$BaseUrl = "http://localhost:5105")

$ErrorActionPreference = "Stop"

function Invoke-Finvex {
    param([string]$Method, [string]$Path, $Body = $null, [string]$Token = "")
    $parameters = @{ Uri = "$BaseUrl$Path"; Method = $Method; ContentType = "application/json" }
    if ($Token) { $parameters.Headers = @{ Authorization = "Bearer $Token" } }
    if ($null -ne $Body) { $parameters.Body = $Body | ConvertTo-Json -Depth 8 }
    Invoke-RestMethod @parameters
}

function Assert-Equal($Expected, $Actual, [string]$Message) {
    if ($Expected -ne $Actual) { throw "$Message. Esperado: $Expected; obtenido: $Actual" }
}

function Assert-Status([int]$Expected, [string]$Method, [string]$Path, [string]$Token) {
    try {
        $null = Invoke-WebRequest -Uri "$BaseUrl$Path" -Method $Method -Headers @{ Authorization = "Bearer $Token" }
        $actual = 200
    }
    catch { $actual = [int]$_.Exception.Response.StatusCode }
    Assert-Equal $Expected $actual "Código HTTP inesperado para $Method $Path"
}

$suffix = Get-Random -Minimum 100000000 -Maximum 999999999
$suffix2 = if ($suffix -eq 999999999) { $suffix - 1 } else { $suffix + 1 }
$ruc1 = "20$suffix"
$ruc2 = "20$suffix2"
$password = "Smoke-2026!"
$sharedUser = "smoke-cliente-$suffix"
$otherUser = "smoke-cliente-$suffix2"

$swagger = Invoke-WebRequest -Uri "$BaseUrl/swagger/index.html"
Assert-Equal 200 $swagger.StatusCode "Swagger no respondió"

$store1 = Invoke-Finvex POST "/api/auth/register/admin" @{
    ruc = $ruc1; razonSocial = "FINVEX Smoke Uno $suffix"; giro = "Bodega"; usuario = "smoke-admin-$suffix"; password = $password
}
$login1 = Invoke-Finvex POST "/api/auth/login/admin" @{ usuario = "smoke-admin-$suffix"; password = $password }
$admin1 = $login1.token

$product1 = Invoke-Finvex POST "/api/productos" @{
    marca = "Prueba"; descripcion = "Refrigeradora"; unidadMedida = "unidad"; precioContado = 850; precioLista = 900
    permiteFinDeMes = $true; permiteCuotas = $true
} $admin1
$client1 = Invoke-Finvex POST "/api/clientes" @{
    dni = "40000001"; nombres = "Cliente Juego Uno"; limiteCredito = 2000; tipoTasa = "Efectiva"
    tasaCompensatoria = 0.60; tasaMoratoria = 0.80; diaCorte = 20; diaPago = 26
    usuario = $sharedUser; password = $password; moneda = "PEN"; maxMeses = 6; horaCorte = "23:59:59"
} $admin1
$purchase1 = Invoke-Finvex POST "/api/clientes/$($client1.id)/compras" @{
    producto = "Refrigeradora"; precioCredito = 900; modalidad = "Cuotas"; plazoMeses = 3
    fechaCompra = "2026-09-15T10:30:00"; productoId = $product1.id; cantidad = 1
} $admin1
$statement1 = Invoke-Finvex GET "/api/clientes/$($client1.id)/estado-cuenta" $null $admin1
Assert-Equal 328.97 ([decimal]$statement1.totalExigible) "La primera cuota del juego 1 no coincide"
$schedule = @($statement1.compras | Where-Object compraId -eq $purchase1.id).cuotas
Assert-Equal 3 $schedule.Count "El cronograma del juego 1 debe tener tres cuotas"
Assert-Equal "2026-10-26" ([datetime]$schedule[0].vencimiento).ToString("yyyy-MM-dd") "Primer vencimiento incorrecto"
$null = Invoke-Finvex POST "/api/clientes/$($client1.id)/listado-pago/generar?fechaCorte=2026-09-20" $null $admin1
$payment1 = Invoke-Finvex POST "/api/clientes/$($client1.id)/pagos" @{ monto = 331.67; fechaPago = "2026-10-31" } $admin1
Assert-Equal 2.70 ([decimal]$payment1.imputacionMora) "Mora del juego 1 incorrecta"
$history1 = @(Invoke-Finvex GET "/api/clientes/$($client1.id)/pagos" $null $admin1)
Assert-Equal 1 $history1.Count "El pago del juego 1 no quedó persistido"

$client2 = Invoke-Finvex POST "/api/clientes" @{
    dni = "40000002"; nombres = "Cliente Juego Dos"; limiteCredito = 500; tipoTasa = "Nominal"
    tasaCompensatoria = 0.36; tasaMoratoria = 0.48; diaCorte = 25; diaPago = 5
    usuario = "smoke-juego2-$suffix"; password = $password; moneda = "PEN"; maxMeses = 1; horaCorte = "23:59:59"
} $admin1
$prices = @(150, 120, 80)
$names = @("Arroz 50 kg", "Aceite caja", "Azúcar 10 kg")
$dates = @("2026-03-10T09:00:00", "2026-03-25T23:00:00", "2026-03-26T08:00:00")
for ($index = 0; $index -lt 3; $index++) {
    $product = Invoke-Finvex POST "/api/productos" @{
        marca = "Prueba"; descripcion = $names[$index]; unidadMedida = "unidad"; precioContado = $prices[$index]
        precioLista = $prices[$index]; permiteFinDeMes = $true; permiteCuotas = $false
    } $admin1
    $null = Invoke-Finvex POST "/api/clientes/$($client2.id)/compras" @{
        producto = $names[$index]; precioCredito = $prices[$index]; modalidad = "FinDeMes"; plazoMeses = 1
        fechaCompra = $dates[$index]; productoId = $product.id; cantidad = 1
    } $admin1
}
$payment2 = Invoke-Finvex POST "/api/clientes/$($client2.id)/pagos" @{ monto = 277.73; fechaPago = "2026-04-12" } $admin1
Assert-Equal 2.53 ([decimal]$payment2.imputacionMora) "Mora del juego 2 incorrecta"
Assert-Equal 270.00 ([decimal]$payment2.imputacionCapital) "Capital del juego 2 incorrecto"

$null = Invoke-Finvex POST "/api/auth/register/admin" @{
    ruc = $ruc2; razonSocial = "FINVEX Smoke Dos $suffix"; giro = "Bodega"; usuario = "smoke-admin-$suffix2"; password = $password
}
$login2 = Invoke-Finvex POST "/api/auth/login/admin" @{ usuario = "smoke-admin-$suffix2"; password = $password }
$admin2 = $login2.token
$clientOtherStore = Invoke-Finvex POST "/api/clientes" @{
    dni = "40000001"; nombres = "Cliente Homónimo Otra Tienda"; limiteCredito = 100; tipoTasa = "Nominal"
    tasaCompensatoria = 0.36; tasaMoratoria = 0; diaCorte = 20; diaPago = 26
    usuario = $otherUser; password = $password; moneda = "PEN"; maxMeses = 1
} $admin2
$clientLogin1 = Invoke-Finvex POST "/api/auth/login/cliente" @{ usuario = $sharedUser; password = $password }
$clientLogin2 = Invoke-Finvex POST "/api/auth/login/cliente" @{ usuario = $otherUser; password = $password }
Assert-Equal $client1.id $clientLogin1.clienteId "El usuario no resolvió al cliente de la tienda uno"
Assert-Equal $clientOtherStore.id $clientLogin2.clienteId "El usuario no resolvió al cliente de la tienda dos"
Assert-Status 403 GET "/api/clientes/$($client1.id)" $admin2
Assert-Status 403 GET "/api/clientes/$($clientOtherStore.id)/estado-cuenta" $clientLogin1.token

$audit = Invoke-Finvex GET "/api/auditoria?accion=Pago&pagina=1&tamanoPagina=20" $null $admin1
if ($audit.totalRegistros -lt 2) { throw "La auditoría no contiene los pagos de la prueba." }

[pscustomobject]@{
    swagger = "OK"
    tienda1Id = $store1.id
    tienda2Id = $login2.tiendaId
    juego1Cuota = [decimal]$statement1.totalExigible
    juego1PagoConMora = [decimal]$payment1.monto
    juego2PagoConMora = [decimal]$payment2.monto
    historialPersistido = $history1.Count
    separacionTiendas = "HTTP 403 verificado"
    auditoriaPagos = $audit.totalRegistros
} | ConvertTo-Json
