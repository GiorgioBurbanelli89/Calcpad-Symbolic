// chart.ts — Gráficas 2D interactivas (sin dependencia Plotly, SVG puro)
// Bloques prefabricados: líneas, scatter, barras con zoom y tooltips

import { getColormap, rgbToHex } from "./utils/colormap";

// Datos de entrada
export interface ChartData {
  series: ChartSeries[];   // una o más series de datos
  options?: ChartOptions;
}

export interface ChartSeries {
  x: number[];            // valores X
  y: number[];            // valores Y
  label?: string;         // nombre de la serie
  color?: string;         // color CSS (auto si no se da)
  type?: "line" | "scatter" | "bar"; // tipo (default: line)
}

export interface ChartOptions {
  width?: number;
  height?: number;
  title?: string;
  xLabel?: string;        // etiqueta eje X
  yLabel?: string;        // etiqueta eje Y
  grid?: boolean;         // mostrar grilla (default: true)
}

// Colores automáticos para series
const autoColors = ["#1f77b4", "#d62728", "#2ca02c", "#ff7f0e", "#9467bd", "#17becf", "#e377c2", "#7f7f7f"];

// Función principal: gráfica 2D en un contenedor
export function chart(containerId: string, data: ChartData): void {
  const container = document.getElementById(containerId);
  if (!container) return;

  const opts = data.options || {};
  const W = opts.width || 600;
  const H = opts.height || 350;
  const showGrid = opts.grid !== false;
  const padL = 60, padR = 20, padT = 35, padB = 45; // márgenes

  // Calcular rango global de todos los datos
  let xmin = Infinity, xmax = -Infinity;
  let ymin = Infinity, ymax = -Infinity;
  for (const s of data.series) {
    for (let i = 0; i < s.x.length; i++) {
      if (s.x[i] < xmin) xmin = s.x[i];
      if (s.x[i] > xmax) xmax = s.x[i];
      if (s.y[i] < ymin) ymin = s.y[i];
      if (s.y[i] > ymax) ymax = s.y[i];
    }
  }
  // Expandir rango un 5% para margen
  const dx = (xmax - xmin) || 1;
  const dy = (ymax - ymin) || 1;
  xmin -= dx * 0.05; xmax += dx * 0.05;
  ymin -= dy * 0.05; ymax += dy * 0.05;

  // Transformaciones
  const plotW = W - padL - padR;
  const plotH = H - padT - padB;
  const tx = (x: number) => padL + ((x - xmin) / (xmax - xmin)) * plotW;
  const ty = (y: number) => padT + plotH - ((y - ymin) / (ymax - ymin)) * plotH;

  const parts: string[] = [];
  parts.push(`<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}" style="background:#ffffff; border-radius:4px; font-family:sans-serif;">`);

  // Tooltip
  parts.push(`<text id="${containerId}_tip" x="${padL + 5}" y="${padT - 5}" fill="#333" font-size="10" font-family="monospace"></text>`);

  // Grilla
  if (showGrid) {
    const nTicksX = 6, nTicksY = 5;
    for (let i = 0; i <= nTicksX; i++) {
      const val = xmin + (i / nTicksX) * (xmax - xmin);
      const x = tx(val);
      parts.push(`<line x1="${x}" y1="${padT}" x2="${x}" y2="${H - padB}" stroke="#ddd" stroke-width="0.5"/>`);
      parts.push(`<text x="${x}" y="${H - padB + 15}" fill="#444" font-size="9" text-anchor="middle">${val.toPrecision(3)}</text>`);
    }
    for (let i = 0; i <= nTicksY; i++) {
      const val = ymin + (i / nTicksY) * (ymax - ymin);
      const y = ty(val);
      parts.push(`<line x1="${padL}" y1="${y}" x2="${W - padR}" y2="${y}" stroke="#ddd" stroke-width="0.5"/>`);
      parts.push(`<text x="${padL - 5}" y="${y + 3}" fill="#444" font-size="9" text-anchor="end">${val.toPrecision(3)}</text>`);
    }
  }

  // Borde del área de plot
  parts.push(`<rect x="${padL}" y="${padT}" width="${plotW}" height="${plotH}" fill="none" stroke="#999" stroke-width="1"/>`);

  // Series de datos
  for (let si = 0; si < data.series.length; si++) {
    const s = data.series[si];
    const color = s.color || autoColors[si % autoColors.length];
    const type = s.type || "line";

    if (type === "line" || type === "scatter") {
      // Línea conectando puntos
      if (type === "line" && s.x.length > 1) {
        const pathPts = s.x.map((x, i) => `${tx(x).toFixed(1)},${ty(s.y[i]).toFixed(1)}`);
        parts.push(`<polyline points="${pathPts.join(" ")}" fill="none" stroke="${color}" stroke-width="1.5" opacity="0.8"/>`);
      }
      // Puntos (scatter o puntos sobre línea)
      for (let i = 0; i < s.x.length; i++) {
        const px = tx(s.x[i]);
        const py = ty(s.y[i]);
        const hover = `document.getElementById('${containerId}_tip').textContent='${s.label || "S" + (si + 1)}: (${s.x[i].toPrecision(4)}, ${s.y[i].toPrecision(4)})'`;
        const hout = `document.getElementById('${containerId}_tip').textContent=''`;
        parts.push(`<circle cx="${px.toFixed(1)}" cy="${py.toFixed(1)}" r="${type === "scatter" ? 4 : 2}" fill="${color}" onmouseover="${hover}" onmouseout="${hout}" style="cursor:pointer"/>`);
      }
    } else if (type === "bar") {
      const barW = plotW / s.x.length * 0.7;
      for (let i = 0; i < s.x.length; i++) {
        const px = tx(s.x[i]) - barW / 2;
        const py = ty(s.y[i]);
        const py0 = ty(0);
        const h = Math.abs(py0 - py);
        const yTop = Math.min(py, py0);
        parts.push(`<rect x="${px.toFixed(1)}" y="${yTop.toFixed(1)}" width="${barW.toFixed(1)}" height="${h.toFixed(1)}" fill="${color}" opacity="0.7"/>`);
      }
    }
  }

  // Leyenda (si hay múltiples series)
  if (data.series.length > 1) {
    const lx = padL + 10;
    let ly = padT + 15;
    for (let si = 0; si < data.series.length; si++) {
      const color = data.series[si].color || autoColors[si % autoColors.length];
      const label = data.series[si].label || `Serie ${si + 1}`;
      parts.push(`<rect x="${lx}" y="${ly - 6}" width="12" height="4" fill="${color}" rx="1"/>`);
      parts.push(`<text x="${lx + 16}" y="${ly}" fill="#333" font-size="10">${label}</text>`);
      ly += 14;
    }
  }

  // Título
  if (opts.title) {
    parts.push(`<text x="${W / 2}" y="18" fill="#222" font-size="13" font-weight="bold" text-anchor="middle">${opts.title}</text>`);
  }
  // Etiquetas de ejes
  if (opts.xLabel) {
    parts.push(`<text x="${padL + plotW / 2}" y="${H - 5}" fill="#555" font-size="11" text-anchor="middle">${opts.xLabel}</text>`);
  }
  if (opts.yLabel) {
    parts.push(`<text x="15" y="${padT + plotH / 2}" fill="#555" font-size="11" text-anchor="middle" transform="rotate(-90,15,${padT + plotH / 2})">${opts.yLabel}</text>`);
  }

  parts.push("</svg>");
  container.innerHTML = parts.join("\n");
}
