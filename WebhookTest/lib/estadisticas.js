'use strict';

/** Content-type sin parametros: "multipart/form-data; boundary=xxx" -> "multipart/form-data". */
function tipoBase(contentType) {
  if (!contentType) return '(ninguno)';
  return contentType.split(';')[0].trim().toLowerCase() || '(ninguno)';
}

function formatearBytes(bytes) {
  if (bytes < 1024) return `${bytes} B`;
  const unidades = ['KB', 'MB', 'GB', 'TB'];
  let valor = bytes / 1024;
  let i = 0;
  while (valor >= 1024 && i < unidades.length - 1) {
    valor /= 1024;
    i++;
  }
  return `${valor.toFixed(1)} ${unidades[i]}`;
}

function formatearDuracion(ms) {
  const total = Math.floor(ms / 1000);
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  return h > 0 ? `${h}h ${m}m ${s}s` : m > 0 ? `${m}m ${s}s` : `${s}s`;
}

/** Ordena un Map de contadores de mayor a menor y lo pinta como tabla alineada. */
function tabla(contadores, total, sangria = '  ') {
  if (contadores.size === 0) return `${sangria}(ninguna)`;

  const filas = [...contadores.entries()].sort((a, b) => b[1] - a[1]);
  const anchoClave = Math.max(...filas.map(([clave]) => clave.length));
  const anchoValor = Math.max(...filas.map(([, n]) => String(n).length));

  return filas
    .map(([clave, n]) => {
      const porcentaje = total > 0 ? ((n / total) * 100).toFixed(1) : '0.0';
      return `${sangria}${clave.padEnd(anchoClave)}  ${String(n).padStart(anchoValor)}  (${porcentaje.padStart(5)}%)`;
    })
    .join('\n');
}

/** Reparte una lista de ids en varias lineas para que no se salga de pantalla. */
function envolverIds(ids, sangria = '      ', ancho = 100) {
  const lineas = [];
  let linea = '';

  ids.forEach((id, i) => {
    const pieza = i < ids.length - 1 ? `${id},` : id;
    if (linea && (sangria + linea + ' ' + pieza).length > ancho) {
      lineas.push(sangria + linea);
      linea = pieza;
    } else {
      linea = linea ? `${linea} ${pieza}` : pieza;
    }
  });

  if (linea) lineas.push(sangria + linea);
  return lineas.join('\n');
}

/** Como `tabla`, pero listando debajo los ids de carpeta de cada grupo. */
function tablaConIds(grupos, total, sangria = '  ') {
  if (grupos.size === 0) return `${sangria}(ninguna)`;

  const filas = [...grupos.entries()].sort((a, b) => b[1].length - a[1].length);
  const anchoClave = Math.max(...filas.map(([clave]) => clave.length));
  const anchoValor = Math.max(...filas.map(([, ids]) => String(ids.length).length));

  return filas
    .map(([clave, ids]) => {
      const porcentaje = total > 0 ? ((ids.length / total) * 100).toFixed(1) : '0.0';
      const cabecera = `${sangria}${clave.padEnd(anchoClave)}  ${String(ids.length).padStart(anchoValor)}  (${porcentaje.padStart(5)}%)`;
      return `${cabecera}\n${envolverIds(ids)}`;
    })
    .join('\n');
}

/**
 * Acumulador de estadisticas de la sesion. Se va alimentando con cada peticion
 * y se imprime al cerrar el servidor.
 */
function crearEstadisticas(idSesion) {
  const inicio = Date.now();
  const porIp = new Map();
  const porContentType = new Map();
  let total = 0;
  let bytes = 0;
  let archivos = 0;

  const sumar = (mapa, clave) => mapa.set(clave, (mapa.get(clave) || 0) + 1);
  const apuntarId = (mapa, clave, id) => {
    if (!mapa.has(clave)) mapa.set(clave, []);
    mapa.get(clave).push(id);
  };

  return {
    registrar(meta) {
      total++;
      bytes += meta.cuerpo.bytesGuardados || 0;
      archivos += meta.multipart ? meta.multipart.archivos.length : 0;
      sumar(porIp, meta.red.ip || meta.red.direccionRemota || '(desconocida)');
      // Del content-type guardamos los ids para poder ir directo a las carpetas.
      apuntarId(porContentType, tipoBase(meta.cuerpo.contentType), meta.id);
    },

    imprimir() {
      const linea = '='.repeat(60);
      console.log(`\n${linea}`);
      console.log(`RESUMEN DE LA SESION ${idSesion}`);
      console.log(linea);
      console.log(`Duracion:           ${formatearDuracion(Date.now() - inicio)}`);
      console.log(`Peticiones totales: ${total}`);
      console.log(`Datos recibidos:    ${formatearBytes(bytes)}`);
      console.log(`Archivos guardados: ${archivos}`);
      console.log('\nPeticiones por IP:');
      console.log(tabla(porIp, total));
      console.log('\nPeticiones por Content-Type (con la carpeta de cada una):');
      console.log(tablaConIds(porContentType, total));
      console.log(`${linea}\n`);
    },
  };
}

module.exports = { crearEstadisticas };
