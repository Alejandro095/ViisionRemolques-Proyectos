'use strict';

// Reenvia a una API todas las peticiones guardadas en una carpeta de sesion.
// Manda los bytes exactos de cuerpo.bin con las cabeceras originales, asi que
// la API recibe justo lo mismo que mandaron las camaras (boundary incluido).

const fs = require('fs');
const path = require('path');
const http = require('http');
const https = require('https');

// Cabeceras que no se reenvian: son propias de la conexion original, no del
// mensaje. Host y Content-Length los recalcula Node con el destino real.
const CABECERAS_IGNORADAS = new Set([
  'host',
  'connection',
  'content-length',
  'transfer-encoding',
  'keep-alive',
  'upgrade',
  'proxy-connection',
  'expect',
]);

function parsearArgumentos(argv) {
  const opciones = {
    sesion: null,
    destino: 'http://localhost:5106/v1/api/webhook',
    concurrencia: 1,
    pausa: 0,
    limite: Infinity,
    filtro: null,
    ritmoReal: false,
    seco: false,
    detalle: false,
    timeout: 30000,
  };

  const resto = [];

  for (let i = 2; i < argv.length; i++) {
    const arg = argv[i];
    const valor = () => argv[++i];

    switch (arg) {
      case '-d': case '--destino':      opciones.destino = valor(); break;
      case '-c': case '--concurrencia': opciones.concurrencia = Number(valor()); break;
      case '-n': case '--limite':       opciones.limite = Number(valor()); break;
      case '-f': case '--filtro':       opciones.filtro = new RegExp(valor(), 'i'); break;
      case '-p': case '--pausa':        opciones.pausa = Number(valor()); break;
      case '-t': case '--timeout':      opciones.timeout = Number(valor()); break;
      case '--ritmo-real':              opciones.ritmoReal = true; break;
      case '--seco':                    opciones.seco = true; break;
      case '-v': case '--detalle':      opciones.detalle = true; break;
      case '-h': case '--ayuda':        opciones.ayuda = true; break;
      default:
        if (arg.startsWith('-')) {
          console.error(`Opcion desconocida: ${arg}`);
          process.exit(1);
        }
        resto.push(arg);
    }
  }

  opciones.sesion = resto[0] || null;
  return opciones;
}

function ayuda() {
  console.log(`
Uso: node reenviar.js <carpeta-de-sesion> [opciones]

  <carpeta-de-sesion>      Carpeta con las peticiones (ej. peticiones/MQxoN2dUnJ5-)

Opciones:
  -d, --destino <url>      URL destino          (por defecto http://localhost:5106/v1/api/webhook)
  -c, --concurrencia <n>   Peticiones en paralelo                            (por defecto 1)
  -n, --limite <n>         Reenviar solo las n primeras                   (por defecto todas)
  -f, --filtro <regex>     Solo las que casen con el regex (eventType o content-type)
  -p, --pausa <ms>         Pausa entre peticiones                            (por defecto 0)
  -t, --timeout <ms>       Timeout por peticion                          (por defecto 30000)
      --ritmo-real         Respetar los intervalos originales entre peticiones
      --seco               No envia nada, solo muestra que haria
  -v, --detalle            Una linea por peticion
  -h, --ayuda              Esta ayuda

Ejemplos:
  node reenviar.js peticiones/MQxoN2dUnJ5-
  node reenviar.js peticiones/MQxoN2dUnJ5- -d http://10.8.8.2:5106/v1/api/webhook
  node reenviar.js peticiones/MQxoN2dUnJ5- -f linedetection -v
  node reenviar.js peticiones/MQxoN2dUnJ5- -c 4 --ritmo-real
`);
}

// Saca el tipo de evento del cuerpo crudo. Funciona igual para XML suelto y
// para multipart, porque en multipart el XML viaja inline dentro del sobre.
function tipoEvento(cuerpo) {
  const texto = cuerpo.subarray(0, 4096).toString('utf8');
  const xml = texto.match(/<eventType>([^<]*)</);
  if (xml) return xml[1];
  const json = texto.match(/"eventType"\s*:\s*"([^"]*)"/);
  return json ? json[1] : '(desconocido)';
}

function cargarPeticiones(dirSesion, opciones) {
  const entradas = fs.readdirSync(dirSesion, { withFileTypes: true });
  const peticiones = [];

  for (const entrada of entradas) {
    if (!entrada.isDirectory()) continue;

    const carpeta = path.join(dirSesion, entrada.name);
    const meta = path.join(carpeta, 'peticion.json');
    const cuerpo = path.join(carpeta, 'cuerpo.bin');
    if (!fs.existsSync(meta)) continue;

    const json = JSON.parse(fs.readFileSync(meta, 'utf8'));
    const bytes = fs.existsSync(cuerpo) ? fs.readFileSync(cuerpo) : Buffer.alloc(0);

    peticiones.push({
      id: entrada.name,
      metodo: json.metodo || 'POST',
      ruta: json.url || json.ruta || '/',
      cabeceras: json.cabeceras || {},
      contentType: (json.cuerpo && json.cuerpo.contentType) || '',
      recibidaEn: new Date(json.recibidaEn || 0),
      evento: tipoEvento(bytes),
      cuerpo: bytes,
    });
  }

  // El orden original importa: hay eventos correlacionados entre si.
  peticiones.sort((a, b) => a.recibidaEn - b.recibidaEn);

  const filtradas = opciones.filtro
    ? peticiones.filter((p) => opciones.filtro.test(p.evento) || opciones.filtro.test(p.contentType))
    : peticiones;

  return filtradas.slice(0, opciones.limite);
}

function enviar(peticion, opciones) {
  return new Promise((resolve) => {
    const destino = new URL(opciones.destino);
    const cliente = destino.protocol === 'https:' ? https : http;

    const cabeceras = {};
    for (const [nombre, valor] of Object.entries(peticion.cabeceras)) {
      if (CABECERAS_IGNORADAS.has(nombre.toLowerCase())) continue;
      cabeceras[nombre] = valor;
    }
    cabeceras['Content-Length'] = peticion.cuerpo.length;

    const arranque = process.hrtime.bigint();
    const terminar = (estado, error) =>
      resolve({
        peticion,
        estado,
        error,
        ms: Number(process.hrtime.bigint() - arranque) / 1e6,
      });

    const req = cliente.request(
      {
        protocol: destino.protocol,
        hostname: destino.hostname,
        port: destino.port,
        // La ruta del destino manda: permite reenviar a un endpoint distinto.
        path: destino.pathname + destino.search,
        method: peticion.metodo,
        headers: cabeceras,
      },
      (res) => {
        res.resume(); // hay que drenar la respuesta o la conexion queda colgada
        res.on('end', () => terminar(res.statusCode, null));
      }
    );

    req.setTimeout(opciones.timeout, () => {
      req.destroy(new Error(`timeout tras ${opciones.timeout} ms`));
    });

    req.on('error', (err) => terminar(0, err.message));
    req.end(peticion.cuerpo);
  });
}

const esperar = (ms) => new Promise((r) => setTimeout(r, ms));

async function main() {
  const opciones = parsearArgumentos(process.argv);

  if (opciones.ayuda || !opciones.sesion) {
    ayuda();
    process.exit(opciones.ayuda ? 0 : 1);
  }

  const dirSesion = path.resolve(opciones.sesion);
  if (!fs.existsSync(dirSesion)) {
    console.error(`No existe la carpeta: ${dirSesion}`);
    process.exit(1);
  }

  const peticiones = cargarPeticiones(dirSesion, opciones);
  if (!peticiones.length) {
    console.error('No se encontro ninguna peticion que reenviar.');
    process.exit(1);
  }

  const bytes = peticiones.reduce((total, p) => total + p.cuerpo.length, 0);

  console.log(`Sesion:       ${dirSesion}`);
  console.log(`Destino:      ${opciones.destino}`);
  console.log(`Peticiones:   ${peticiones.length}  (${(bytes / 1024 / 1024).toFixed(1)} MB)`);
  console.log(`Concurrencia: ${opciones.concurrencia}${opciones.ritmoReal ? '  (ritmo real)' : ''}`);
  console.log('');

  if (opciones.seco) {
    for (const p of peticiones) {
      console.log(
        `  ${p.id}  ${p.metodo} ${p.ruta}  ${p.evento.padEnd(18)} ` +
          `${String(p.cuerpo.length).padStart(8)} B  ${p.contentType.split(';')[0]}`
      );
    }
    console.log(`\n(simulacion: no se envio nada)`);
    return;
  }

  const resultados = [];
  let siguiente = 0;
  let completadas = 0;

  // El contador se reescribe sobre si mismo con \r, asi que solo tiene sentido
  // en una terminal: redirigido a fichero dejaria una linea por peticion.
  const interactivo = process.stdout.isTTY && !opciones.detalle;

  const progreso = () => {
    if (!interactivo) return;
    const pct = ((completadas / peticiones.length) * 100).toFixed(0);
    process.stdout.write(`\r  enviando... ${completadas}/${peticiones.length}  (${pct}%)`);
  };

  async function trabajador() {
    while (siguiente < peticiones.length) {
      const indice = siguiente++;
      const peticion = peticiones[indice];

      if (opciones.ritmoReal && indice > 0) {
        const hueco = peticion.recibidaEn - peticiones[indice - 1].recibidaEn;
        if (hueco > 0) await esperar(Math.min(hueco, 10000));
      } else if (opciones.pausa > 0) {
        await esperar(opciones.pausa);
      }

      const resultado = await enviar(peticion, opciones);
      resultados.push(resultado);
      completadas++;

      if (opciones.detalle) {
        const marca = resultado.estado >= 200 && resultado.estado < 300 ? 'ok ' : '>>>';
        console.log(
          `  ${marca} ${String(resultado.estado || 'ERR').padEnd(4)} ` +
            `${peticion.evento.padEnd(18)} ${String(resultado.ms.toFixed(0)).padStart(6)} ms  ` +
            `${peticion.id}${resultado.error ? '  ' + resultado.error : ''}`
        );
      } else {
        progreso();
      }
    }
  }

  const arranque = Date.now();
  await Promise.all(
    Array.from({ length: Math.max(1, opciones.concurrencia) }, () => trabajador())
  );
  const total = (Date.now() - arranque) / 1000;

  if (interactivo) process.stdout.write('\r' + ' '.repeat(50) + '\r');

  informe(resultados, total);

  const fallos = resultados.filter((r) => !(r.estado >= 200 && r.estado < 300));
  process.exit(fallos.length ? 1 : 0);
}

function informe(resultados, segundos) {
  const porEstado = {};
  const porEvento = {};

  for (const r of resultados) {
    const clave = r.estado || 'ERROR';
    porEstado[clave] = (porEstado[clave] || 0) + 1;

    const fila = (porEvento[r.peticion.evento] = porEvento[r.peticion.evento] || { ok: 0, mal: 0 });
    if (r.estado >= 200 && r.estado < 300) fila.ok++;
    else fila.mal++;
  }

  const ms = resultados.map((r) => r.ms).sort((a, b) => a - b);
  const pct = (p) => ms[Math.min(ms.length - 1, Math.floor(ms.length * p))].toFixed(0);

  console.log('=== RESPUESTAS ===');
  for (const [estado, n] of Object.entries(porEstado).sort((a, b) => b[1] - a[1])) {
    console.log(`  ${String(n).padStart(5)}  ${estado}`);
  }

  console.log('\n=== POR TIPO DE EVENTO ===');
  console.log('  ' + 'evento'.padEnd(20) + 'ok'.padStart(6) + 'fallo'.padStart(8));
  for (const [evento, f] of Object.entries(porEvento).sort((a, b) => b[1].ok + b[1].mal - a[1].ok - a[1].mal)) {
    const aviso = f.mal ? '   <-- revisar' : '';
    console.log('  ' + evento.padEnd(20) + String(f.ok).padStart(6) + String(f.mal).padStart(8) + aviso);
  }

  console.log('\n=== TIEMPOS ===');
  console.log(`  mediana ${pct(0.5)} ms   p95 ${pct(0.95)} ms   max ${pct(1)} ms`);
  console.log(`  total ${segundos.toFixed(1)} s   (${(resultados.length / segundos).toFixed(1)} pet/s)`);

  const fallos = resultados.filter((r) => !(r.estado >= 200 && r.estado < 300));
  if (fallos.length) {
    console.log(`\n=== FALLOS (${fallos.length}) ===`);
    for (const r of fallos.slice(0, 20)) {
      console.log(
        `  ${r.peticion.id}  ${r.peticion.evento}  ->  ${r.estado || 'ERROR'}` +
          `${r.error ? '  ' + r.error : ''}`
      );
    }
    if (fallos.length > 20) console.log(`  ... y ${fallos.length - 20} mas`);
  } else {
    console.log('\nTodas las peticiones respondieron 2xx.');
  }
}

main().catch((err) => {
  console.error('Error:', err);
  process.exit(1);
});
