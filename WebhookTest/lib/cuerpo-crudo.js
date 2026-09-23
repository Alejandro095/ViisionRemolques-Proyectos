'use strict';

/**
 * Middleware que consume el stream de la peticion y deja los bytes crudos en
 * `req.cuerpoCrudo`. No usa ningun body-parser: no interpreta el content-type,
 * asi que funciona igual con JSON, XML, binario, multipart o sin cuerpo.
 *
 * @param {{ limite?: number }} opciones limite de bytes a guardar en memoria
 */
function capturarCuerpoCrudo({ limite = 1024 * 1024 * 1024 } = {}) {
  return function middleware(req, res, next) {
    req.recibidaEn = new Date();

    const trozos = [];
    let bytesRecibidos = 0;
    let truncado = false;
    let finalizado = false;

    const terminar = (err) => {
      if (finalizado) return;
      finalizado = true;
      req.cuerpoCrudo = Buffer.concat(trozos);
      req.cuerpoBytesRecibidos = bytesRecibidos;
      req.cuerpoTruncado = truncado;
      req.cuerpoIncompleto = Boolean(err);
      req.cuerpoError = err ? String(err.message || err) : null;
      next();
    };

    req.on('data', (trozo) => {
      bytesRecibidos += trozo.length;
      if (bytesRecibidos > limite) {
        // Seguimos drenando el stream para no romper la conexion, pero
        // dejamos de acumular en memoria.
        truncado = true;
        return;
      }
      trozos.push(trozo);
    });

    req.on('end', () => terminar(null));
    req.on('aborted', () => terminar(new Error('El cliente abortó la petición')));
    req.on('error', (err) => terminar(err));
  };
}

module.exports = { capturarCuerpoCrudo };
