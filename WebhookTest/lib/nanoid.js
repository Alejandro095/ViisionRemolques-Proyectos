'use strict';

const crypto = require('crypto');

// Alfabeto estandar de nanoid: 64 caracteres exactos, asi que 6 bits por
// caracter se mapean sin sesgo y no hace falta rechazar muestras.
const ALFABETO = 'useandom-26T198340PX75pxJACKVERYMINDBUSHWOLF_GQZbfghjklqvwyzrict';

/**
 * Genera un identificador corto tipo nanoid.
 * @param {number} tamano numero de caracteres del id
 * @returns {string}
 */
function nanoid(tamano = 12) {
  const bytes = crypto.randomBytes(tamano);
  let id = '';
  for (let i = 0; i < tamano; i++) {
    id += ALFABETO[bytes[i] & 63];
  }
  return id;
}

module.exports = { nanoid };
