'use strict';

// Misma paleta (acento esmeralda) que usa reenviar.js, para que la consola
// del servidor y la del script de reenvio se vean como un mismo sistema.
// NO_COLOR desactiva el color (texto plano).
const SIN_COLOR = !!process.env.NO_COLOR;
const col = (codigo) => (SIN_COLOR ? '' : codigo);

const TINTA = {
  reset: col('\x1b[0m'),
  bold: col('\x1b[1m'),
  dim: col('\x1b[2m'),
  esmeralda: col('\x1b[38;2;16;185;129m'),
  esmeraldaOscura: col('\x1b[38;2;6;95;70m'),
  blanco: col('\x1b[38;2;250;250;250m'),
  grisClaro: col('\x1b[38;2;190;190;190m'),
  grisOscuro: col('\x1b[38;2;110;110;110m'),
  amarillo: col('\x1b[38;2;253;224;71m'),
  rojo: col('\x1b[38;2;248;113;113m'),
};

module.exports = { TINTA };
