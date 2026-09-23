'use strict';

const fs = require('fs');
const fsp = require('fs/promises');
const path = require('path');
const { pipeline } = require('stream/promises');
const Busboy = require('busboy');
const { XMLParser, XMLValidator } = require('fast-xml-parser');

const { nanoid } = require('./nanoid');

const parserXml = new XMLParser({ ignoreAttributes: false, attributeNamePrefix: '@_' });

const LIMITE_CUERPO_EN_JSON = 1024 * 1024; // 1 MB de cuerpo parseado dentro de peticion.json
const LIMITE_VISTA_ARCHIVO = 2 * 1024 * 1024; // hasta aqui leemos un archivo subido para mostrarlo

/**
 * Convierte un nombre de archivo recibido del cliente en algo seguro de
 * escribir en disco (sin rutas, sin caracteres prohibidos en Windows).
 */
function nombreSeguro(nombre, porDefecto = 'archivo.bin') {
  if (!nombre) return porDefecto;
  const base = String(nombre).replace(/\\/g, '/').split('/').pop();
  const limpio = base
    .replace(/[\x00-\x1f<>:"|?*]/g, '_')
    .replace(/^\.+/, '_')
    .trim();
  return limpio.slice(0, 120) || porDefecto;
}

/** Heuristica para decidir si el cuerpo se puede volcar tambien como texto. */
function esTextual(contentType) {
  if (!contentType) return false;
  const tipo = contentType.split(';')[0].trim().toLowerCase();
  if (tipo.startsWith('text/')) return true;
  if (/^application\/(json|xml|javascript|x-www-form-urlencoded|graphql|yaml|x-yaml|ld\+json)$/.test(tipo)) return true;
  if (/\+(json|xml)$/.test(tipo)) return true;
  return false;
}

/** Extension sugerida para el volcado de texto del cuerpo. */
function extensionTexto(contentType) {
  const tipo = (contentType || '').split(';')[0].trim().toLowerCase();
  if (tipo.includes('json')) return 'json';
  if (tipo.includes('xml')) return 'xml';
  if (tipo.includes('html')) return 'html';
  if (tipo.includes('yaml')) return 'yaml';
  return 'txt';
}

function parsearCookies(cabecera) {
  const cookies = {};
  if (!cabecera) return cookies;
  for (const par of cabecera.split(';')) {
    const i = par.indexOf('=');
    if (i === -1) continue;
    const clave = par.slice(0, i).trim();
    const valor = par.slice(i + 1).trim();
    if (clave) {
      try {
        cookies[clave] = decodeURIComponent(valor.replace(/^"|"$/g, ''));
      } catch {
        cookies[clave] = valor;
      }
    }
  }
  return cookies;
}

/** Reconstruye la peticion tal y como llego: linea de peticion + cabeceras crudas + cuerpo. */
function construirRawHttp(req, cuerpo) {
  const lineas = [`${req.method} ${req.originalUrl} HTTP/${req.httpVersion}`];
  for (let i = 0; i < req.rawHeaders.length; i += 2) {
    lineas.push(`${req.rawHeaders[i]}: ${req.rawHeaders[i + 1]}`);
  }
  const cabecera = Buffer.from(lineas.join('\r\n') + '\r\n\r\n', 'latin1');
  return Buffer.concat([cabecera, cuerpo]);
}

/**
 * Parte un cuerpo multipart y vuelca cada archivo a disco.
 * @returns {Promise<{archivos: Array, campos: Object, error: string|null}>}
 */
async function extraerMultipart(cuerpo, headers, carpetaArchivos) {
  const archivos = [];
  const campos = {};
  let error = null;

  await new Promise((resolve) => {
    let bb;
    try {
      bb = Busboy({
        headers,
        limits: {
          fieldNameSize: 1000,
          fieldSize: Infinity,
          fields: Infinity,
          files: Infinity,
          parts: Infinity,
          fileSize: Infinity,
        },
      });
    } catch (err) {
      error = `No se pudo interpretar el multipart: ${err.message}`;
      resolve();
      return;
    }

    const escrituras = [];
    let indice = 0;

    bb.on('field', (nombre, valor, info) => {
      const anterior = campos[nombre];
      if (anterior === undefined) campos[nombre] = valor;
      else if (Array.isArray(anterior)) anterior.push(valor);
      else campos[nombre] = [anterior, valor];
      if (info && info.valueTruncated) error = `El campo "${nombre}" llego truncado`;
    });

    bb.on('file', (nombreCampo, flujo, info) => {
      indice += 1;
      const original = info.filename || '';
      const nombreDisco = `${String(indice).padStart(2, '0')}_${nombreSeguro(original)}`;
      const destino = path.join(carpetaArchivos, nombreDisco);

      const registro = {
        campo: nombreCampo,
        nombreOriginal: original || null,
        nombreGuardado: nombreDisco,
        rutaRelativa: `archivos/${nombreDisco}`,
        contentType: info.mimeType || null,
        encoding: info.encoding || null,
        bytes: 0,
      };
      archivos.push(registro);

      // Ojo: hay que enganchar la escritura de forma SINCRONA. Cualquier await
      // (o un listener de 'data') antes del pipe haria que se perdieran bytes
      // del stream y el archivo acabaria vacio o incompleto.
      let destinoStream;
      try {
        fs.mkdirSync(carpetaArchivos, { recursive: true });
        destinoStream = fs.createWriteStream(destino);
      } catch (err) {
        registro.error = String(err.message || err);
        flujo.resume();
        return;
      }

      const escritura = pipeline(flujo, destinoStream)
        .then(() => {
          registro.bytes = destinoStream.bytesWritten;
          if (info.truncated) registro.error = 'El archivo llego truncado';
        })
        .catch((err) => {
          registro.bytes = destinoStream.bytesWritten;
          registro.error = String(err.message || err);
          flujo.resume();
        });

      escrituras.push(escritura);
    });

    bb.on('error', (err) => {
      error = String(err.message || err);
      Promise.allSettled(escrituras).then(() => resolve());
    });

    bb.on('close', () => {
      Promise.allSettled(escrituras).then(() => resolve());
    });

    bb.end(cuerpo);
  });

  const contenidos = await leerArchivosTextuales(archivos, carpetaArchivos);
  return { archivos, campos, error, contenidos };
}

/**
 * Relee los archivos guardados que parecen XML o JSON para poder mostrarlos por
 * consola. Se descarta cualquier cosa binaria mirando el primer byte util, asi
 * que una foto o un PDF ni se convierten a texto.
 *
 * @returns {Promise<Object<string,string>>} nombreGuardado -> contenido
 */
async function leerArchivosTextuales(archivos, carpetaArchivos) {
  const contenidos = {};

  for (const archivo of archivos) {
    if (archivo.error || archivo.bytes === 0 || archivo.bytes > LIMITE_VISTA_ARCHIVO) continue;
    try {
      const bytes = await fsp.readFile(path.join(carpetaArchivos, archivo.nombreGuardado));
      let i = 0;
      while (i < bytes.length && bytes[i] <= 0x20) i++; // saltamos espacios/BOM-less
      const primero = bytes[i];
      // '<' (XML), '{' o '[' (JSON). Cualquier otra cosa se trata como binario.
      if (primero !== 0x3c && primero !== 0x7b && primero !== 0x5b) continue;
      contenidos[archivo.nombreGuardado] = bytes.toString('utf8');
    } catch {
      // si no se puede releer, simplemente no se muestra el contenido
    }
  }

  return contenidos;
}

/**
 * Guarda una peticion entera dentro de `dirBase/<nanoid>/`.
 * @param {import('express').Request} req
 * @param {string} dirBase carpeta "peticiones"
 */
async function guardarPeticion(req, dirBase) {
  const id = nanoid(12);
  const carpeta = path.join(dirBase, id);
  await fsp.mkdir(carpeta, { recursive: true });

  const cuerpo = req.cuerpoCrudo || Buffer.alloc(0);
  const contentType = req.headers['content-type'] || null;
  const socket = req.socket || {};

  // 1) La peticion tal cual llego: cabeceras crudas + bytes del cuerpo.
  await fsp.writeFile(path.join(carpeta, 'raw.http'), construirRawHttp(req, cuerpo));

  // 2) El cuerpo en binario, siempre que haya bytes.
  if (cuerpo.length > 0) {
    await fsp.writeFile(path.join(carpeta, 'cuerpo.bin'), cuerpo);
  }

  // 3) Volcado en texto / parseado cuando el content-type lo permite.
  let archivoTexto = null;
  let textoCuerpo = null;
  let parseado;
  let formatoParseado = null;
  let errorParseo = null;

  if (cuerpo.length > 0 && esTextual(contentType)) {
    const texto = cuerpo.toString('utf8');
    const nombreTexto = `cuerpo.${extensionTexto(contentType)}`;
    await fsp.writeFile(path.join(carpeta, nombreTexto), texto, 'utf8');
    archivoTexto = nombreTexto;
    textoCuerpo = texto;

    const tipo = contentType.split(';')[0].trim().toLowerCase();
    try {
      if (tipo.includes('json')) {
        parseado = JSON.parse(texto);
        formatoParseado = 'json';
      } else if (tipo.includes('xml')) {
        const validacion = XMLValidator.validate(texto);
        if (validacion !== true) {
          throw new Error(validacion.err ? validacion.err.msg : 'XML invalido');
        }
        parseado = parserXml.parse(texto);
        formatoParseado = 'xml';
      } else if (tipo === 'application/x-www-form-urlencoded') {
        parseado = Object.fromEntries(new URLSearchParams(texto));
        formatoParseado = 'urlencoded';
      }
    } catch (err) {
      errorParseo = String(err.message || err);
    }
  }

  // 4) Archivos y campos si venia como multipart.
  let multipart = null;
  if (contentType && /^multipart\//i.test(contentType) && cuerpo.length > 0) {
    multipart = await extraerMultipart(cuerpo, req.headers, path.join(carpeta, 'archivos'));
  }

  const parseadoCabe = parseado !== undefined && cuerpo.length <= LIMITE_CUERPO_EN_JSON;

  const meta = {
    id,
    recibidaEn: (req.recibidaEn || new Date()).toISOString(),
    metodo: req.method,
    url: req.originalUrl,
    // req.path es relativo al punto de montaje, asi que la sacamos de originalUrl.
    ruta: req.originalUrl.split('?')[0],
    query: { ...req.query },
    httpVersion: req.httpVersion,
    protocolo: req.protocol,
    host: req.headers.host || null,
    seguro: Boolean(req.secure),
    cabeceras: req.headers,
    cabecerasCrudas: req.rawHeaders,
    trailers: req.trailers && Object.keys(req.trailers).length ? req.trailers : null,
    cookies: parsearCookies(req.headers.cookie),
    red: {
      ip: req.ip || null,
      ips: req.ips && req.ips.length ? req.ips : null,
      direccionRemota: socket.remoteAddress || null,
      puertoRemoto: socket.remotePort || null,
      direccionLocal: socket.localAddress || null,
      puertoLocal: socket.localPort || null,
      familia: socket.remoteFamily || null,
    },
    cuerpo: {
      contentType,
      contentLengthCabecera: req.headers['content-length']
        ? Number(req.headers['content-length'])
        : null,
      transferEncoding: req.headers['transfer-encoding'] || null,
      bytesRecibidos: req.cuerpoBytesRecibidos || 0,
      bytesGuardados: cuerpo.length,
      truncado: Boolean(req.cuerpoTruncado),
      incompleto: Boolean(req.cuerpoIncompleto),
      error: req.cuerpoError || null,
      archivoBinario: cuerpo.length > 0 ? 'cuerpo.bin' : null,
      archivoTexto,
      formatoParseado,
      errorParseo,
      parseado: parseadoCabe ? parseado : undefined,
      parseadoOmitido:
        parseado !== undefined && !parseadoCabe
          ? 'Cuerpo demasiado grande para incluirlo aqui; mira cuerpo.bin o el volcado de texto'
          : undefined,
    },
    multipart: multipart
      ? { campos: multipart.campos, archivos: multipart.archivos, error: multipart.error }
      : null,
    nota:
      'raw.http es una reconstruccion fiel de la peticion (linea de peticion + cabeceras crudas + cuerpo). ' +
      'Si el cliente uso Transfer-Encoding: chunked, Node ya quito el troceado, asi que el cuerpo aparece ya ensamblado.',
  };

  await fsp.writeFile(
    path.join(carpeta, 'peticion.json'),
    JSON.stringify(meta, null, 2),
    'utf8'
  );

  // 5) Linea en el indice global para poder ordenar por fecha de un vistazo.
  const resumen = {
    id,
    recibidaEn: meta.recibidaEn,
    metodo: meta.metodo,
    url: meta.url,
    ip: meta.red.ip || meta.red.direccionRemota || null,
    contentType,
    bytes: cuerpo.length,
    archivos: multipart ? multipart.archivos.length : 0,
  };
  await fsp.appendFile(
    path.join(dirBase, '_indice.jsonl'),
    JSON.stringify(resumen) + '\n',
    'utf8'
  );

  // `parseado`, `texto` y el contenido de los archivos van aparte de `meta`: son
  // solo para el log por consola, no queremos duplicarlos dentro de peticion.json.
  return {
    meta,
    carpeta,
    resumen,
    parseado,
    texto: textoCuerpo,
    archivosContenido: multipart ? multipart.contenidos : {},
  };
}

module.exports = { guardarPeticion };
