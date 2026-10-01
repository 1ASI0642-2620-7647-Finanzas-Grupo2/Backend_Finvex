# Datos de prueba del motor financiero

Ambos juegos están automatizados en `Finvex.Tests/JuegosDePruebaTests.cs`, y sus valores se verificaron con un cálculo independiente de las fórmulas. Convenciones del motor:

- Año comercial de 360 días y mes comercial de 30 días.
- Tasa nominal: `TEM = TN / 360 × 30`. Tasa efectiva: `TEM = (1 + TEA)^(30/360) − 1`.
- `TED = (1 + TEM)^(1/30) − 1`. TEM y TED se redondean a 7 decimales y los montos a 2 decimales (redondeo comercial, AwayFromZero).
- Interés por días: `saldo × ((1 + TED)^días − 1)`.
- Mora: `total vencido × ((1 + TEDmora)^díasMora − 1)`. Si la tasa moratoria es 0 se usa la compensatoria.
- Las tasas se registran como fracción: `0.60` = 60 %.

## Juego 1: compra en cuotas con período de gracia y mora

### Entradas

| Dato | Valor |
|---|---|
| Tipo de tasa | Efectiva (TEA) |
| Tasa compensatoria | 0.60 (60 % TEA) |
| Tasa moratoria | 0.80 (80 % TEA) |
| Día de corte | 20, hora de corte 23:59:59 |
| Día de pago | 26 |
| Límite de crédito | 2 000.00 |
| Plazo máximo | 6 meses |
| Compra | Refrigeradora, 900.00, 15/09/2026 10:30, 3 cuotas |

### Tasas

| Tasa | Valor |
|---|---|
| TEM compensatoria | 0.0399441 |
| TED compensatoria | 0.0013064 |
| TEM moratoria | 0.0502017 |
| TED moratoria | 0.0016341 |

### Ciclo y período de gracia

- La compra (15/09) es anterior al corte del 20/09 23:59:59, así que pertenece a ese ciclo.
- Como el día de pago (26) es mayor o igual al de corte (20), la fecha de pago P es el 26/09/2026.
- Días de gracia: del 15/09 al 26/09 = 11 días.
- Capital capitalizado: `900 × (1.0013064)^11` = 913.02, con un interés de gracia de 13.02.
- Cuota francesa: `913.02 × 0.0399441 × (1.0399441)^3 / ((1.0399441)^3 − 1)` = 328.97.

### Cronograma

| N.º | Vencimiento | Saldo inicial | Interés | Amortización | Cuota |
|---|---|---|---|---|---|
| 1 | 26/10/2026 | 913.02 | 36.47 | 292.50 | 328.97 |
| 2 | 26/11/2026 | 620.52 | 24.79 | 304.18 | 328.97 |
| 3 | 26/12/2026 | 316.34 | 12.64 | 316.34 | 328.98 |
| | Total | | 73.90 | 913.02 | 986.92 |

La última cuota absorbe la diferencia de redondeo (0.01).

### Pagos

| Fecha de pago | Exigible | Días de mora | Mora | Interés | Capital | Monto exacto |
|---|---|---|---|---|---|---|
| 31/10/2026 | Cuota 1 | 5 | 2.70 | 36.47 | 292.50 | 331.67 |
| 26/11/2026 | Cuota 2 | 0 | 0.00 | 24.79 | 304.18 | 328.97 |
| 26/12/2026 | Cuota 3 | 0 | 0.00 | 12.64 | 316.34 | 328.98 |
| | Total | | 2.70 | 73.90 | 913.02 | 989.62 |

Mora de la cuota 1: `328.97 × ((1.0016341)^5 − 1)` = 2.70.

Casos de rechazo:

- El 31/10 se rechaza un pago de 331.66 (parcial) y uno de 331.68 (excedente). En ambos casos el mensaje indica el monto exacto, 331.67.
- Una compra en cuotas a 7 meses se rechaza porque supera el plazo máximo de 6.

## Juego 2: compras a fin de mes, compra posterior al corte y mora

### Entradas

| Dato | Valor |
|---|---|
| Tipo de tasa | Nominal (TNA) |
| Tasa compensatoria | 0.36 (36 % TNA) |
| Tasa moratoria | 0.48 (48 % TNA) |
| Día de corte | 25, hora de corte 23:59:59 |
| Día de pago | 5 |
| Límite de crédito | 500.00 |
| Plazo máximo | 1 mes |

| Compra | Fecha | Precio | Modalidad |
|---|---|---|---|
| Arroz 50 kg | 10/03/2026 09:00 | 150.00 | FinDeMes |
| Aceite caja | 25/03/2026 23:00 | 120.00 | FinDeMes |
| Azúcar 10 kg | 26/03/2026 08:00 | 80.00 | FinDeMes |

### Tasas

| Tasa | Valor |
|---|---|
| TEM compensatoria | 0.0300000 |
| TED compensatoria | 0.0009858 |
| TEM moratoria | 0.0400000 |
| TED moratoria | 0.0013082 |

### Ciclos

- El corte de marzo es el 25/03/2026 23:59:59. Como el día de pago (5) es menor que el de corte (25), P cae en el mes siguiente: 05/04/2026.
- La compra del 25/03 a las 23:00 es anterior a la hora de corte y entra al ciclo de marzo.
- La compra del 26/03 es posterior al corte y pasa al ciclo del 25/04/2026, con P el 05/05/2026.

### Listado del corte 25/03/2026 (pago puntual el 05/04/2026)

| Compra | Días | Capital | Interés compensatorio | Total |
|---|---|---|---|---|
| Arroz 50 kg | 26 | 150.00 | 3.89 | 153.89 |
| Aceite caja | 11 | 120.00 | 1.31 | 121.31 |
| Total | | 270.00 | 5.20 | 275.20 |

Pago exacto del 05/04/2026: 275.20, que se imputa así: mora 0.00, interés 5.20 y capital 270.00.

### El mismo ciclo pagado con 7 días de mora (12/04/2026)

| Concepto | Base | Días | Monto |
|---|---|---|---|
| Arroz 50 kg | 153.89 | 7 | 1.41 |
| Aceite caja | 121.31 | 7 | 1.12 |
| Intereses por mora | 275.20 | 7 | 2.53 |

Pago exacto del 12/04/2026: 277.73, que se imputa así: mora 2.53, interés 5.20 y capital 270.00.

El listado `GET /api/clientes/{id}/listado-pago?fechaCorte=2026-03-25` consultado el 12/04 muestra los dos ítems Compra y un ítem InteresMora "Intereses por mora" de 2.53, con un total de 277.73.

### Ciclo siguiente

| Compra | Días | Capital | Interés compensatorio | Total |
|---|---|---|---|---|
| Azúcar 10 kg | 40 (26/03 al 05/05) | 80.00 | 3.22 | 83.22 |

Casos de rechazo:

- El 12/04 se rechaza un pago de 277.72 (parcial) y uno de 277.74 (excedente).
- Un pago el 01/04 se rechaza porque todavía no hay montos exigibles.
- Con 350.00 de deuda, el disponible es 150.00, así que una compra de 150.01 se rechaza por exceder el límite de crédito.
- Una compra en cuotas a 2 meses se rechaza porque supera el plazo máximo de 1.

## Cuerpos JSON para reproducir los juegos en la API

Juego 1, registro de cliente (`POST /api/clientes`):

```json
{
  "dni": "40000001",
  "nombres": "Cliente Juego Uno",
  "limiteCredito": 2000,
  "tipoTasa": "Efectiva",
  "tasaCompensatoria": 0.60,
  "tasaMoratoria": 0.80,
  "diaCorte": 20,
  "diaPago": 26,
  "usuario": "juego1",
  "password": "Juego1-2026",
  "moneda": "PEN",
  "maxMeses": 6,
  "horaCorte": "23:59:59"
}
```

Juego 1, compra (`POST /api/clientes/{id}/compras`):

```json
{ "producto": "Refrigeradora", "precioCredito": 900, "modalidad": "Cuotas", "plazoMeses": 3, "fechaCompra": "2026-09-15T10:30:00" }
```

Juego 1, primer pago (`POST /api/clientes/{id}/pagos`):

```json
{ "monto": 331.67, "fechaPago": "2026-10-31" }
```

Juego 2, registro de cliente:

```json
{
  "dni": "40000002",
  "nombres": "Cliente Juego Dos",
  "limiteCredito": 500,
  "tipoTasa": "Nominal",
  "tasaCompensatoria": 0.36,
  "tasaMoratoria": 0.48,
  "diaCorte": 25,
  "diaPago": 5,
  "usuario": "juego2",
  "password": "Juego2-2026",
  "maxMeses": 1
}
```

Juego 2, compras:

```json
{ "producto": "Arroz 50 kg", "precioCredito": 150, "modalidad": "FinDeMes", "plazoMeses": 1, "fechaCompra": "2026-03-10T09:00:00" }
{ "producto": "Aceite caja", "precioCredito": 120, "modalidad": "FinDeMes", "plazoMeses": 1, "fechaCompra": "2026-03-25T23:00:00" }
{ "producto": "Azúcar 10 kg", "precioCredito": 80, "modalidad": "FinDeMes", "plazoMeses": 1, "fechaCompra": "2026-03-26T08:00:00" }
```

Juego 2, pago con mora:

```json
{ "monto": 277.73, "fechaPago": "2026-04-12" }
```
