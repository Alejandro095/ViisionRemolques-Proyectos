'use strict';

const { formatearXml } = require('./formato-xml');

const MAX_CARACTERES = Number(process.env.MAX_LOG_CUERPO) || 4000;

/** Corta un texto largo para que un cuerpo de varios MB no inunde la consola. */
function recortar(texto) {
  if (texto.length <= MAX_CARACTERES) return texto;
  const sobran = texto.length - MAX_CARACTERES;
  return `${texto.slice(0, MAX_CARACTERES)}\n... [recortado: ${sobran} caracteres mas]`;
}

/** Indenta un bloque multilinea para que quede alineado bajo su etiqueta. */
function indentar(texto, sangria = '    ') {
  return texto
    .split('\n')
    .map((linea) => sangria + linea)
    .join('\n');
}

/**
 * Detecta si un texto es XML o JSON y lo devuelve indentado. Se usa igual para
 * los campos de un formulario y para los archivos subidos: muchos dispositivos
 * (camaras, PLCs...) mandan un XML o un JSON entero por cualquiera de las dos
 * vias, y en crudo llega todo en una sola linea.
 *
 * @returns {{ formato: 'xml'|'json'|null, contenido: string }}
 */
function formatearValor(valor) {
  const texto = String(valor);
  const limpio = texto.trim();

  if (limpio.startsWith('<')) {
    return { formato: 'xml', contenido: formatearXml(limpio) };
  }

  if (limpio.startsWith('{') || limpio.startsWith('[')) {
    try {
      return { formato: 'json', contenido: JSON.stringify(JSON.parse(limpio), null, 2) };
    } catch {
      // no era JSON valido: se queda como texto plano
    }
  }

  return { formato: null, contenido: texto };
}

/** Una linea "- nombre (formato): valor", con el valor debajo si es multilinea. */
function describirCampo(nombre, valor) {
  const { formato, contenido } = formatearValor(valor);
  const recortado = recortar(contenido);
  const etiqueta = `  - ${nombre}${formato ? ` (${formato})` : ''}:`;

  return recortado.includes('\n')
    ? `${etiqueta}\n${indentar(recortado, '      ')}`
    : `${etiqueta} ${recortado}`;
}

/**
 * Una linea por archivo subido y, si su contenido es XML o JSON, el contenido
 * formateado justo debajo.
 */
function describirArchivo(archivo, contenidos) {
  const cabecera =
    `  - ${archivo.nombreOriginal || '(sin nombre)'} ` +
    `[${archivo.contentType || '?'}, ${archivo.bytes} bytes] -> ${archivo.rutaRelativa}`;

  const crudo = contenidos[archivo.nombreGuardado];
  if (crudo === undefined) return cabecera;

  const { formato, contenido } = formatearValor(crudo);
  const etiqueta = formato ? `${cabecera}  (${formato})` : cabecera;

  return `${etiqueta}\n${indentar(recortar(contenido), '      ')}`;
}

/** Decide que mostrar como cuerpo: parseado, texto plano o un resumen binario. */
function describirCuerpo(meta, parseado, texto, archivosContenido) {
  if (meta.multipart) {
    const partes = [];
    const campos = meta.multipart.campos;
    if (Object.keys(campos).length > 0) {
      const lineas = [];
      for (const [nombre, valor] of Object.entries(campos)) {
        const valores = Array.isArray(valor) ? valor : [valor];
        for (const v of valores) lineas.push(describirCampo(nombre, v));
      }
      partes.push(`campos:\n${lineas.join('\n')}`);
    }
    if (meta.multipart.archivos.length > 0) {
      const lista = meta.multipart.archivos
        .map((a) => describirArchivo(a, archivosContenido))
        .join('\n');
      partes.push(`archivos:\n${lista}`);
    }
    return partes.length > 0 ? partes.join('\n') : '(multipart vacio)';
  }

  if (meta.cuerpo.bytesGuardados === 0) return '(sin cuerpo)';

  // El XML se muestra como XML indentado, no como objeto: se re-formatea el
  // texto original solo para la consola, en disco sigue tal cual llego.
  if (meta.cuerpo.formatoParseado === 'xml' && texto) {
    return recortar(formatearXml(texto));
  }

  if (parseado !== undefined) {
    return recortar(JSON.stringify(parseado, null, 2));
  }

  if (texto !== null && texto !== undefined) {
    const aviso = meta.cuerpo.errorParseo ? ` (no se pudo parsear: ${meta.cuerpo.errorParseo})` : '';
    return recortar(texto) + aviso;
  }

  return `(binario no parseable: ${meta.cuerpo.bytesGuardados} bytes, guardado en cuerpo.bin)`;
}

/**
 * Imprime por consola lo esencial de la peticion: id de la carpeta, IP,
 * content-type y cuerpo.
 * @param {Object} opciones
 * @param {Object} opciones.meta metadatos devueltos por guardarPeticion
 * @param {string} opciones.carpeta ruta en disco donde se guardo todo
 * @param {*} opciones.parseado cuerpo ya parseado (json/xml/urlencoded) o undefined
 * @param {string|null} opciones.texto cuerpo como texto plano, si era textual
 * @param {Object<string,string>} [opciones.archivosContenido] contenido de los
 *        archivos subidos que son XML o JSON, por nombre guardado
 */
function imprimirPeticion({ meta, carpeta, parseado, texto, archivosContenido = {} }) {
  const formato = meta.cuerpo.formatoParseado ? ` (${meta.cuerpo.formatoParseado})` : '';

  console.log(`\n[${meta.recibidaEn}] ${meta.metodo} ${meta.url}`);
  console.log(`  ID (carpeta): ${meta.id}`);
  console.log(`  Guardado en:  ${carpeta}`);
  console.log(`  IP:           ${meta.red.ip || meta.red.direccionRemota || 'desconocida'}`);
  console.log(`  Content-Type: ${meta.cuerpo.contentType || '(ninguno)'}`);
  console.log(`  Body${formato}:`);
  console.log(indentar(describirCuerpo(meta, parseado, texto, archivosContenido)));
}

module.exports = { imprimirPeticion };
