# Análisis de las capturas ARP — `personDensityDetection`

> Fuente: `WebhookTest/peticiones/ARP-DensidadPersonas`, `ARP-RangoCantidad`, `ARP-Rangos de permanencia`
> Cámara: `192.168.60.64` (`04:ee:cd:9c:a1:4f`), canal 1 — "Camera 01"
> Fecha de análisis: 2026-09-24

---

## 1. Resumen ejecutivo

Las tres carpetas ARP contienen **405 peticiones** y **todas son el mismo `eventType`: `personDensityDetection`**.
No hay ningún otro tipo de evento en ninguna de las tres sesiones.

Lo que cambia entre carpetas **no es el `eventType`, es el campo `dataSource`** dentro de
`personDensityResult.Target[].TargetInfo`. Ese campo es el verdadero discriminador: define qué
sub-bloque de datos viene y qué significa la alarma.

| Carpeta | Peticiones | `dataSource` presentes | Significado |
|---|---|---|---|
| `ARP-DensidadPersonas` | 130 | `PDC` (99), `timing` (24), `trigger` (7) | Recuento de densidad por área |
| `ARP-RangoCantidad` | 185 | `PQA` (185) | Alarma por rango de **cantidad** de personas |
| `ARP-Rangos de permanencia` | 90 | `DSA` (90) | Alarma por rango de **tiempo de permanencia** |

**Consecuencia para el parseador:** un solo extractor nuevo (`RecuentoPersonasExtractor`)
cubre las tres carpetas, pero debe ramificar internamente por `dataSource`, no por `eventType`.

### Características comunes a las 405 peticiones

- `Content-Type: multipart/form-data; boundary=boundary` (**siempre**, nunca XML ni JSON plano).
- Un único campo de formulario llamado **`personDensityDetection`**, con `Content-Type: application/json`.
- El JSON viene **tabulado con `\t`** (formato típico de Hikvision), no minificado.
- `personDensityResult.Target` es **siempre un array de 1 elemento** (0 casos con 2 o más).
- `eventState` = `"active"` siempre. `isDataRetransmission` = `false` siempre.
- `recognitionType` = `"human"`, `recognition` = `"personDensityDetection"` siempre.
- `regionName` = `"Area1"`, `regionID` = `1` siempre (solo había un área configurada).
- `dateTime` viene con el **reloj de la cámara sin sincronizar**: `2019-01-01T00:xx:xx+08:00`.
  No es utilizable como fecha real del evento (ver §6.4).
- Imagen adjunta: campo `background_image`, `image/jpeg`, ~180–200 KB, nombre sin extensión
  (`2019010100444283200uCdl33yRmGwZb`). **Excepción:** los eventos `timing` no traen imagen.

---

## 2. Catálogo de eventos encontrados

Hay **1 `eventType`** y **5 variantes funcionales** distinguidas por `dataSource`.

### 2.0 Tabla rápida

| # | `dataSource` | Nombre funcional | ¿Alarma? | ¿Imagen? | Campos exclusivos |
|---|---|---|---|---|---|
| 1 | `PDC` | Recuento periódico de personas | No | Sí | `framesPeopleCounting_number` |
| 2 | `timing` | Recuento por temporizador | No | **No** | `framesPeopleCounting_number` |
| 3 | `trigger` | Cambio de nivel de densidad | **Sí** | Sí | `densityLevel`, `densityLevelChangeType`, `customName`, `personCnt` |
| 4 | `PQA` | Alarma por rango de cantidad | **Sí** | Sí | `PQA.alarmCount`, `PQA.countTriggerType`, `PQA.Region[]` |
| 5 | `DSA` | Alarma por rango de permanencia | **Sí** | Sí | `DSA.alarmTime`, `DSA.timeTriggerType`, `DSA.Region[]`, `targetID` |

---

### 2.1 `dataSource: "PDC"` — Recuento periódico (People Density Counting)

**Qué es:** la cámara publica cuánta gente está viendo en el área, de forma continua.
Es telemetría, **no una alarma**. 99 de 130 eventos en `ARP-DensidadPersonas`.

**Campo clave:** `framesPeopleCounting_number` = número de personas en el frame (valores observados: 1–5).

**Trae imagen:** sí (`background_image`) + `personDensityResult.contentID` + `densityBackgroundImageResolution`.

**Ejemplo** (`ARP-DensidadPersonas/-KXEVHnyj-DS`):

```json
{
  "ipAddress": "192.168.60.64",
  "portNo": 4000,
  "protocol": "HTTP",
  "macAddress": "04:ee:cd:9c:a1:4f",
  "channelID": 1,
  "dateTime": "2019-01-01T00:44:42+08:00",
  "activePostCount": 1,
  "isDataRetransmission": false,
  "eventState": "active",
  "channelName": "Camera 01",
  "eventType": "personDensityDetection",
  "eventDescription": "Person Density Detection",
  "personDensityResult": {
    "Target": [{
      "recognitionType": "human",
      "TargetInfo": {
        "recognition": "personDensityDetection",
        "dataSource": "PDC",
        "framesPeopleCounting_number": 4,
        "regionName": "Area1",
        "regionID": 1
      }
    }],
    "contentID": "background_image"
  },
  "densityBackgroundImageResolution": { "height": 1136, "width": 1920 }
}
```

---

### 2.2 `dataSource: "timing"` — Recuento por temporizador

**Qué es:** el mismo recuento que `PDC`, pero emitido por el temporizador de reporte periódico
(el "Upload Interval" de la cámara) en lugar de por cambio detectado. 24 eventos.

**Diferencia estructural importante:** **no trae imagen**, y por tanto **tampoco**
`personDensityResult.contentID` ni `densityBackgroundImageResolution`.
Es el único caso de las 405 peticiones sin adjunto.

**Ejemplo** (`ARP-DensidadPersonas/5SEMWLnc_thf`):

```json
{
  "...": "cabecera idéntica a PDC",
  "eventType": "personDensityDetection",
  "eventDescription": "Person Density Detection",
  "personDensityResult": {
    "Target": [{
      "recognitionType": "human",
      "TargetInfo": {
        "recognition": "personDensityDetection",
        "dataSource": "timing",
        "framesPeopleCounting_number": 3,
        "regionName": "Area1",
        "regionID": 1
      }
    }]
  }
}
```

> Fíjate: no hay `contentID`, no hay `densityBackgroundImageResolution`, no hay parte de archivo.

---

### 2.3 `dataSource: "trigger"` — Cambio de nivel de densidad (ALARMA)

**Qué es:** la cámara tiene **niveles de densidad configurados con nombre** y dispara cuando se
cruza el umbral de un nivel a otro. Es la alarma real de "Densidad de personas". Solo 7 eventos
en la captura (son los que se generaron al forzar el cambio de nivel).

**Campos exclusivos:**

| Campo | Significado | Valores observados |
|---|---|---|
| `densityLevel` | Nivel de densidad alcanzado | `2`, `3` |
| `densityLevelChangeType` | Sentido del cambio | `lowToHigh` (4), `highToLow` (3) |
| `customName` | Nombre del nivel configurado en la cámara | `"Densidad dos"`, `"Densidad tr3s"` |
| `personCnt` | Personas que dispararon el nivel | `2`, `3` |
| `framesPeopleCounting_number` | Recuento del frame | `2`, `3` |

Combinaciones completas observadas:

- `highToLow` / nivel 2 / "Densidad dos" / 2 personas → 3 eventos
- `lowToHigh` / nivel 3 / "Densidad tr3s" / 3 personas → 4 eventos

**Ejemplo** (`ARP-DensidadPersonas/239f0XXzNKge`):

```json
{
  "...": "cabecera igual",
  "personDensityResult": {
    "Target": [{
      "recognitionType": "human",
      "TargetInfo": {
        "recognition": "personDensityDetection",
        "dataSource": "trigger",
        "framesPeopleCounting_number": 2,
        "densityLevel": 2,
        "personCnt": 2,
        "densityLevelChangeType": "highToLow",
        "customName": "Densidad dos",
        "regionName": "Area1",
        "regionID": 1
      }
    }],
    "contentID": "background_image"
  },
  "densityBackgroundImageResolution": { "height": 1136, "width": 1920 }
}
```

---

### 2.4 `dataSource: "PQA"` — Alarma por rango de cantidad (ALARMA)

**Qué es:** *Person Quantity Alarm*. Salta cuando el número de personas dentro de la zona cumple
una condición configurada. **Toda** la carpeta `ARP-RangoCantidad` (185 eventos).

**Sub-objeto `PQA`:**

| Campo | Significado | Valores observados |
|---|---|---|
| `alarmCount` | Personas contadas al disparar | `2`, `3`, `4` |
| `countTriggerType` | Condición configurada | ver tabla abajo |
| `Region[]` | Polígono de la zona, **coordenadas normalizadas 0..1** | 6 vértices |

**`countTriggerType` — los 6 valores aparecen en la captura:**

| Valor | Significado | Nº eventos |
|---|---|---|
| `greater` | Mayor que N | 60 |
| `range` | Dentro de un rango | 42 |
| `outOfRange` | Fuera de un rango | 40 |
| `less` | Menor que N | 16 |
| `equal` | Igual a N | 14 |
| `unequal` | Distinto de N | 13 |

> El JSON **no envía el umbral configurado**, solo el tipo de condición y el conteo real
> (`alarmCount`). El umbral hay que leerlo de la configuración ISAPI de la cámara si se necesita.

**El polígono `Region` es idéntico en las 185 peticiones** (la zona no cambió durante la prueba):
`(0.013,0.035) (0.021,0.857) (0.98,0.87) (0.981,0.159) (0.739,0.156) (0.737,0.013)`

**Ejemplo** (`ARP-RangoCantidad/-ITkHo_TjVHD`):

```json
{
  "...": "cabecera igual",
  "personDensityResult": {
    "Target": [{
      "recognitionType": "human",
      "TargetInfo": {
        "recognition": "personDensityDetection",
        "dataSource": "PQA",
        "PQA": {
          "alarmCount": 4,
          "countTriggerType": "greater",
          "Region": [
            { "x": 0.013, "y": 0.035 },
            { "x": 0.021, "y": 0.857 },
            { "x": 0.98,  "y": 0.87  },
            { "x": 0.981, "y": 0.159 },
            { "x": 0.739, "y": 0.156 },
            { "x": 0.737, "y": 0.013 }
          ]
        },
        "regionName": "Area1",
        "regionID": 1
      }
    }],
    "contentID": "background_image"
  },
  "densityBackgroundImageResolution": { "height": 1136, "width": 1920 }
}
```

---

### 2.5 `dataSource: "DSA"` — Alarma por rango de permanencia (ALARMA)

**Qué es:** *Dwell/Stay Alarm*. Salta cuando una persona concreta lleva en la zona un tiempo que
cumple la condición configurada. **Toda** la carpeta `ARP-Rangos de permanencia` (90 eventos).

**Sub-objeto `DSA` + campo hermano `targetID`:**

| Campo | Significado | Valores observados |
|---|---|---|
| `alarmTime` | Tiempo de permanencia al disparar (**segundos**) | 0 – 491 |
| `timeTriggerType` | Condición configurada | `greater` (76), `range` (9), `less` (5) |
| `Region[]` | Polígono de la zona (normalizado 0..1) | mismos 6 vértices que PQA |
| `targetID` (fuera de `DSA`) | **ID de la persona rastreada** | `1, 2, 8, 9, 25` |

> `targetID` es exclusivo de `DSA` y es lo que permite **deduplicar**: la misma persona genera
> eventos repetidos con `alarmTime` creciente (p. ej. 198, 199, 200, 201… en segundos consecutivos).
> Esto explica por qué 90 eventos son en realidad un puñado de personas, no 90 alarmas distintas.

**Ejemplo** (`ARP-Rangos de permanencia/-1yHTKEmcrQZ`):

```json
{
  "...": "cabecera igual",
  "personDensityResult": {
    "Target": [{
      "recognitionType": "human",
      "TargetInfo": {
        "recognition": "personDensityDetection",
        "dataSource": "DSA",
        "regionID": 1,
        "DSA": {
          "alarmTime": 200,
          "timeTriggerType": "greater",
          "Region": [
            { "x": 0.013, "y": 0.035 },
            { "x": 0.021, "y": 0.857 },
            { "x": 0.98,  "y": 0.87  },
            { "x": 0.981, "y": 0.159 },
            { "x": 0.739, "y": 0.156 },
            { "x": 0.737, "y": 0.013 }
          ]
        },
        "regionName": "Area1",
        "targetID": 2
      }
    }],
    "contentID": "background_image"
  },
  "densityBackgroundImageResolution": { "height": 1136, "width": 1920 }
}
```

---

## 3. Diccionario completo de campos

### 3.1 Cabecera (idéntica en los 5 tipos)

| Campo JSON | XPath tras normalizar | Tipo | Nota |
|---|---|---|---|
| `ipAddress` | `//ipaddress` | string | Ya lo extrae `BaseInfoExtractor` |
| `portNo` | `//portno` | int | 4000 |
| `protocol` | `//protocol` | string | `HTTP` |
| `macAddress` | `//macaddress` | string | Identifica la cámara mejor que la IP |
| `channelID` | `//channelid` | int | 1 |
| `channelName` | `//channelname` | string | `Camera 01` |
| `dateTime` | `//datetime` | ISO8601 | Reloj desincronizado |
| `activePostCount` | `//activepostcount` | int | 1 |
| `isDataRetransmission` | `//isdataretransmission` | bool | `false` siempre |
| `eventState` | `//eventstate` | string | `active` siempre |
| `eventType` | `//eventtype` | string | `personDensityDetection` |
| `eventDescription` | `//eventdescription` | string | `Person Density Detection` |

### 3.2 Bloque de resultado

| Campo JSON | XPath tras normalizar | Presente en |
|---|---|---|
| `personDensityResult.contentID` | `//persondensityresult/contentid` | todos menos `timing` |
| `densityBackgroundImageResolution.width` | `//densitybackgroundimageresolution/width` | todos menos `timing` |
| `densityBackgroundImageResolution.height` | `//densitybackgroundimageresolution/height` | todos menos `timing` |
| `...Target[].recognitionType` | `//target/recognitiontype` | todos |
| `...TargetInfo.recognition` | `//targetinfo/recognition` | todos |
| `...TargetInfo.dataSource` | `//targetinfo/datasource` | **todos — discriminador** |
| `...TargetInfo.regionID` | `//targetinfo/regionid` | todos |
| `...TargetInfo.regionName` | `//targetinfo/regionname` | todos |
| `...TargetInfo.framesPeopleCounting_number` | `//targetinfo/framespeoplecounting_number` | PDC, timing, trigger |
| `...TargetInfo.densityLevel` | `//targetinfo/densitylevel` | trigger |
| `...TargetInfo.densityLevelChangeType` | `//targetinfo/densitylevelchangetype` | trigger |
| `...TargetInfo.customName` | `//targetinfo/customname` | trigger |
| `...TargetInfo.personCnt` | `//targetinfo/personcnt` | trigger |
| `...TargetInfo.targetID` | `//targetinfo/targetid` | **DSA** |
| `...TargetInfo.PQA.alarmCount` | `//targetinfo/pqa/alarmcount` | PQA |
| `...TargetInfo.PQA.countTriggerType` | `//targetinfo/pqa/counttriggertype` | PQA |
| `...TargetInfo.PQA.Region[]` | `//targetinfo/pqa/region` (**N nodos**) | PQA |
| `...TargetInfo.DSA.alarmTime` | `//targetinfo/dsa/alarmtime` | DSA |
| `...TargetInfo.DSA.timeTriggerType` | `//targetinfo/dsa/timetriggertype` | DSA |
| `...TargetInfo.DSA.Region[]` | `//targetinfo/dsa/region` (**N nodos**) | DSA |

---

## 4. Cómo lo parseamos con el parseador actual

### 4.1 Lo que ya funciona sin tocar nada

El pipeline existente ya cubre el 80% del camino:

1. **`WebhookPayloadExtractorService.Extraer`** — la petición es `multipart/form-data`, entra por
   `request.HasFormContentType`, recorre los campos y coge el primero no vacío. Como solo hay un
   campo (`personDensityDetection`), `Body` queda con el JSON correcto.
2. La imagen `background_image` llega con `Content-Type: image/jpeg`, así que cae en la rama de
   `form.Files` → `resultado.Imagenes`. Se guarda como `.jpg` con GUID.
3. **`CameraPayloadLoader.ToNormalizedXml`** — detecta JSON por el primer carácter `{`
   (ignora el Content-Type, que aquí viene bien pero da igual), lo convierte a `XDocument`,
   aplana `Target[]` y `Region[]` a elementos hermanos repetidos, y pasa todo a minúsculas.
4. **`BaseInfoExtractor`** — `//ipaddress`, `//eventtype`, `//eventstate` salen directos.

**Lo único que falta es el extractor de sección.** Hoy `personDensityDetection` no está en
`EventosAplicables` de `EventoSmartExtractor`, así que `VCAModo` se queda en `Ninguno` y el
controller lo manda a `AlarmasDesconocidasLog` con motivo `EXTRACTOR_DETALLES_ERROR`.

### 4.2 Lo que hay que añadir

#### a) Un helper para listas en `XmlQueryExtensions`

`BuscarInnerJson` usa `XPathSelectElement` (singular) → con `//dsa/region` solo devolvería el
**primer vértice**. Para el polígono hace falta seleccionar todos:

```csharp
/// <summary>Serializa TODOS los nodos que casen como un array JSON: [{"x":..,"y":..}, ...]</summary>
public static string? BuscarListaInnerJson(this XDocument doc, params string[] xpaths)
{
    foreach (var xpath in xpaths)
    {
        var nodos = doc.XPathSelectElements(xpath).ToList();
        if (nodos.Count == 0) continue;

        var items = nodos.Select(n => new JObject(
            n.Elements().Select(h => new JProperty(h.Name.LocalName, (string)h))));

        return new JArray(items).ToString(Formatting.None);
    }

    return null;
}
```

> Formato de salida: `[{"x":"0.013","y":"0.035"},{"x":"0.021","y":"0.857"}, ...]`
> Cabe de sobra en `ZonaDeteccion VARCHAR(500)` (6 vértices ≈ 170 caracteres).

#### b) Modelo de sección

```csharp
namespace ViisionRemolques.Parsing.Models
{
    public class RecuentoPersonasModel
    {
        // Común
        public string? DataSource { get; set; }            // PDC | timing | trigger | PQA | DSA
        public string? RegionID { get; set; }
        public string? RegionName { get; set; }
        public string? RegionCoordinatesList { get; set; } // polígono JSON (solo PQA/DSA)

        // PDC / timing / trigger
        public int? PersonasEnFrame { get; set; }          // framesPeopleCounting_number

        // trigger
        public int? DensityLevel { get; set; }
        public string? DensityLevelChangeType { get; set; } // lowToHigh | highToLow
        public string? CustomName { get; set; }
        public int? PersonCnt { get; set; }

        // PQA
        public int? AlarmCount { get; set; }
        public string? CountTriggerType { get; set; }      // greater|less|equal|unequal|range|outOfRange

        // DSA
        public int? AlarmTimeSegundos { get; set; }
        public string? TimeTriggerType { get; set; }       // greater|less|range
        public string? TargetID { get; set; }
    }
}
```

Y en `CameraEventModel`:

```csharp
public RecuentoPersonasModel? RecuentoPersonas { get; set; }
```

#### c) El extractor

```csharp
using System.Xml.Linq;
using ViisionRemolques.Enums;
using ViisionRemolques.Parsing.Models;

namespace ViisionRemolques.Parsing.Extractors
{
    /// <summary>
    /// personDensityDetection: recuento y alarmas de aforo/permanencia.
    /// El eventType es único; lo que cambia el significado es TargetInfo/dataSource.
    /// </summary>
    public class RecuentoPersonasExtractor : ICameraEventSectionExtractor
    {
        public VCAModoEnum VCAModo { get; set; } = VCAModoEnum.AlarmaRecuntoPersonas;

        public bool AplicaPara(string eventType, XDocument? doc = null) =>
            string.Equals(eventType, "personDensityDetection", StringComparison.OrdinalIgnoreCase);

        public void Extraer(XDocument doc, CameraEventModel evento)
        {
            var dataSource = doc.Buscar("//targetinfo/datasource");

            evento.RecuentoPersonas = new RecuentoPersonasModel
            {
                DataSource      = dataSource,
                RegionID        = doc.Buscar("//targetinfo/regionid"),
                RegionName      = doc.Buscar("//targetinfo/regionname"),
                PersonasEnFrame = Entero(doc.Buscar("//targetinfo/framespeoplecounting_number")),

                // trigger
                DensityLevel           = Entero(doc.Buscar("//targetinfo/densitylevel")),
                DensityLevelChangeType = doc.Buscar("//targetinfo/densitylevelchangetype"),
                CustomName             = doc.Buscar("//targetinfo/customname"),
                PersonCnt              = Entero(doc.Buscar("//targetinfo/personcnt")),

                // PQA
                AlarmCount       = Entero(doc.Buscar("//targetinfo/pqa/alarmcount")),
                CountTriggerType = doc.Buscar("//targetinfo/pqa/counttriggertype"),

                // DSA
                AlarmTimeSegundos = Entero(doc.Buscar("//targetinfo/dsa/alarmtime")),
                TimeTriggerType   = doc.Buscar("//targetinfo/dsa/timetriggertype"),
                TargetID          = doc.Buscar("//targetinfo/targetid"),

                // El polígono está bajo pqa o bajo dsa según el caso.
                RegionCoordinatesList = doc.BuscarListaInnerJson(
                    "//targetinfo/pqa/region",
                    "//targetinfo/dsa/region")
            };

            // PDC y timing son telemetría de recuento, no alarma: modo distinto.
            evento.BaseInfo.VCAModo =
                string.Equals(dataSource, "PDC", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(dataSource, "timing", StringComparison.OrdinalIgnoreCase)
                    ? VCAModoEnum.RecuentoPersonas
                    : VCAModoEnum.AlarmaRecuntoPersonas;
        }

        private static int? Entero(string? v) =>
            int.TryParse(v, out var n) ? n : null;
    }
}
```

Registrarlo en `CameraEventParser`:

```csharp
private static readonly List<ICameraEventSectionExtractor> Extractores =
[
    new EventoSmartExtractor(),
    new RecuentoPersonasExtractor()
];
```

#### d) Un cambio obligatorio en `CameraEventParser`

Hoy el parser hace esto **después** de `Extraer`:

```csharp
extractor.Extraer(doc, evento);
evento.BaseInfo.VCAModo = extractor.VCAModo;   // <-- pisa lo que puso el extractor
```

Como aquí el modo depende del payload (`PDC`/`timing` → recuento, resto → alarma), hay que
invertir el orden para que el extractor tenga la última palabra:

```csharp
evento.BaseInfo.VCAModo = extractor.VCAModo;   // valor por defecto del extractor
extractor.Extraer(doc, evento);                // el extractor puede afinarlo
```

> Nota aparte: la propiedad `VCAModo` del interfaz vive en una instancia **singleton compartida**
> dentro de la lista estática `Extractores`. Escribirla por petición sería una condición de
> carrera. Con este cambio se usa solo como valor por defecto de lectura, que es seguro.

#### e) El `switch` del controller

```csharp
case VCAModoEnum.AlarmaRecuntoPersonas:
case VCAModoEnum.RecuentoPersonas:

    await _repo.InsertarAsync(new EventoPerimetral()
    {
        IPCamara      = evento.BaseInfo.IpAddress,
        Evento        = evento.BaseInfo.EventType ?? "",
        ReglaId       = evento.RecuentoPersonas?.RegionID,
        ZonaDeteccion = evento.RecuentoPersonas?.RegionCoordinatesList,
        TipoObjetivo  = "human",
        FechaEvento   = DateTime.Now,
        PathImagen    = imagenesPaths.Count != 0 ? imagenesPaths[0] : null
    });
    break;
```

---

## 5. El problema de fondo: `EventoPerimetral` no da para estos eventos

La tabla `EventosPerimetrales` fue diseñada para eventos smart (intrusión, cruce de línea…) y
solo tiene `ReglaId`, `ZonaDeteccion` y `TipoObjetivo`. Si insertamos ahí los ARP,
**se pierde todo lo que hace útil a estos eventos**: el conteo, el tiempo de permanencia, el tipo
de condición, el nivel de densidad y el `targetID`.

Dos caminos:

**Opción A — tabla propia (recomendada).** Los datos son estructuralmente distintos; forzarlos en
`EventosPerimetrales` obliga a leer columnas que significan otra cosa.

```sql
CREATE TABLE EventosRecuentoPersonas (
    IdInterno         BIGINT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
    IdExterno         BIGINT NULL,
    IPCamara          VARCHAR(45),
    MacCamara         VARCHAR(20)   NULL,
    Evento            VARCHAR(50)   NOT NULL,   -- personDensityDetection
    DataSource        VARCHAR(20)   NOT NULL,   -- PDC|timing|trigger|PQA|DSA
    RegionId          VARCHAR(64)   NULL,
    RegionNombre      NVARCHAR(100) NULL,
    ZonaDeteccion     VARCHAR(500)  NULL,       -- polígono JSON normalizado 0..1
    PersonasEnFrame   INT           NULL,       -- framesPeopleCounting_number
    DensityLevel      INT           NULL,       -- trigger
    DensityChangeType VARCHAR(20)   NULL,       -- lowToHigh | highToLow
    NivelNombre       NVARCHAR(100) NULL,       -- customName
    AlarmCount        INT           NULL,       -- PQA
    CountTriggerType  VARCHAR(20)   NULL,       -- PQA
    AlarmTimeSeg      INT           NULL,       -- DSA
    TimeTriggerType   VARCHAR(20)   NULL,       -- DSA
    TargetId          VARCHAR(32)   NULL,       -- DSA, para deduplicar
    FechaEvento       DATETIME2(2)  NOT NULL DEFAULT GETDATE(),
    PathImagen        NVARCHAR(500) NULL,
    Sincronizado      BIT           NOT NULL DEFAULT 0,
    FechaRegistro     DATETIME      NOT NULL DEFAULT GETDATE()
);
GO

CREATE INDEX IX_ERP_PendientesSincronizar ON EventosRecuentoPersonas (Sincronizado, FechaRegistro)
WHERE Sincronizado = 0;
GO

-- Para la deduplicación de DSA (§6.2)
CREATE INDEX IX_ERP_Dedupe ON EventosRecuentoPersonas (IPCamara, DataSource, TargetId, FechaEvento DESC);
GO
```

**Opción B — reutilizar `EventosPerimetrales`** metiendo el detalle en un `NVARCHAR(MAX) Detalle`
como JSON. Más rápido de implementar, pero pierdes poder consultar/filtrar por conteo o tiempo
desde SQL, que es justo lo que se va a querer en los reportes de aforo.

---

## 6. Trampas a tener en cuenta

### 6.1 El volumen es alto y casi todo es ruido

405 eventos en ~15 minutos de prueba, y **123 de 130** en `ARP-DensidadPersonas` son
`PDC`/`timing` (telemetría pura). Guardarlos todos como "evento" inunda la tabla.
**Recomendación:** filtrar `PDC`/`timing` igual que se hace hoy con `heartBeat`/`VMD`/`duration`,
o mandarlos a una tabla de telemetría aparte. Las alarmas reales son `trigger`, `PQA` y `DSA`.

### 6.2 `DSA` repite la misma alarma cada segundo

Ejemplo real de la captura: `targetID=2` genera eventos con `alarmTime` = 198, 199, 200, 201, 202,
203… uno por segundo mientras la persona siga en la zona. Son **la misma alarma**.
Deduplicar por `(IPCamara, targetID, timeTriggerType)` con una ventana de N segundos, o quedarse
solo con el primero de cada ráfaga.

### 6.3 El umbral configurado no viaja en el evento

`countTriggerType: "greater"` con `alarmCount: 4` dice "saltó por ser mayor que el umbral, y había
4 personas" — pero **no dice cuál era el umbral**. Si se necesita para mostrarlo en la UI hay que
leerlo por ISAPI de la configuración de la cámara, no del webhook.

### 6.4 `dateTime` no es fiable

Todas las capturas traen `2019-01-01T00:xx:xx+08:00`: la cámara tiene el reloj de fábrica y el
huso horario de China. El controller ya usa `DateTime.Now` en lugar de `dateTime`, que es lo
correcto **mientras las cámaras no estén sincronizadas por NTP**. Si se sincronizan, conviene
pasar a usar `dateTime` (respetando el offset) porque `activePostCount` / la retransmisión pueden
hacer que la hora de llegada no sea la del evento.

### 6.5 Coordenadas normalizadas, no píxeles

`Region[].x/y` van de 0 a 1. Para dibujar sobre la imagen hay que multiplicar por
`densityBackgroundImageResolution.width/height` (1920×1136 en esta cámara).
Ojo: 1136 no es 1080 — **no asumas la resolución**, léela del evento.
En `timing` no viene, pero `timing` tampoco trae imagen, así que no hay conflicto.

### 6.6 La imagen llega sin extensión

`nombreOriginal` es `2019010100444283200uCdl33yRmGwZb`, sin `.jpg`. `AlmacenamientoImagenesService`
ya fuerza `.jpg` por defecto y el `Content-Type` declarado es `image/jpeg`, así que funciona —
pero no dependas del nombre del archivo para deducir el tipo.

### 6.7 `Target` es un array

En esta captura siempre trae 1 elemento, pero es un array y otra configuración (varias áreas)
podría traer varios. Los XPath `//targetinfo/...` cogen el primero. Si aparecen payloads
multi-área habrá que iterar `doc.XPathSelectElements("//target")` y emitir N eventos.
Vale la pena dejar un aviso en el log si `//target` devuelve más de un nodo.

### 6.8 Consistencia de mayúsculas en `dataSource`

`dataSource` mezcla mayúsculas y minúsculas: `PDC`, `PQA`, `DSA` en mayúsculas; `timing`,
`trigger` en minúsculas. Comparar siempre con `OrdinalIgnoreCase`, igual que hace hoy
`EventoSmartExtractor` con su lista `EventosAplicables` (que también mezcla ambos estilos).

---

## 7. Checklist de implementación

- [ ] Añadir `BuscarListaInnerJson` a `XmlQueryExtensions`.
- [ ] Crear `Parsing/Models/RecuentoPersonasModel.cs`.
- [ ] Añadir `RecuentoPersonas` a `CameraEventModel`.
- [ ] Crear `Parsing/Extractors/RecuentoPersonasExtractor.cs`.
- [ ] Registrarlo en `CameraEventParser.Extractores`.
- [ ] **Invertir el orden** de `evento.BaseInfo.VCAModo = extractor.VCAModo;` y `extractor.Extraer(...)`.
- [ ] Decidir tabla (opción A o B) y escribir el SP de inserción.
- [ ] Añadir el `case` en `WebhookController`.
- [ ] Filtrar o desviar `PDC`/`timing` para no inundar la tabla.
- [ ] Implementar la deduplicación de `DSA` por `targetID`.
- [ ] Probar reinyectando las 405 capturas con `WebhookTest/reenviar.js`.
