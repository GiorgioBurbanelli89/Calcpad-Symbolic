// fem2d.ts — Malla FEM 2D con colormap suave
// Three.js ortográfico renderizado a imagen estática (no animation loop)
// Un solo render → toDataURL → <img> = no consume GPU continuamente

import * as THREE from "three";
import { getColormap, rgbToHex } from "./utils/colormap";

export interface Fem2DData {
  nodes: number[][];
  elements: number[][];
  supports?: number[];
  loads?: number[][];
  values?: number[];
  deformed?: number[][];
  options?: Fem2DOptions;
}

export interface Fem2DOptions {
  width?: number;
  height?: number;
  scale?: number;
  palette?: string;
  showNodes?: boolean;
  showLabels?: boolean;
  showElements?: boolean;
  title?: string;
}

// Renderer compartido (evita crear múltiples contextos WebGL)
let _sharedRenderer: THREE.WebGLRenderer | null = null;
function getRenderer(w: number, h: number): THREE.WebGLRenderer {
  if (!_sharedRenderer) {
    _sharedRenderer = new THREE.WebGLRenderer({ antialias: true, preserveDrawingBuffer: true });
  }
  _sharedRenderer.setSize(w, h);
  _sharedRenderer.setPixelRatio(1); // sin DPI scaling para imagen
  return _sharedRenderer;
}

export function fem2d(containerId: string, data: Fem2DData): void {
  const container = document.getElementById(containerId);
  if (!container) return;

  const opts = data.options || {};
  const W = opts.width || 600;
  const H = opts.height || 400;
  const pad = 40;
  const showNodes = opts.showNodes !== false;
  const defScale = opts.scale || 1;
  const cmap = getColormap(opts.palette || "jet");

  // Bounding box
  let xmin = Infinity, xmax = -Infinity;
  let ymin = Infinity, ymax = -Infinity;
  for (const [x, y] of data.nodes) {
    if (x < xmin) xmin = x; if (x > xmax) xmax = x;
    if (y < ymin) ymin = y; if (y > ymax) ymax = y;
  }
  const dx = xmax - xmin || 1;
  const dy = ymax - ymin || 1;

  // Min/max valores
  let vmin = 0, vmax = 1;
  if (data.values && data.values.length > 0) {
    vmin = Math.min(...data.values);
    vmax = Math.max(...data.values);
    if (Math.abs(vmax - vmin) < 1e-15) vmax = vmin + 1;
  }

  // Transformaciones para SVG overlay
  const sc = Math.min((W - 2 * pad) / dx, (H - 2 * pad) / dy);
  const offX = pad + ((W - 2 * pad) - dx * sc) / 2;
  const offY = pad + ((H - 2 * pad) - dy * sc) / 2;
  const tx = (x: number) => offX + (x - xmin) * sc;
  const ty = (y: number) => H - offY - (y - ymin) * sc;

  const nodePos: [number, number][] = data.nodes.map(([nx, ny], i) => {
    const ddx = data.deformed ? data.deformed[i][0] * defScale : 0;
    const ddy = data.deformed ? data.deformed[i][1] * defScale : 0;
    return [tx(nx + ddx), ty(ny + ddy)];
  });

  // Wrapper
  const wrapper = document.createElement("div");
  wrapper.style.cssText = `position:relative; width:${W}px; height:${H}px; overflow:hidden;`;
  container.appendChild(wrapper);

  // === Three.js render to image (colores por vértice, gradiente suave) ===
  if (data.values) {
    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x1a1a2e);

    // Cámara ortográfica
    const aspect = W / H;
    const margin = 1.15;
    let camW: number, camH: number;
    if (dx / dy > aspect) { camW = dx * margin; camH = camW / aspect; }
    else { camH = dy * margin; camW = camH * aspect; }
    const cx = (xmin + xmax) / 2, cy = (ymin + ymax) / 2;
    const camera = new THREE.OrthographicCamera(
      cx - camW / 2, cx + camW / 2, cy + camH / 2, cy - camH / 2, -1, 1
    );
    camera.position.z = 1;

    // Geometría con colores por vértice
    const geometry = new THREE.BufferGeometry();
    const positions: number[] = [];
    const colors: number[] = [];

    for (const elem of data.elements) {
      const verts: [number, number][] = [];
      const vc: [number, number, number][] = [];
      for (const ni of elem) {
        const [nx, ny] = data.nodes[ni];
        const ddx = data.deformed ? data.deformed[ni][0] * defScale : 0;
        const ddy = data.deformed ? data.deformed[ni][1] * defScale : 0;
        verts.push([nx + ddx, ny + ddy]);
        const t = (data.values[ni] - vmin) / (vmax - vmin);
        const rgb = cmap(Math.max(0, Math.min(1, t)));
        vc.push([rgb[0] / 255, rgb[1] / 255, rgb[2] / 255]);
      }
      const tris = elem.length === 4 ? [[0, 1, 2], [0, 2, 3]] : [[0, 1, 2]];
      for (const [a, b, c] of tris) {
        positions.push(verts[a][0], verts[a][1], 0, verts[b][0], verts[b][1], 0, verts[c][0], verts[c][1], 0);
        colors.push(...vc[a], ...vc[b], ...vc[c]);
      }
    }

    geometry.setAttribute("position", new THREE.Float32BufferAttribute(positions, 3));
    geometry.setAttribute("color", new THREE.Float32BufferAttribute(colors, 3));
    scene.add(new THREE.Mesh(geometry, new THREE.MeshBasicMaterial({ vertexColors: true, side: THREE.DoubleSide })));

    // Wireframe
    const wirePos: number[] = [];
    for (const elem of data.elements) {
      for (let i = 0; i < elem.length; i++) {
        const ni = elem[i], nj = elem[(i + 1) % elem.length];
        const [x1, y1] = data.nodes[ni]; const [x2, y2] = data.nodes[nj];
        const d1x = data.deformed ? data.deformed[ni][0] * defScale : 0;
        const d1y = data.deformed ? data.deformed[ni][1] * defScale : 0;
        const d2x = data.deformed ? data.deformed[nj][0] * defScale : 0;
        const d2y = data.deformed ? data.deformed[nj][1] * defScale : 0;
        wirePos.push(x1 + d1x, y1 + d1y, 0.01, x2 + d2x, y2 + d2y, 0.01);
      }
    }
    const wireGeo = new THREE.BufferGeometry();
    wireGeo.setAttribute("position", new THREE.Float32BufferAttribute(wirePos, 3));
    scene.add(new THREE.LineSegments(wireGeo, new THREE.LineBasicMaterial({ color: 0x000000, opacity: 0.15, transparent: true })));

    // Render UNA vez → convertir a imagen
    const renderer = getRenderer(W, H);
    renderer.render(scene, camera);
    const imgUrl = renderer.domElement.toDataURL("image/png");

    // Insertar como <img> (no consume GPU después)
    const img = document.createElement("img");
    img.src = imgUrl;
    img.style.cssText = "position:absolute; top:0; left:0; width:100%; height:100%;";
    wrapper.appendChild(img);

    // Limpiar Three.js
    geometry.dispose(); wireGeo.dispose();
  }

  // === SVG overlay ===
  const svgParts: string[] = [];
  svgParts.push(`<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}" style="position:absolute; top:0; left:0; pointer-events:none;">`);
  svgParts.push(`<text id="${containerId}_tip" x="10" y="20" fill="#ccc" font-size="11" font-family="monospace" style="pointer-events:auto;"></text>`);

  if (!data.values) {
    for (const elem of data.elements) {
      const points = elem.map(ni => `${nodePos[ni][0].toFixed(1)},${nodePos[ni][1].toFixed(1)}`);
      svgParts.push(`<polygon points="${points.join(" ")}" fill="rgba(0,200,100,0.1)" stroke="rgba(0,200,100,0.6)" stroke-width="1" style="pointer-events:auto;"/>`);
    }
  }

  if (data.supports) {
    for (const ni of data.supports) {
      const [px, py] = nodePos[ni];
      const sz = 7;
      svgParts.push(`<polygon points="${px},${py} ${px - sz},${py + sz * 1.5} ${px + sz},${py + sz * 1.5}" fill="rgba(255,100,100,0.6)" stroke="#f66" stroke-width="1.5"/>`);
      svgParts.push(`<line x1="${px - sz * 1.2}" y1="${py + sz * 1.5}" x2="${px + sz * 1.2}" y2="${py + sz * 1.5}" stroke="#f66" stroke-width="1.5"/>`);
    }
  }

  if (data.loads) {
    let fmax = 0;
    for (const [, fx, fy] of data.loads) fmax = Math.max(fmax, Math.sqrt(fx * fx + fy * fy));
    if (fmax === 0) fmax = 1;
    svgParts.push(`<defs><marker id="ah_${containerId}" markerWidth="8" markerHeight="6" refX="8" refY="3" orient="auto"><polygon points="0 0, 8 3, 0 6" fill="#4af"/></marker></defs>`);
    for (const [ni, fx, fy] of data.loads) {
      const [px, py] = nodePos[ni];
      const mag = Math.sqrt(fx * fx + fy * fy);
      const len = (mag / fmax) * 40;
      const arrowDx = (fx / mag) * len, arrowDy = -(fy / mag) * len;
      svgParts.push(`<line x1="${(px - arrowDx).toFixed(1)}" y1="${(py - arrowDy).toFixed(1)}" x2="${px.toFixed(1)}" y2="${py.toFixed(1)}" stroke="#4af" stroke-width="2" marker-end="url(#ah_${containerId})" style="pointer-events:auto;"/>`);
    }
  }

  if (showNodes) {
    for (let i = 0; i < data.nodes.length; i++) {
      const [px, py] = nodePos[i];
      const val = data.values ? `, v=${data.values[i].toFixed(3)}` : "";
      const hIn = `document.getElementById('${containerId}_tip').textContent='N${i + 1}: (${data.nodes[i][0].toFixed(2)}, ${data.nodes[i][1].toFixed(2)})${val}'`;
      const hOut = `document.getElementById('${containerId}_tip').textContent=''`;
      svgParts.push(`<circle cx="${px.toFixed(1)}" cy="${py.toFixed(1)}" r="2" fill="#fff" stroke="#000" stroke-width="0.3" onmouseover="${hIn}" onmouseout="${hOut}" style="pointer-events:auto; cursor:pointer;"/>`);
    }
  }

  if (data.values) {
    const lx = W - 30, ly = pad, lh = H - 2 * pad, lw = 15;
    for (let i = 0; i < lh; i++) {
      const t = 1 - i / lh;
      const rgb = cmap(t);
      svgParts.push(`<rect x="${lx}" y="${ly + i}" width="${lw}" height="1.5" fill="rgb(${rgb[0]},${rgb[1]},${rgb[2]})"/>`);
    }
    svgParts.push(`<rect x="${lx}" y="${ly}" width="${lw}" height="${lh}" fill="none" stroke="#666" stroke-width="0.5"/>`);
    svgParts.push(`<text x="${lx - 3}" y="${ly + 4}" fill="#ccc" font-size="9" text-anchor="end">${vmax.toFixed(2)}</text>`);
    svgParts.push(`<text x="${lx - 3}" y="${ly + lh}" fill="#ccc" font-size="9" text-anchor="end">${vmin.toFixed(2)}</text>`);
    svgParts.push(`<text x="${lx - 3}" y="${ly + lh / 2 + 3}" fill="#999" font-size="8" text-anchor="end">${((vmin + vmax) / 2).toFixed(2)}</text>`);
  }

  if (opts.title) {
    svgParts.push(`<text x="${W / 2}" y="18" fill="#ddd" font-size="13" text-anchor="middle" font-family="sans-serif">${opts.title}</text>`);
  }

  svgParts.push("</svg>");
  const svgDiv = document.createElement("div");
  svgDiv.style.cssText = "position:absolute; top:0; left:0;";
  svgDiv.innerHTML = svgParts.join("\n");
  wrapper.appendChild(svgDiv);
}
