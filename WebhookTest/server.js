'use strict';

const fs = require('fs');
const path = require('path');
const express = require('express');

const { capturarCuerpoCrudo } = require('./lib/cuerpo-crudo');
const { guardarPeticion } = require('./lib/guardar-peticion');
const { imprimirPeticion } = require('./lib/imprimir-peticion');
const { iniciarRegistro } = require('./lib/registro-log');
const { crearEstadisticas } = require('./lib/estadisticas');
const { nanoid } = require('./lib/nanoid');

const RUTA = process.env.RUTA_WEBHOOK || '/v1/api/webhook';
const PUERTO = Number(process.env.PORT) || 5106;
const HOST = process.env.HOST || '0.0.0.0';
const DIR_PETICIONES = process.env.DIR_PETICIONES
  ? path.resolve(process.env.DIR_PETICIONES)
  : path.join(__dirname, 'peticiones');
const LIMITE_BYTES = Number(process.env.LIMITE_BYTES) || 1024 * 1024 * 1024; // 1 GB

// Cada arranque del servidor tiene su propia carpeta: dentro van las peticiones
// de esta ejecucion y el log con todo lo que se imprimio por pantalla.
const ID_SESION = nanoid(12);
const DIR_SESION = path.join(DIR_PETICIONES, ID_SESION);
fs.mkdirSync(DIR_SESION, { recursive: true });

const registro = iniciarRegistro(path.join(DIR_SESION, 'log.txt'));
const estadisticas = crearEstadisticas(ID_SESION);

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
    console.error('Error guardando la peticion:', err);
    res.status(500).end();
  }
});

const servidor = app.listen(PUERTO, HOST, () => {
  console.log(`--- Sesion ${ID_SESION} arrancada: ${new Date().toISOString()} ---`);
  console.log(`Escuchando en http://localhost:${PUERTO}${RUTA} (cualquier verbo, cualquier content-type)`);
  console.log(`Guardando esta sesion en ${DIR_SESION}`);
  console.log(`Log de esta sesion: ${path.join(DIR_SESION, 'log.txt')}`);
});

// Sin timeout de cabeceras/cuerpo para no cortar subidas grandes o lentas.
servidor.requestTimeout = 0;
servidor.headersTimeout = 0;

// El parser HTTP de Node rechaza (400) los verbos que no estan en
// http.METHODS antes de que Express los vea. Al menos lo dejamos por consola.
servidor.on('clientError', (err, socket) => {
  console.warn(
    `Peticion rechazada por el parser HTTP de Node (${err.code || err.message}). ` +
      'Recuerda: solo se aceptan los verbos de http.METHODS.'
  );
  if (socket.destroyed || !socket.writable) return;
  socket.end('HTTP/1.1 400 Bad Request\r\nConnection: close\r\n\r\n');
});

let cerrando = false;

function cerrar(motivo, codigo = 0) {
  if (cerrando) return;
  cerrando = true;
  console.log(`\n${motivo}: cerrando servidor...`);

  let rematado = false;
  const rematar = () => {
    if (rematado) return;
    rematado = true;
    // Las estadisticas van aqui, no antes de cerrar: asi entran tambien las
    // peticiones que todavia estaban en vuelo cuando llego la señal.
    estadisticas.imprimir();
    registro.cerrar(); // el log ya esta en disco (escrituras sincronas), esto solo lo remata
    process.exit(codigo);
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
  console.error('Excepcion no capturada:', err);
  cerrar('Excepcion no capturada', 1);
});
process.on('unhandledRejection', (err) => {
  console.error('Promesa rechazada sin manejar:', err);
});
process.on('exit', () => registro.cerrar());
