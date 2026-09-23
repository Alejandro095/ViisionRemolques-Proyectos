'use strict';

/**
 * Re-indenta un XML para mostrarlo por consola. Trabaja sobre el texto original
 * (no reconstruye desde un objeto), asi que conserva atributos, comentarios,
 * CDATA y la declaracion tal y como venian: solo cambia los saltos de linea y
 * la sangria. El XML guardado en disco no se toca.
 *
 * @param {string} xml
 * @returns {string} el mismo XML con formato, o el original si no se puede tokenizar
 */
function formatearXml(xml) {
  const piezas = trocear(xml);
  if (piezas === null) return xml;

  const lineas = [];
  let nivel = 0;

  for (let i = 0; i < piezas.length; i++) {
    const pieza = piezas[i];

    if (pieza.tipo === 'texto') {
      lineas.push('  '.repeat(nivel) + pieza.valor);
      continue;
    }

    if (esCierre(pieza.valor)) {
      nivel = Math.max(0, nivel - 1);
      lineas.push('  '.repeat(nivel) + pieza.valor);
      continue;
    }

    if (esApertura(pieza.valor)) {
      // <tag>texto</tag> queda mas legible en una sola linea.
      const siguiente = piezas[i + 1];
      const posterior = piezas[i + 2];
      if (
        siguiente &&
        siguiente.tipo === 'texto' &&
        posterior &&
        posterior.tipo === 'tag' &&
        esCierre(posterior.valor)
      ) {
        lineas.push('  '.repeat(nivel) + pieza.valor + siguiente.valor + posterior.valor);
        i += 2;
        continue;
      }
      lineas.push('  '.repeat(nivel) + pieza.valor);
      nivel++;
      continue;
    }

    // Auto-cerrado, declaracion, comentario, DOCTYPE o CDATA: no cambian el nivel.
    lineas.push('  '.repeat(nivel) + pieza.valor);
  }

  return lineas.join('\n');
}

/** Parte el XML en etiquetas y nodos de texto, respetando comentarios y CDATA. */
function trocear(xml) {
  const piezas = [];
  let i = 0;

  while (i < xml.length) {
    if (xml[i] === '<') {
      let fin;
      if (xml.startsWith('<!--', i)) fin = indiceTras(xml, '-->', i);
      else if (xml.startsWith('<![CDATA[', i)) fin = indiceTras(xml, ']]>', i);
      else if (xml.startsWith('<?', i)) fin = indiceTras(xml, '?>', i);
      else fin = indiceTras(xml, '>', i);

      if (fin === -1) return null; // etiqueta sin cerrar: mejor dejarlo tal cual
      piezas.push({ tipo: 'tag', valor: xml.slice(i, fin) });
      i = fin;
    } else {
      const siguiente = xml.indexOf('<', i);
      const bruto = siguiente === -1 ? xml.slice(i) : xml.slice(i, siguiente);
      if (bruto.trim()) piezas.push({ tipo: 'texto', valor: bruto.trim() });
      i = siguiente === -1 ? xml.length : siguiente;
    }
  }

  return piezas;
}

function indiceTras(texto, marca, desde) {
  const pos = texto.indexOf(marca, desde);
  return pos === -1 ? -1 : pos + marca.length;
}

function esCierre(tag) {
  return tag.startsWith('</');
}

function esApertura(tag) {
  return (
    !tag.startsWith('</') &&
    !tag.startsWith('<?') &&
    !tag.startsWith('<!') &&
    !tag.endsWith('/>')
  );
}

module.exports = { formatearXml };
