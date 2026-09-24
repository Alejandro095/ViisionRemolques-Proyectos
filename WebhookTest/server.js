'use strict';

const fs = require('fs');
const path = require('path');
const readline = require('readline');
const express = require('express');

const { capturarCuerpoCrudo } = require('./lib/cuerpo-crudo');
const { guardarPeticion } = require('./lib/guardar-peticion');
const { imprimirPeticion } = require('./lib/imprimir-peticion');
const { iniciarRegistro } = require('./lib/registro-log');
const { crearEstadisticas } = require('./lib/estadisticas');
const { nanoid } = require('./lib/nanoid');
const { TINTA } = require('./lib/colores');

const RUTA = process.env.RUTA_WEBHOOK || '/v1/api/webhook';
const PUERTO_DEFECTO = Number(process.env.PORT) || 3000;
const HOST = process.env.HOST || '0.0.0.0';
const DIR_PETICIONES = process.env.DIR_PETICIONES
  ? path.resolve(process.env.DIR_PETICIONES)
  : path.join(__dirname, 'peticiones');
const LIMITE_BYTES = Number(process.env.LIMITE_BYTES) || 1024 * 1024 * 1024; // 1 GB

// Una unica interfaz readline para toda la sesion: crear/cerrar una nueva en
// cada prompt (puerto, stop, renombrar/borrar) deja el stdin en un estado raro
// entre medias (hay que pulsar Enter de mas). Se crea una vez y se reutiliza.
let rl;
function obtenerRL() {
  if (!process.stdin.isTTY) return null;
  if (!rl) rl = readline.createInterface({ input: process.stdin, output: process.stdout });
  return rl;
}

// Si PORT viene por entorno (scripts/CI) no se pregunta nada. Si hay terminal
// interactiva se pregunta el puerto (Enter = el de por defecto): asi se pueden
// levantar varias sesiones a la vez, cada una en su propio puerto.
function preguntarPuerto() {
  const interfaz = obtenerRL();
  if (process.env.PORT || !interfaz) {
    return Promise.resolve(PUERTO_DEFECTO);
  }

  return new Promise((resolve) => {
    interfaz.question(`${TINTA.grisOscuro}Puerto (Enter = ${PUERTO_DEFECTO}):${TINTA.reset} `, (respuesta) => {
      const texto = respuesta.trim();
      if (!texto) return resolve(PUERTO_DEFECTO);

      const n = Number(texto);
      if (!Number.isInteger(n) || n <= 0 || n > 65535) {
        console.log(`${TINTA.amarillo}Puerto invalido, usando ${PUERTO_DEFECTO}.${TINTA.reset}`);
        return resolve(PUERTO_DEFECTO);
      }
      resolve(n);
    });
  });
}

let servidor;
let registro;
let estadisticas;
let cerrando = false;
let DIR_SESION_ACTUAL;

function manejarLineaComando(linea) {
  const comando = linea.trim().toLowerCase();
  if (comando === 'stop' || comando === 'salir' || comando === 'exit') {
    cerrar('Comando "stop" recibido');
  }
}

// Al cerrar, si hay terminal interactiva, se ofrece renombrar o borrar la
// carpeta de esta sesion. Enter = dejarla como esta.
function preguntarAccionCarpeta(dirSesion) {
  const interfaz = obtenerRL();
  if (!interfaz) return Promise.resolve();

  return new Promise((resolve) => {
    interfaz.question(
      `${TINTA.grisOscuro}¿Renombrar (r) o borrar (b) la carpeta de esta sesion? (Enter = dejarla):${TINTA.reset} `,
      (respuesta) => {
        const opcion = respuesta.trim().toLowerCase();

        if (opcion === 'b') {
          try {
            fs.rmSync(dirSesion, { recursive: true, force: true });
            console.log(`${TINTA.amarillo}Carpeta borrada.${TINTA.reset}`);
          } catch (err) {
            console.error(`${TINTA.rojo}No se pudo borrar la carpeta:${TINTA.reset}`, err.message);
          }
          return resolve();
        }

        if (opcion === 'r') {
          interfaz.question(`${TINTA.grisOscuro}Nuevo nombre:${TINTA.reset} `, (nombre) => {
            const nuevoNombre = nombre.trim();
            if (nuevoNombre) {
              const destino = path.join(path.dirname(dirSesion), nuevoNombre);
              try {
                fs.renameSync(dirSesion, destino);
                console.log(`${TINTA.amarillo}Carpeta renombrada a ${destino}${TINTA.reset}`);
              } catch (err) {
                console.error(`${TINTA.rojo}No se pudo renombrar la carpeta:${TINTA.reset}`, err.message);
              }
            }
            resolve();
          });
          return;
        }

        resolve();
      }
    );
  });
}

async function iniciar() {
  const PUERTO = await preguntarPuerto();

  // Cada arranque del servidor tiene su propia carpeta: dentro van las peticiones
  // de esta ejecucion y el log con todo lo que se imprimio por pantalla.
  const ID_SESION = nanoid(12);
  const DIR_SESION = path.join(DIR_PETICIONES, ID_SESION);
  fs.mkdirSync(DIR_SESION, { recursive: true });
  DIR_SESION_ACTUAL = DIR_SESION;

  registro = iniciarRegistro(path.join(DIR_SESION, 'log.txt'));
  estadisticas = crearEstadisticas(ID_SESION);

  const app = express();
  app.disable('x-powered-by');
  app.set('trust proxy', true);

  // Un unico endpoint: cualquier verbo, cualquier content-type, cualquier archivo.
  // Sin body-parsers: el stream llega intacto al capturador de bytes crudos.
  app.use(RUTA, capturarCuerpoCrudo({ limite: LIMITE_BYTES }), async (req, res) => {
    try {
      const { meta, carpeta, parseado, texto, archivosContenido } = await guardarPeticion(
        req,
        DIR_SESION
      );
      estadisticas.registrar(meta);
      imprimirPeticion({ meta, carpeta, parseado, texto, archivosContenido });
      res.status(200).end();
    } catch (err) {
      console.error(`${TINTA.rojo}Error guardando la peticion:${TINTA.reset}`, err);
      res.status(500).end();
    }
  });

  servidor = app.listen(PUERTO, HOST, () => {
    console.log(
      `${TINTA.esmeraldaOscura}---${TINTA.reset} ${TINTA.bold}${TINTA.esmeralda}Sesion ${ID_SESION}${TINTA.reset} arrancada: ` +
        `${TINTA.grisOscuro}${new Date().toISOString()}${TINTA.reset} ${TINTA.esmeraldaOscura}---${TINTA.reset}`
    );
    console.log(
      `Escuchando en ${TINTA.esmeralda}http://localhost:${PUERTO}${RUTA}${TINTA.reset} (cualquier verbo, cualquier content-type)`
    );
    console.log(`Guardando esta sesion en ${TINTA.blanco}${DIR_SESION}${TINTA.reset}`);
    console.log(`Log de esta sesion: ${TINTA.blanco}${path.join(DIR_SESION, 'log.txt')}${TINTA.reset}`);

    // Alternativa a Ctrl+C: en cmd.exe (via npm) Ctrl+C abre el dialogo
    // "Terminate batch job" y mata el proceso en seco sin pasar por cerrar().
    // Escribiendo "stop" se evita ese lio por completo.
    const interfaz = obtenerRL();
    if (interfaz) {
      console.log(`Escribe ${TINTA.esmeralda}stop${TINTA.reset} + Enter para detener el servidor.`);
      interfaz.on('line', manejarLineaComando);
    }
  });

  // El servidor nunca llego a arrancar (puerto ocupado, sin permisos...): no
  // hubo sesion real, asi que borramos la carpeta vacia que se creo para no
  // ensuciar peticiones/ con sesiones que jamas recibieron nada.
  servidor.on('error', (err) => {
    console.error(`${TINTA.rojo}No se pudo iniciar el servidor en ${HOST}:${PUERTO}${TINTA.reset}`);
    if (err.code === 'EADDRINUSE') {
      console.error(`${TINTA.grisOscuro}El puerto ${PUERTO} ya esta en uso por otro proceso.${TINTA.reset}`);
    } else if (err.code === 'EACCES') {
      console.error(`${TINTA.grisOscuro}Sin permisos para escuchar en el puerto ${PUERTO}.${TINTA.reset}`);
    } else {
      console.error(`${TINTA.grisOscuro}${err.message}${TINTA.reset}`);
    }

    registro.cerrar(); // libera el log.txt antes de borrar la carpeta (si no, Windows bloquea el rmSync)
    try {
      fs.rmSync(DIR_SESION, { recursive: true, force: true });
    } catch {
      // si no se pudo borrar, no tapamos el error original con este
    }
    process.exit(1);
  });

  // Sin timeout de cabeceras/cuerpo para no cortar subidas grandes o lentas.
  servidor.requestTimeout = 0;
  servidor.headersTimeout = 0;

  // El parser HTTP de Node rechaza (400) los verbos que no estan en
  // http.METHODS antes de que Express los vea. Al menos lo dejamos por consola.
  servidor.on('clientError', (err, socket) => {
    console.warn(
      `${TINTA.amarillo}Peticion rechazada por el parser HTTP de Node (${err.code || err.message}).${TINTA.reset} ` +
        'Recuerda: solo se aceptan los verbos de http.METHODS.'
    );
    if (socket.destroyed || !socket.writable) return;
    socket.end('HTTP/1.1 400 Bad Request\r\nConnection: close\r\n\r\n');
  });
}

iniciar();

function cerrar(motivo, codigo = 0) {
  if (cerrando) return;
  cerrando = true;
  if (rl) rl.removeListener('line', manejarLineaComando);
  console.log(`\n${TINTA.amarillo}${motivo}: cerrando servidor...${TINTA.reset}`);

  const salir = () => preguntarAccionCarpeta(DIR_SESION_ACTUAL).then(() => {
    if (rl) rl.close();
    process.exit(codigo);
  });

  // Si la señal llega mientras todavia se estaba preguntando el puerto, el
  // servidor ni siquiera existe: no hay nada que cerrar ni sesion que resumir.
  if (!servidor) {
    if (registro) registro.cerrar();
    salir();
    return;
  }

  let rematado = false;
  const rematar = () => {
    if (rematado) return;
    rematado = true;
    // Las estadisticas van aqui, no antes de cerrar: asi entran tambien las
    // peticiones que todavia estaban en vuelo cuando llego la señal.
    estadisticas.imprimir();
    registro.cerrar(); // el log ya esta en disco (escrituras sincronas), esto solo lo remata
    salir();
  };

  servidor.close(rematar);
  // Los clientes con keep-alive (camaras, etc.) dejarian el close esperando.
  if (servidor.closeIdleConnections) servidor.closeIdleConnections();

  // Si alguna peticion en curso se eterniza, cerramos igualmente a los 3s.
  setTimeout(() => {
    if (servidor.closeAllConnections) servidor.closeAllConnections();
    rematar();
  }, 3000);
}

for (const senal of ['SIGINT', 'SIGTERM', 'SIGHUP', 'SIGBREAK']) {
  process.on(senal, () => cerrar(`Señal ${senal} recibida`));
}

// Aunque el proceso muera mal, lo que se vio por pantalla queda en el log.
process.on('uncaughtException', (err) => {
  console.error(`${TINTA.rojo}Excepcion no capturada:${TINTA.reset}`, err);
  cerrar('Excepcion no capturada', 1);
});
process.on('unhandledRejection', (err) => {
  console.error(`${TINTA.rojo}Promesa rechazada sin manejar:${TINTA.reset}`, err);
});
process.on('exit', () => {
  if (registro) registro.cerrar();
});
