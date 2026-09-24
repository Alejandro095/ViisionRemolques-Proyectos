'use strict';

// Reenvia a una API todas las peticiones guardadas en una carpeta de sesion.
// Manda los bytes exactos de cuerpo.bin con las cabeceras originales, asi que
// la API recibe justo lo mismo que mandaron las camaras (boundary incluido).

const fs = require('fs');
const path = require('path');
const http = require('http');
const https = require('https');
const readline = require('readline');

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
Uso: node reenviar.js [carpeta-de-sesion] [opciones]

  [carpeta-de-sesion]      Carpeta con las peticiones (ej. peticiones/MQxoN2dUnJ5-)
                           Si se omite, se abre un selector visual sobre peticiones/
                           donde se elige la carpeta y si se reenvia entera o
                           solo una peticion suya.

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
  node reenviar.js                          (selector visual)
`);
}

// Codigos ANSI (24-bit) para el selector: acento esmeralda sobre una caja
// con bordes, en vez de una lista plana gris. NO_COLOR desactiva el color.
const SIN_COLOR = !!process.env.NO_COLOR;
const col = (codigo) => (SIN_COLOR ? '' : codigo);
const TINTA = {
  reset: col('\x1b[0m'),
  bold: col('\x1b[1m'),
  dim: col('\x1b[2m'),
  esmeralda: col('\x1b[38;2;16;185;129m'),
  esmeraldaFondo: col('\x1b[48;2;16;185;129m'),
  esmeraldaOscura: col('\x1b[38;2;6;95;70m'),
  negro: col('\x1b[38;2;20;20;20m'),
  blanco: col('\x1b[38;2;250;250;250m'),
  grisClaro: col('\x1b[38;2;190;190;190m'),
  grisOscuro: col('\x1b[38;2;110;110;110m'),
  amarillo: col('\x1b[38;2;253;224;71m'),
  rojo: col('\x1b[38;2;248;113;113m'),
};

// Valor especial que devuelve seleccionarMenu cuando el usuario pide volver
// al paso anterior (solo si se llama con { permitirAtras: true }).
const ATRAS = Symbol('atras');

// Pausa hasta que el usuario pulse Enter (continua) o Esc/Ctrl+C (sale del
// programa). Se usa entre reenvios para no cerrar el programa de golpe.
function esperarTecla(mensaje) {
  return new Promise((resolve) => {
    if (!process.stdin.isTTY) {
      resolve();
      return;
    }

    console.log(`\n${TINTA.grisOscuro}${mensaje}${TINTA.reset}`);

    const limpiar = () => {
      process.stdin.setRawMode(false);
      process.stdin.pause();
      process.stdin.removeListener('keypress', onTecla);
    };

    const onTecla = (_str, tecla) => {
      if (!tecla) return;
      if (tecla.name === 'return') {
        limpiar();
        resolve();
      } else if (tecla.name === 'escape' || (tecla.ctrl && tecla.name === 'c')) {
        limpiar();
        console.log('\nHasta luego.');
        process.exit(0);
      }
    };

    readline.emitKeypressEvents(process.stdin);
    process.stdin.setRawMode(true);
    process.stdin.resume();
    process.stdin.on('keypress', onTecla);
  });
}

// Como esperarTecla, pero sin limpiar pantalla (se queda viendo el informe) y
// con una tecla extra para repetir el mismo envio. Devuelve 'repetir' o 'inicio'.
function esperarAccion(mensaje) {
  return new Promise((resolve) => {
    if (!process.stdin.isTTY) {
      resolve('inicio');
      return;
    }

    console.log(`\n${mensaje}`);

    const limpiar = () => {
      process.stdin.setRawMode(false);
      process.stdin.pause();
      process.stdin.removeListener('keypress', onTecla);
    };

    const onTecla = (_str, tecla) => {
      if (!tecla) return;
      if (tecla.name === 'r') {
        limpiar();
        resolve('repetir');
      } else if (tecla.name === 'return') {
        limpiar();
        resolve('inicio');
      } else if (tecla.name === 'escape' || (tecla.ctrl && tecla.name === 'c')) {
        limpiar();
        console.log('\nHasta luego.');
        process.exit(0);
      }
    };

    readline.emitKeypressEvents(process.stdin);
    process.stdin.setRawMode(true);
    process.stdin.resume();
    process.stdin.on('keypress', onTecla);
  });
}

const limpiarPantalla = () => process.stdout.write('\x1b[2J\x1b[3J\x1b[H');

// Menu de flechas por terminal (sin dependencias externas). Devuelve el
// "value" de la opcion elegida con Enter; Esc/Ctrl+C cancela el proceso;
// Backspace/Izquierda devuelve ATRAS si permitirAtras esta activo.
function seleccionarMenu(titulo, opciones, { permitirAtras = false } = {}) {
  return new Promise((resolve, reject) => {
    if (!process.stdin.isTTY || !process.stdout.isTTY) {
      reject(new Error('Hace falta una terminal interactiva (TTY) para el selector visual. Pasa la carpeta de sesion como argumento.'));
      return;
    }

    // Numeracion "1. ", "2. "... alineada segun cuantas opciones haya. Si la
    // opcion trae "sufijo" (p.ej. "(4 peticiones)"), ese trozo se ancla al
    // borde derecho de la caja en vez de ir pegado al label.
    const numAncho = String(opciones.length).length;
    const filasPlano = opciones.map((op, i) => `${String(i + 1).padStart(numAncho)}. ${op.label}`);
    const anchoContenidoMin = Math.max(
      titulo.length + 2,
      ...filasPlano.map((f, i) => f.length + 2 + (opciones[i].sufijo ? 1 + opciones[i].sufijo.length : 0))
    );
    const b = TINTA.esmeraldaOscura; // color de los bordes de la caja

    let indice = 0;

    // Overhead fijo fuera del cuerpo de opciones: borde sup., titulo,
    // separador, borde inf., linea en blanco y ayuda = 6 lineas.
    const OVERHEAD = 6;

    const dibujar = () => {
      limpiarPantalla();

      const filasTerm = process.stdout.rows || 24;
      const altoDisponible = Math.max(filasTerm - 1, OVERHEAD + 3); // -1: margen para no forzar scroll
      const altoOpciones = Math.max(3, altoDisponible - OVERHEAD);

      // El ancho tambien ocupa todo el terminal (con margen de 4: bordes + 1
      // espacio de relleno a cada lado), sin bajar del ancho que pide el contenido.
      const anchoTerminal = process.stdout.columns || 80;
      const anchoContenido = Math.max(anchoContenidoMin, anchoTerminal - 4);
      const anchoInterior = anchoContenido + 2;

      // Ventana de paginacion: centra la seleccion cuando no caben todas.
      const inicio =
        opciones.length <= altoOpciones
          ? 0
          : Math.min(Math.max(0, indice - Math.floor(altoOpciones / 2)), opciones.length - altoOpciones);
      const fin = Math.min(opciones.length, inicio + altoOpciones);

      const lineas = [];
      lineas.push(`${b}╭${'─'.repeat(anchoInterior)}╮${TINTA.reset}`);

      const tituloPlano = `▍ ${titulo}`;
      lineas.push(
        `${b}│${TINTA.reset} ${TINTA.bold}${TINTA.esmeralda}${tituloPlano}${TINTA.reset}` +
          `${' '.repeat(anchoContenido - tituloPlano.length)} ${b}│${TINTA.reset}`
      );
      lineas.push(`${b}├${'─'.repeat(anchoInterior)}┤${TINTA.reset}`);

      for (let i = inicio; i < fin; i++) {
        const plano = filasPlano[i];
        const sufijo = opciones[i].sufijo || '';
        const pisoRelleno = sufijo ? 1 : 0; // con sufijo hace falta al menos un espacio de separacion
        const relleno = ' '.repeat(Math.max(pisoRelleno, anchoContenido - 2 - plano.length - sufijo.length));
        const cuerpo = sufijo ? `${plano}${relleno}${sufijo}` : `${plano}${relleno}`;
        if (i === indice) {
          lineas.push(
            `${b}│${TINTA.reset} ${TINTA.esmeraldaFondo}${TINTA.negro}${TINTA.bold}❯ ${cuerpo}${TINTA.reset} ${b}│${TINTA.reset}`
          );
        } else {
          lineas.push(`${b}│${TINTA.reset}   ${TINTA.grisClaro}${cuerpo}${TINTA.reset} ${b}│${TINTA.reset}`);
        }
      }

      // Relleno con filas vacias para que la caja siempre ocupe el alto
      // disponible del terminal, aunque haya pocas opciones.
      const filaVacia = `${b}│${TINTA.reset}${' '.repeat(anchoContenido + 2)}${b}│${TINTA.reset}`;
      for (let k = fin - inicio; k < altoOpciones; k++) lineas.push(filaVacia);

      lineas.push(`${b}╰${'─'.repeat(anchoInterior)}╯${TINTA.reset}`);

      const paginacion =
        opciones.length > altoOpciones
          ? `${TINTA.grisOscuro}(${indice + 1}/${opciones.length})${TINTA.reset}   `
          : '';
      const atras = permitirAtras ? `   ${TINTA.amarillo}←${TINTA.reset} atrás` : '';
      lineas.push(
        '',
        `${paginacion}${TINTA.grisOscuro}↑/↓${TINTA.reset} mover   ${TINTA.esmeralda}Enter${TINTA.reset} elegir${atras}   ${TINTA.rojo}Esc${TINTA.reset} salir`
      );
      process.stdout.write(lineas.join('\n') + '\n');
    };

    const limpiar = () => {
      process.stdin.setRawMode(false);
      process.stdin.pause();
      process.stdin.removeListener('keypress', onTecla);
      process.stdout.removeListener('resize', dibujar);
    };

    const onTecla = (_str, tecla) => {
      if (!tecla) return;
      if (tecla.name === 'up') {
        indice = (indice - 1 + opciones.length) % opciones.length;
        dibujar();
      } else if (tecla.name === 'down') {
        indice = (indice + 1) % opciones.length;
        dibujar();
      } else if (tecla.name === 'return') {
        limpiar();
        resolve(opciones[indice].value);
      } else if (permitirAtras && (tecla.name === 'backspace' || tecla.name === 'left')) {
        limpiar();
        resolve(ATRAS);
      } else if (tecla.name === 'escape' || (tecla.ctrl && tecla.name === 'c')) {
        limpiar();
        console.log('\nCancelado.');
        process.exit(0);
      }
    };

    readline.emitKeypressEvents(process.stdin);
    process.stdin.setRawMode(true);
    process.stdin.resume();
    process.stdin.on('keypress', onTecla);
    process.stdout.on('resize', dibujar);
    dibujar();
  });
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

// Lee todas las peticiones validas de una carpeta de sesion, sin filtrar.
function cargarPeticionesCrudo(dirSesion) {
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
  return peticiones;
}

function aplicarFiltros(peticiones, opciones) {
  const filtradas = opciones.filtro
    ? peticiones.filter((p) => opciones.filtro.test(p.evento) || opciones.filtro.test(p.contentType))
    : peticiones;

  return filtradas.slice(0, opciones.limite);
}

function cargarPeticiones(dirSesion, opciones) {
  return aplicarFiltros(cargarPeticionesCrudo(dirSesion), opciones);
}

// Selector visual: elige carpeta de sesion dentro de peticiones/ y despues
// si se reenvia entera o solo una de sus peticiones.
async function elegirInteractivo() {
  const raiz = path.join(__dirname, 'peticiones');
  if (!fs.existsSync(raiz)) {
    throw new Error(`No existe la carpeta de peticiones: ${raiz}`);
  }

  const sesiones = fs
    .readdirSync(raiz, { withFileTypes: true })
    .filter((e) => e.isDirectory())
    .map((e) => e.name)
    .sort();

  if (!sesiones.length) {
    throw new Error(`No hay carpetas de sesion dentro de ${raiz}`);
  }

  const opcionesSesion = sesiones.map((nombre) => {
    const dir = path.join(raiz, nombre);
    const n = fs.readdirSync(dir, { withFileTypes: true }).filter((e) => e.isDirectory()).length;
    // El sufijo va anclado al borde derecho de la caja (ver seleccionarMenu).
    return { label: nombre, sufijo: `(${n} peticiones)`, value: nombre };
  });

  // Maquina de estados simple para poder ir hacia atras entre pasos.
  let paso = 'sesion';
  let dirSesion, todas;

  while (true) {
    if (paso === 'sesion') {
      const sesionElegida = await seleccionarMenu('Elige la carpeta de sesion:', opcionesSesion);
      dirSesion = path.join(raiz, sesionElegida);
      todas = cargarPeticionesCrudo(dirSesion);
      if (!todas.length) {
        throw new Error(`La carpeta "${sesionElegida}" no tiene peticiones validas.`);
      }
      paso = 'modo';
      continue;
    }

    if (paso === 'modo') {
      const modo = await seleccionarMenu(
        'Que quieres reenviar?',
        [
          { label: `Toda la carpeta (${todas.length} peticiones)`, value: 'todas' },
          { label: 'Elegir una unica peticion', value: 'una' },
        ],
        { permitirAtras: true }
      );
      if (modo === ATRAS) { paso = 'sesion'; continue; }
      if (modo === 'todas') { limpiarPantalla(); return { dirSesion, todas, soloId: null }; }
      paso = 'peticion';
      continue;
    }

    if (paso === 'peticion') {
      const opcionesPeticion = todas.map((p) => ({
        label: `${p.id}  ${p.evento.padEnd(18)} ${p.recibidaEn.toLocaleString()}`,
        value: p.id,
      }));
      const soloId = await seleccionarMenu('Elige la peticion:', opcionesPeticion, { permitirAtras: true });
      if (soloId === ATRAS) { paso = 'modo'; continue; }
      limpiarPantalla();
      return { dirSesion, todas, soloId };
    }
  }
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

// Color segun rango HTTP: verde 2xx, amarillo 3xx, rojo el resto/ERROR.
function colorEstado(estado) {
  if (estado >= 200 && estado < 300) return TINTA.esmeralda;
  if (estado >= 300 && estado < 400) return TINTA.amarillo;
  return TINTA.rojo;
}

function barraProgreso(completadas, total, ancho = 28) {
  const llenas = total ? Math.round((completadas / total) * ancho) : 0;
  const barra = `${TINTA.esmeralda}${'█'.repeat(llenas)}${TINTA.grisOscuro}${'░'.repeat(ancho - llenas)}${TINTA.reset}`;
  const pct = String(total ? Math.round((completadas / total) * 100) : 0).padStart(3);
  return `  ${barra}  ${TINTA.bold}${pct}%${TINTA.reset}  ${TINTA.grisClaro}${completadas}/${total}${TINTA.reset}`;
}

function mostrarResumen(dirSesion, opciones, peticiones, bytes) {
  const filas = [
    ['Sesión', dirSesion],
    ['Destino', opciones.destino],
    ['Peticiones', `${peticiones.length}  (${(bytes / 1024 / 1024).toFixed(1)} MB)`],
    ['Concurrencia', `${opciones.concurrencia}${opciones.ritmoReal ? '  · ritmo real' : ''}`],
  ];
  const etiquetaAncho = Math.max(...filas.map(([k]) => k.length));

  console.log(`${TINTA.bold}${TINTA.esmeralda}▍ Reenvío${TINTA.reset}`);
  for (const [clave, valor] of filas) {
    console.log(`  ${TINTA.grisOscuro}${clave.padEnd(etiquetaAncho)}${TINTA.reset}  ${TINTA.blanco}${valor}${TINTA.reset}`);
  }
  console.log('');
}

async function main() {
  const opciones = parsearArgumentos(process.argv);

  if (opciones.ayuda) {
    ayuda();
    process.exit(0);
  }

  // Con carpeta pasada por argumento (uso en scripts/CI) el programa hace un
  // solo reenvio y sale con codigo de exito/fallo. Sin argumento (selector
  // visual) se queda vivo: al terminar, Enter vuelve al selector.
  const modoInteractivo = !opciones.sesion;

  seleccion: while (true) {
    let dirSesion;
    let peticiones;

    if (opciones.sesion) {
      dirSesion = path.resolve(opciones.sesion);
      if (!fs.existsSync(dirSesion)) {
        console.error(`No existe la carpeta: ${dirSesion}`);
        process.exit(1);
      }
      peticiones = cargarPeticiones(dirSesion, opciones);
    } else {
      let eleccion;
      try {
        eleccion = await elegirInteractivo();
      } catch (err) {
        console.error(err.message);
        process.exit(1);
      }
      dirSesion = eleccion.dirSesion;
      peticiones = eleccion.soloId
        ? eleccion.todas.filter((p) => p.id === eleccion.soloId)
        : aplicarFiltros(eleccion.todas, opciones);
    }

    if (!peticiones.length) {
      console.error('No se encontro ninguna peticion que reenviar.');
      if (!modoInteractivo) process.exit(1);
      await esperarTecla('Enter para volver al inicio · Esc para salir');
      continue;
    }

    if (opciones.seco) {
      const bytes = peticiones.reduce((total, p) => total + p.cuerpo.length, 0);
      mostrarResumen(dirSesion, opciones, peticiones, bytes);
      for (const p of peticiones) {
        console.log(
          `  ${TINTA.grisOscuro}${p.id}${TINTA.reset}  ${p.metodo} ${p.ruta}  ${TINTA.grisClaro}${p.evento.padEnd(18)}${TINTA.reset} ` +
            `${String(p.cuerpo.length).padStart(8)} B  ${p.contentType.split(';')[0]}`
        );
      }
      console.log(`\n${TINTA.amarillo}(simulación: no se envió nada)${TINTA.reset}`);
      if (!modoInteractivo) return;
      await esperarTecla('Enter para volver al inicio · Esc para salir');
      continue;
    }

    // Bucle de reenvio: "Reenviar la misma seleccion" repite este bloque sin
    // volver a preguntar carpeta/peticion; "Volver al inicio" rompe a seleccion.
    reenvio: while (true) {
      const bytes = peticiones.reduce((total, p) => total + p.cuerpo.length, 0);
      mostrarResumen(dirSesion, opciones, peticiones, bytes);

      const resultados = [];
      let siguiente = 0;
      let completadas = 0;

      // El contador se reescribe sobre si mismo con \r, asi que solo tiene
      // sentido en una terminal: redirigido a fichero dejaria una linea por peticion.
      const interactivo = process.stdout.isTTY && !opciones.detalle;

      const progreso = () => {
        if (!interactivo) return;
        readline.cursorTo(process.stdout, 0);
        readline.clearLine(process.stdout, 1);
        process.stdout.write(barraProgreso(completadas, peticiones.length));
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
            const c = colorEstado(resultado.estado);
            const marca = resultado.estado >= 200 && resultado.estado < 300 ? '✓' : '✗';
            console.log(
              `  ${c}${marca} ${String(resultado.estado || 'ERR').padEnd(4)}${TINTA.reset} ` +
                `${TINTA.grisClaro}${peticion.evento.padEnd(18)}${TINTA.reset} ${String(resultado.ms.toFixed(0)).padStart(6)} ms  ` +
                `${TINTA.grisOscuro}${peticion.id}${TINTA.reset}${resultado.error ? '  ' + TINTA.rojo + resultado.error + TINTA.reset : ''}`
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

      if (interactivo) {
        readline.cursorTo(process.stdout, 0);
        readline.clearLine(process.stdout, 0);
      }

      informe(resultados, total);

      const fallos = resultados.filter((r) => !(r.estado >= 200 && r.estado < 300));
      if (!modoInteractivo) process.exit(fallos.length ? 1 : 0);

      const hint =
        `${TINTA.esmeralda}R${TINTA.reset} reenviar (${peticiones.length} peticiones)   ` +
        `${TINTA.esmeralda}Enter${TINTA.reset} volver al inicio   ${TINTA.rojo}Esc${TINTA.reset} salir`;
      const accion = await esperarAccion(hint);

      if (accion === 'repetir') {
        limpiarPantalla();
        continue reenvio;
      }
      continue seleccion;
    }
  }
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
  const titulo = (t) => `${TINTA.bold}${TINTA.esmeralda}▍ ${t}${TINTA.reset}`;

  console.log(titulo('RESPUESTAS'));
  for (const [estado, n] of Object.entries(porEstado).sort((a, b) => b[1] - a[1])) {
    const c = colorEstado(Number(estado) || 0);
    console.log(`  ${c}${String(n).padStart(5)}${TINTA.reset}  ${c}${estado}${TINTA.reset}`);
  }

  console.log(`\n${titulo('POR TIPO DE EVENTO')}`);
  const eventos = Object.entries(porEvento).sort((a, b) => b[1].ok + b[1].mal - (a[1].ok + a[1].mal));
  const maxTotal = Math.max(1, ...eventos.map(([, f]) => f.ok + f.mal));
  const anchoBarra = 18;
  console.log(`  ${TINTA.grisOscuro}${'evento'.padEnd(20)}${'ok'.padStart(5)}${'fallo'.padStart(7)}${TINTA.reset}`);
  for (const [evento, f] of eventos) {
    const llenas = Math.round(((f.ok + f.mal) / maxTotal) * anchoBarra);
    const barra = `${TINTA.esmeralda}${'█'.repeat(llenas)}${TINTA.grisOscuro}${'░'.repeat(anchoBarra - llenas)}${TINTA.reset}`;
    const colorMal = f.mal ? TINTA.rojo : TINTA.grisOscuro;
    const aviso = f.mal ? `  ${TINTA.rojo}◀ revisar${TINTA.reset}` : '';
    console.log(
      `  ${TINTA.grisClaro}${evento.padEnd(20)}${TINTA.reset}${TINTA.esmeralda}${String(f.ok).padStart(5)}${TINTA.reset}` +
        `${colorMal}${String(f.mal).padStart(7)}${TINTA.reset}  ${barra}${aviso}`
    );
  }

  console.log(`\n${titulo('TIEMPOS')}`);
  console.log(
    `  mediana ${TINTA.blanco}${pct(0.5)} ms${TINTA.reset}   p95 ${TINTA.blanco}${pct(0.95)} ms${TINTA.reset}   max ${TINTA.blanco}${pct(1)} ms${TINTA.reset}`
  );
  console.log(
    `  total ${TINTA.blanco}${segundos.toFixed(1)} s${TINTA.reset}   (${TINTA.esmeralda}${(resultados.length / segundos).toFixed(1)} pet/s${TINTA.reset})`
  );

  const fallos = resultados.filter((r) => !(r.estado >= 200 && r.estado < 300));
  if (fallos.length) {
    console.log(`\n${TINTA.bold}${TINTA.rojo}▍ FALLOS (${fallos.length})${TINTA.reset}`);
    for (const r of fallos.slice(0, 20)) {
      console.log(
        `  ${TINTA.rojo}✗${TINTA.reset} ${TINTA.grisOscuro}${r.peticion.id}${TINTA.reset}  ${r.peticion.evento}  → ` +
          `${TINTA.rojo}${r.estado || 'ERROR'}${TINTA.reset}${r.error ? '  ' + TINTA.grisOscuro + r.error + TINTA.reset : ''}`
      );
    }
    if (fallos.length > 20) console.log(`  ${TINTA.grisOscuro}... y ${fallos.length - 20} más${TINTA.reset}`);
  } else {
    console.log(`\n${TINTA.bold}${TINTA.esmeralda}✓ Todas las peticiones respondieron 2xx.${TINTA.reset}`);
  }
}

main().catch((err) => {
  console.error('Error:', err);
  process.exit(1);
});
