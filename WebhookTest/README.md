# WebhookTest

Servidor Express que expone **un único endpoint** en `http://localhost:5106/v1/api/webhook`
que acepta cualquier método HTTP, cualquier `Content-Type` y cualquier archivo.
Cada petición se guarda íntegra en disco y el servidor responde siempre con un
**200 sin cuerpo**.

## Uso

```bash
npm install
npm start          # http://localhost:5106/v1/api/webhook  (npm run dev recarga al guardar)
```

Las subrutas también entran (`/v1/api/webhook/lo/que/sea?x=1`). Cualquier otra ruta da 404.

## Estructura en disco

**Cada arranque del servidor crea su propia carpeta de sesión** con un id corto. Dentro
van las peticiones de esa ejecución y el log completo de lo que salió por pantalla:

```
peticiones/
  jVT4JQOcSCzU/              <- una sesión = un arranque del servidor
    log.txt                  <- TODO lo impreso por pantalla en esa ejecución
    _indice.jsonl            <- una línea por petición (id, fecha, método, url, ip, tipo, bytes)
    buSYN6oAvLUn/            <- una petición
      raw.http               <- la petición tal cual llegó (cabeceras crudas + cuerpo en bytes)
      peticion.json          <- todos los metadatos
      cuerpo.bin             <- el cuerpo exacto en binario
      cuerpo.json|xml|txt    <- el cuerpo en texto, si el Content-Type era textual
      archivos/
        01_foto.jpg          <- cada archivo del multipart, con su nombre original saneado
        02_informe.pdf
```

`peticion.json` incluye método, URL, query, versión HTTP, cabeceras, cabeceras crudas,
trailers, cookies, IP/puerto remoto y local, tamaños, el cuerpo parseado, los campos del
formulario y la lista de archivos.

> **Los datos se guardan exactamente como llegan.** `raw.http`, `cuerpo.bin` y los archivos
> son byte a byte lo que mandó el cliente. El formateo con indentación es solo para lo que
> se imprime por pantalla y en `log.txt`.

## Qué se imprime por consola (y se copia a `log.txt`)

```
[2026-09-23T16:48:33.001Z] POST /v1/api/webhook
  ID (carpeta): buSYN6oAvLUn
  Guardado en:  C:\...\peticiones\jVT4JQOcSCzU\buSYN6oAvLUn
  IP:           192.168.50.26
  Content-Type: application/xml
  Body (xml):
    <?xml version="1.0" encoding="UTF-8"?>
    <EventNotificationAlert version="2.0">
      <ipAddress>192.168.50.26</ipAddress>
      <eventType>attendedBaggage</eventType>
    </EventNotificationAlert>
```

- **XML** → se imprime como XML indentado (conservando atributos, comentarios, CDATA y la declaración).
- **JSON** y **x-www-form-urlencoded** → se imprimen como JSON indentado.
- Textual pero no parseable → el texto tal cual, indicando el error de parseo.
- Binario → `(binario no parseable: N bytes, guardado en cuerpo.bin)`.
- **Multipart** → los campos y la lista de archivos. Si un campo **o un archivo subido**
  contiene un XML o un JSON (típico de cámaras y dispositivos IoT), se muestra formateado
  debajo, igual que el body:

```
    archivos:
      - MoveDetection.xml [application/xml, 699 bytes] -> archivos/01_MoveDetection.xml  (xml)
          <?xml version="1.0" encoding="UTF-8"?>
          <EventNotificationAlert version="2.0">
            <eventType>VMD</eventType>
          </EventNotificationAlert>
      - 20260923110545461.jpg [image/jpeg, 122798 bytes] -> archivos/02_20260923110545461.jpg
```

  Solo se releen los archivos de hasta 2 MB cuyo primer byte útil es `<`, `{` o `[`; el
  resto (fotos, PDFs, binarios) se queda en una línea. El contenido no se duplica dentro
  de `peticion.json`: se lee del archivo ya guardado solo para imprimirlo.
- Cada bloque se recorta a 4000 caracteres (`MAX_LOG_CUERPO`); en disco se guarda todo.

### Resumen al cerrar

Al parar el servidor (Ctrl+C) se imprime — y se guarda en `log.txt` — un resumen de la sesión:

```
============================================================
RESUMEN DE LA SESION F4bBJUVqtoSe
============================================================
Duracion:           7s
Peticiones totales: 14
Datos recibidos:    1.1 KB
Archivos guardados: 2

Peticiones por IP:
  127.0.0.1  14  (100.0%)

Peticiones por Content-Type (con la carpeta de cada una):
  application/json     8  ( 57.1%)
      VY8Dp8a6eZ0_, O0O7XAddEBY3, pQLfBj6hZl3X, Yb1rX9ivniOi, -H6XzQV0ioiJ, qw4GLrVLbL4I,
      -PeYjx0PDOcx, q5uHJN8bE_bp
  application/xml      3  ( 21.4%)
      zRx8eANIcPB6, lkh3uxj1_S7e, 1mYKMEyj9AAp
  multipart/form-data  2  ( 14.3%)
      wzbuQQyA4y_T, Z3Ut0QQTJRtm
============================================================
```

Bajo cada content-type van los ids de las carpetas de esas peticiones (en orden de llegada),
para poder ir directo a `peticiones/<sesion>/<id>/`. Los content-type se agrupan sin sus
parámetros (`multipart/form-data; boundary=xxx` cuenta como `multipart/form-data`), y el
resumen se imprime cuando ya han terminado las peticiones que estaban en vuelo, así que no
se pierde ninguna en la cuenta.

### El archivo de log

`log.txt` recibe una copia de cada línea impresa, con **escrituras síncronas**: en todo
momento el archivo tiene en disco lo mismo que se vio por pantalla, así que no se pierde
nada aunque el proceso muera de golpe (Ctrl+C, `kill`, o una excepción no capturada).
Al cerrar ordenadamente se añade una marca `--- Log cerrado: <fecha> ---`.

## Variables de entorno

| Variable | Por defecto | Para qué |
|---|---|---|
| `PORT` | `5106` | Puerto de escucha |
| `HOST` | `0.0.0.0` | Interfaz (permite recibir desde otras máquinas de la red) |
| `RUTA_WEBHOOK` | `/v1/api/webhook` | Ruta del endpoint |
| `DIR_PETICIONES` | `./peticiones` | Dónde se crean las carpetas de sesión |
| `LIMITE_BYTES` | `1073741824` (1 GB) | Máximo de cuerpo que se guarda en memoria; si se supera, se marca `truncado: true` y se sigue drenando la conexión |
| `MAX_LOG_CUERPO` | `4000` | Caracteres de body que se imprimen antes de recortar |

## Cómo funciona

No se usa ningún body-parser: un middleware propio (`lib/cuerpo-crudo.js`) consume el
stream y guarda los bytes tal cual, sin mirar el `Content-Type`. Por eso funciona igual
con JSON, XML, binario, `multipart`, un cuerpo mal formado o sin cuerpo.
Si el `Content-Type` es `multipart/*`, además se parsea con `busboy` para extraer los
archivos y los campos — pero el cuerpo original sigue guardado entero en `cuerpo.bin`.

| Archivo | Responsabilidad |
|---|---|
| `server.js` | Endpoint, carpeta de sesión, arranque y cierre ordenado |
| `lib/cuerpo-crudo.js` | Middleware que captura los bytes del cuerpo sin interpretarlos |
| `lib/guardar-peticion.js` | Vuelca la petición a disco y parsea multipart / JSON / XML |
| `lib/imprimir-peticion.js` | Da formato a lo que se ve por consola |
| `lib/formato-xml.js` | Re-indenta XML sobre el texto original |
| `lib/registro-log.js` | Copia la consola a `log.txt` con escrituras síncronas |
| `lib/estadisticas.js` | Acumula y pinta el resumen de la sesión al cerrar |
| `lib/nanoid.js` | Ids cortos, sin dependencias |

## Ejemplos

```bash
U=http://localhost:5106/v1/api/webhook

curl -X POST "$U?a=1" -H 'Content-Type: application/json' -d '{"mensaje":"hola"}'
curl -X POST $U -H 'Content-Type: application/xml' -d '<pedido id="7"><cliente>Javi</cliente></pedido>'
curl -X PUT  $U -F 'usuario=javi' -F 'fichero=@C:/ruta/al/archivo.pdf'
curl -X PATCH $U -H 'Content-Type: application/octet-stream' --data-binary @foto.jpg
curl -X DELETE $U
```

## Límites conocidos

- **Verbos HTTP**: Node solo acepta los 35 de `http.METHODS` (GET, POST, PUT, PATCH,
  DELETE, OPTIONS, TRACE, PURGE, QUERY, los de WebDAV…). Un verbo inventado como `MIAU`
  lo rechaza el parser de Node con un 400 **antes** de llegar a Express; queda avisado por
  consola pero no se puede guardar. No hay forma de evitarlo desde Express.
- `raw.http` es una reconstrucción fiel, no una captura del cable: si el cliente usó
  `Transfer-Encoding: chunked`, Node ya quitó el troceado y el cuerpo aparece ensamblado.
- El cuerpo se acumula en memoria hasta `LIMITE_BYTES` antes de escribirse a disco.
- En Windows, `kill` desde Git Bash mata el proceso sin entregarle la señal a Node, así que
  no se ejecuta el cierre ordenado (con Ctrl+C en una consola sí). Da igual para el log:
  al escribirse de forma síncrona ya está todo en disco.
