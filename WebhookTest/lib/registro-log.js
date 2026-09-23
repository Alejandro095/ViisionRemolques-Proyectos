'use strict';

const fs = require('fs');
const util = require('util');

/**
 * Duplica todo lo que se imprime por consola hacia un archivo.
 *
 * Las escrituras son SINCRONAS a proposito: asi el archivo siempre tiene en
 * disco todo lo que se ha visto por pantalla, aunque el proceso muera de golpe
 * (Ctrl+C, excepcion no capturada o kill). No hay buffer que se pueda perder.
 *
 * @param {string} rutaArchivo destino del log
 * @returns {{ cerrar: () => void }}
 */
function iniciarRegistro(rutaArchivo) {
  const fd = fs.openSync(rutaArchivo, 'a');
  const originales = {
    log: console.log,
    info: console.info,
    warn: console.warn,
    error: console.error,
  };
  let cerrado = false;

  const escribir = (texto) => {
    if (cerrado) return;
    try {
      fs.writeSync(fd, texto);
    } catch {
      // Si el log falla no tumbamos el servidor: la consola sigue funcionando.
    }
  };

  for (const nivel of Object.keys(originales)) {
    console[nivel] = (...args) => {
      originales[nivel].apply(console, args);
      escribir(util.format(...args) + '\n');
    };
  }

  return {
    cerrar() {
      if (cerrado) return;
      escribir(`\n--- Log cerrado: ${new Date().toISOString()} ---\n`);
      cerrado = true;
      Object.assign(console, originales);
      try {
        fs.closeSync(fd);
      } catch {
        // ya estaba cerrado
      }
    },
  };
}

module.exports = { iniciarRegistro };
