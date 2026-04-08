// fem2d.ts — 2D FEM mesh visualization with smooth vertex color interpolation
// Interactive with OrbitControls, white background, color legend

import * as THREE from "three";
import { OrbitControls } from "three/examples/jsm/controls/OrbitControls.js";
import { getColormap } from "./utils/colormap";

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

export function fem2d(containerId: string, data: Fem2DData): void {
  const container = document.getElementById(containerId);
  if (!container) return;

  const opts = data.options || {};
  const W = opts.width || 700;
  const H = opts.height || 500;
  const defScale = opts.scale || 1;
  const cmap = getColormap("jet");

  // Bounding box
  let xmin = Infinity, xmax = -Infinity;
  let ymin = Infinity, ymax = -Infinity;
  for (const [x, y] of data.nodes) {
    if (x < xmin) xmin = x; if (x > xmax) xmax = x;
    if (y < ymin) ymin = y; if (y > ymax) ymax = y;
  }
  const dx = xmax - xmin || 1;
  const dy = ymax - ymin || 1;
  const cx = (xmin + xmax) / 2, cy = (ymin + ymax) / 2;

  // Value range
  let vmin = 0, vmax = 1;
  if (data.values && data.values.length > 0) {
    vmin = Math.min(...data.values);
    vmax = Math.max(...data.values);
    if (Math.abs(vmax - vmin) < 1e-15) vmax = vmin + 1;
  }

  // Scene
  const scene = new THREE.Scene();
  scene.background = new THREE.Color(0xffffff);

  // Camera — orthographic for 2D
  const aspect = W / H;
  const margin = 1.2;
  let camW: number, camH: number;
  if (dx / dy > aspect) { camW = dx * margin; camH = camW / aspect; }
  else { camH = dy * margin; camW = camH * aspect; }
  const camera = new THREE.OrthographicCamera(
    cx - camW / 2, cx + camW / 2, cy + camH / 2, cy - camH / 2, -10, 10
  );
  camera.position.z = 5;

  // Renderer
  const renderer = new THREE.WebGLRenderer({ antialias: true });
  renderer.setSize(W, H);
  renderer.setPixelRatio(window.devicePixelRatio || 1);
  container.style.position = "relative";
  container.appendChild(renderer.domElement);

  // Controls
  const controls = new OrbitControls(camera, renderer.domElement);
  controls.enableRotate = false; // 2D — only pan and zoom
  controls.enableDamping = true;
  controls.dampingFactor = 0.1;

  // ─── FILLED MESH WITH VERTEX COLORS ───
  if (data.values) {
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
  }

  // ─── WIREFRAME ───
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
  scene.add(new THREE.LineSegments(wireGeo, new THREE.LineBasicMaterial({ color: 0x333333, opacity: 0.2, transparent: true })));

  // ─── SUPPORTS ───
  if (data.supports) {
    const sz = Math.max(dx, dy) * 0.015;
    for (const ni of data.supports) {
      const [nx, ny] = data.nodes[ni];
      // Triangle symbol
      const shape = new THREE.BufferGeometry();
      shape.setAttribute("position", new THREE.Float32BufferAttribute([
        nx, ny, 0.02, nx - sz, ny - sz * 1.5, 0.02, nx + sz, ny - sz * 1.5, 0.02
      ], 3));
      scene.add(new THREE.Mesh(shape, new THREE.MeshBasicMaterial({ color: 0x333333, side: THREE.DoubleSide })));
      // Ground line
      const lineGeo = new THREE.BufferGeometry();
      lineGeo.setAttribute("position", new THREE.Float32BufferAttribute([
        nx - sz * 1.3, ny - sz * 1.5, 0.02, nx + sz * 1.3, ny - sz * 1.5, 0.02
      ], 3));
      scene.add(new THREE.Line(lineGeo, new THREE.LineBasicMaterial({ color: 0x333333 })));
    }
  }

  // ─── NODES ───
  if (opts.showNodes === true && data.nodes.length < 500) {
    const nodeGeom = new THREE.BufferGeometry();
    const pts: number[] = [];
    for (const [nx, ny] of data.nodes) pts.push(nx, ny, 0.03);
    nodeGeom.setAttribute("position", new THREE.Float32BufferAttribute(pts, 3));
    scene.add(new THREE.Points(nodeGeom, new THREE.PointsMaterial({ color: 0x333333, size: 2, sizeAttenuation: false })));
  }

  // ─── COLOR LEGEND (HTML overlay) ───
  if (data.values) {
    const lH = H - 80;
    const legend = document.createElement("div");
    legend.style.cssText = `position:absolute; right:10px; top:35px; width:24px; height:${lH}px; border:1px solid #666; overflow:hidden;`;
    const canvas = document.createElement("canvas");
    canvas.width = 24; canvas.height = lH;
    const ctx = canvas.getContext("2d")!;
    for (let i = 0; i < lH; i++) {
      const t = 1 - i / lH;
      const rgb = cmap(t);
      ctx.fillStyle = `rgb(${rgb[0]},${rgb[1]},${rgb[2]})`;
      ctx.fillRect(0, i, 24, 1);
    }
    legend.appendChild(canvas);
    container.appendChild(legend);

    // Tick labels (5 levels)
    const nTicks = 5;
    for (let k = 0; k <= nTicks; k++) {
      const t = k / nTicks;
      const val = vmax - t * (vmax - vmin);
      const yPos = 35 + t * lH - 5;
      const lbl = document.createElement("div");
      lbl.style.cssText = `position:absolute; right:40px; top:${yPos}px; font:10px monospace; color:#333; white-space:nowrap;`;
      lbl.textContent = val.toPrecision(4);
      container.appendChild(lbl);
    }
  }

  // ─── TITLE ───
  if (opts.title) {
    const div = document.createElement("div");
    div.style.cssText = "position:absolute; top:8px; left:12px; color:#333; font:bold 14px sans-serif; pointer-events:none;";
    div.textContent = opts.title;
    container.appendChild(div);
  }

  // ─── ANIMATION LOOP ───
  function animate() {
    requestAnimationFrame(animate);
    controls.update();
    renderer.render(scene, camera);
  }
  animate();
}
