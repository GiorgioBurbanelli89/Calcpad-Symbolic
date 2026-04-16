// frameViewer.ts — Visor 3D interactivo de pórticos estilo awatif
// Three.js + OrbitControls con diagramas de M, V, N, deformada, cargas, soportes

import * as THREE from "three";
import { OrbitControls } from "three/examples/jsm/controls/OrbitControls.js";

export interface FrameViewerData {
  nodes: number[][];         // [[x,y,z], ...] coordenadas (Z-up)
  elements: number[][];      // [[n1,n2], ...] conectividad (0-indexed)
  supports?: number[];       // índices de nodos apoyados
  supportTypes?: number[][]; // [[dx,dy,dz,rx,ry,rz], ...] 1=fijo 0=libre
  loads?: number[][];        // [[nodo, Fx, Fy, Fz], ...]
  distLoads?: number[][];    // [[elem, wx, wy, wz], ...] cargas distribuidas
  deformed?: number[][];     // [[dx,dy,dz], ...] desplazamientos
  moments?: number[][];      // [[Mi, Mj], ...] momento en i,j por elemento
  shears?: number[][];       // [[Vi, Vj], ...] cortante por elemento
  normals?: number[];        // [N1, N2, ...] fuerza axial por elemento
  reactions?: number[][];    // [[Fx,Fy,Fz,Mx,My,Mz], ...] en nodos de soporte
  options?: FrameViewerOptions;
}

export interface FrameViewerOptions {
  width?: number;
  height?: number;
  defScale?: number;         // escala de deformada
  diagScale?: number;        // escala de diagramas M/V/N
  showNodes?: boolean;
  showElements?: boolean;
  showSupports?: boolean;
  showLoads?: boolean;
  showDeformed?: boolean;
  showMoments?: boolean;
  showShears?: boolean;
  showNormals?: boolean;
  showReactions?: boolean;
  showLabels?: boolean;
  title?: string;
  backgroundColor?: string;
}

// Coordenadas: X=horizontal, Y=vertical (arriba), Z=profundidad
// Igual que Three.js (Y-up) — sin swap
function toThree(x: number, y: number, z: number): THREE.Vector3 {
  return new THREE.Vector3(x, y, z);
}

// Colores
const COLORS = {
  element: 0x333333,
  elementDeformed: 0x666666,
  node: 0x333333,
  support: 0xcc0000,
  load: 0xff6600,
  momentPos: 0x1a6dcc,
  momentNeg: 0xcc1a1a,
  shearPos: 0x1a9944,
  shearNeg: 0x991a44,
  normalTension: 0xcc6622,
  normalCompression: 0x2266cc,
  reaction: 0xff6600,
  deformed: 0x0099cc,
  background: 0xffffff,
};

export function frameViewer(containerId: string, data: FrameViewerData): void {
  const container = document.getElementById(containerId);
  if (!container) return;

  const opts = data.options || {};
  const W = opts.width || 800;
  const H = opts.height || 500;
  const defScale = opts.defScale || 50;
  const diagScale = opts.diagScale || 1;

  // Scene
  const scene = new THREE.Scene();
  scene.background = new THREE.Color(opts.backgroundColor
    ? parseInt(opts.backgroundColor.replace("#", ""), 16)
    : COLORS.background);

  // Camera
  const camera = new THREE.PerspectiveCamera(45, W / H, 0.01, 10000);

  // Renderer
  const renderer = new THREE.WebGLRenderer({ antialias: true });
  renderer.setSize(W, H);
  renderer.setPixelRatio(window.devicePixelRatio || 1);
  container.appendChild(renderer.domElement);

  // Controls
  const controls = new OrbitControls(camera, renderer.domElement);
  controls.enableDamping = true;
  controls.dampingFactor = 0.1;

  // Compute bounding box for camera
  let minX = Infinity, maxX = -Infinity;
  let minY = Infinity, maxY = -Infinity;
  let minZ = Infinity, maxZ = -Infinity;
  for (const n of data.nodes) {
    minX = Math.min(minX, n[0]); maxX = Math.max(maxX, n[0]);
    minY = Math.min(minY, n[1]); maxY = Math.max(maxY, n[1]);
    minZ = Math.min(minZ, n[2] || 0); maxZ = Math.max(maxZ, n[2] || 0);
  }
  const cx = (minX + maxX) / 2, cy = (minY + maxY) / 2, cz = (minZ + maxZ) / 2;
  const size = Math.max(maxX - minX, maxY - minY, maxZ - minZ) || 1;
  const target = new THREE.Vector3(cx, cy, cz);
  controls.target.copy(target);
  // Default: front view (looking from +Z towards XY plane)
  camera.position.set(cx, cy, cz + size * 2);
  camera.up.set(0, 1, 0);

  // Grid at ground level (Y=0)
  const grid = new THREE.GridHelper(size * 2, 20, 0xcccccc, 0xe5e5e5);
  grid.position.set(cx, Math.min(minY, 0), cz);
  scene.add(grid);

  // ─── ELEMENTS (undeformed) ───
  if (opts.showElements !== false) {
    const geom = new THREE.BufferGeometry();
    const positions: number[] = [];
    for (const [i, j] of data.elements) {
      const a = data.nodes[i], b = data.nodes[j];
      const va = toThree(a[0], a[1], a[2] || 0);
      const vb = toThree(b[0], b[1], b[2] || 0);
      positions.push(va.x, va.y, va.z, vb.x, vb.y, vb.z);
    }
    geom.setAttribute("position", new THREE.Float32BufferAttribute(positions, 3));
    const mat = new THREE.LineBasicMaterial({ color: COLORS.element, linewidth: 2 });
    scene.add(new THREE.LineSegments(geom, mat));
  }

  // ─── NODES ───
  if (opts.showNodes !== false) {
    const nodeGeom = new THREE.BufferGeometry();
    const pts: number[] = [];
    for (const n of data.nodes) {
      const v = toThree(n[0], n[1], n[2] || 0);
      pts.push(v.x, v.y, v.z);
    }
    nodeGeom.setAttribute("position", new THREE.Float32BufferAttribute(pts, 3));
    const nodeMat = new THREE.PointsMaterial({ color: COLORS.node, size: 6, sizeAttenuation: false });
    scene.add(new THREE.Points(nodeGeom, nodeMat));
  }

  // ─── SUPPORTS ───
  if (opts.showSupports !== false && data.supports) {
    const supGeom = new THREE.BoxGeometry(size * 0.03, size * 0.03, size * 0.03);
    const supMat = new THREE.MeshBasicMaterial({ color: COLORS.support });
    for (const idx of data.supports) {
      const n = data.nodes[idx];
      const pos = toThree(n[0], n[1], n[2] || 0);
      const mesh = new THREE.Mesh(supGeom, supMat);
      mesh.position.copy(pos);
      scene.add(mesh);
    }
  }

  // ─── LOADS (arrows) ───
  if (opts.showLoads !== false && data.loads) {
    for (const load of data.loads) {
      const nodeIdx = load[0];
      const n = data.nodes[nodeIdx];
      const origin = toThree(n[0], n[1], n[2] || 0);
      const fx = load[1] || 0, fy = load[2] || 0, fz = load[3] || 0;
      const mag = Math.sqrt(fx * fx + fy * fy + fz * fz);
      if (mag < 1e-10) continue;
      const dir = toThree(fx / mag, fy / mag, fz / mag);
      const arrowLen = size * 0.15;
      const arrow = new THREE.ArrowHelper(dir, origin, arrowLen, COLORS.load, arrowLen * 0.3, arrowLen * 0.15);
      scene.add(arrow);
    }
  }

  // ─── DEFORMED SHAPE ───
  if (opts.showDeformed !== false && data.deformed) {
    const defGeom = new THREE.BufferGeometry();
    const defPos: number[] = [];
    for (const [i, j] of data.elements) {
      const a = data.nodes[i], b = data.nodes[j];
      const da = data.deformed[i] || [0, 0, 0], db = data.deformed[j] || [0, 0, 0];
      const va = toThree(a[0] + da[0] * defScale, a[1] + da[1] * defScale, (a[2] || 0) + (da[2] || 0) * defScale);
      const vb = toThree(b[0] + db[0] * defScale, b[1] + db[1] * defScale, (b[2] || 0) + (db[2] || 0) * defScale);
      defPos.push(va.x, va.y, va.z, vb.x, vb.y, vb.z);
    }
    defGeom.setAttribute("position", new THREE.Float32BufferAttribute(defPos, 3));
    const defMat = new THREE.LineBasicMaterial({ color: COLORS.deformed, linewidth: 2 });
    const defLines = new THREE.LineSegments(defGeom, defMat);
    scene.add(defLines);
  }

  // ─── MOMENT DIAGRAMS ───
  if (opts.showMoments !== false && data.moments) {
    addDiagramsToScene(scene, data, data.moments, diagScale, COLORS.momentPos, COLORS.momentNeg, "moment");
  }

  // ─── SHEAR DIAGRAMS ───
  if (opts.showShears !== false && data.shears) {
    addDiagramsToScene(scene, data, data.shears, diagScale, COLORS.shearPos, COLORS.shearNeg, "shear");
  }

  // ─── NORMAL FORCE DIAGRAMS ───
  if (opts.showNormals !== false && data.normals) {
    const normalPairs = data.normals.map(n => [n, n]); // constant along element
    addDiagramsToScene(scene, data, normalPairs, diagScale, COLORS.normalTension, COLORS.normalCompression, "normal");
  }

  // ─── REACTIONS (arrows at supports) ───
  if (opts.showReactions !== false && data.reactions && data.supports) {
    for (let k = 0; k < data.supports.length; k++) {
      const nodeIdx = data.supports[k];
      const n = data.nodes[nodeIdx];
      const r = data.reactions[k];
      if (!r) continue;
      const origin = toThree(n[0], n[1], n[2] || 0);
      // Force reactions
      for (let d = 0; d < 3; d++) {
        const val = r[d] || 0;
        if (Math.abs(val) < 1e-10) continue;
        const dirArr = [0, 0, 0]; dirArr[d] = val > 0 ? 1 : -1;
        const dir = toThree(dirArr[0], dirArr[1], dirArr[2]);
        const arrowLen = size * 0.12;
        const arrow = new THREE.ArrowHelper(dir, origin, arrowLen, COLORS.reaction, arrowLen * 0.25, arrowLen * 0.12);
        scene.add(arrow);
      }
    }
  }

  // ─── TITLE ───
  if (opts.title) {
    const div = document.createElement("div");
    div.style.cssText = "position:absolute;top:8px;left:12px;color:#333;font:bold 14px sans-serif;pointer-events:none;";
    div.textContent = opts.title;
    container.style.position = "relative";
    container.appendChild(div);
  }

  // ─── CONTROLS PANEL ───
  createControlsPanel(container, scene, renderer, camera);

  // ─── ANIMATION LOOP ───
  function animate() {
    requestAnimationFrame(animate);
    controls.update();
    renderer.render(scene, camera);
  }
  animate();
}

// ─── DIAGRAM RENDERING ───
// Draws filled polygons perpendicular to each element showing M, V, or N values
function addDiagramsToScene(
  scene: THREE.Scene,
  data: FrameViewerData,
  values: number[][],
  scale: number,
  colorPos: number,
  colorNeg: number,
  _type: string
) {
  const nSegs = 10; // segments per element for smooth diagrams

  for (let e = 0; e < data.elements.length; e++) {
    const [ni, nj] = data.elements[e];
    const a = data.nodes[ni], b = data.nodes[nj];
    const va = toThree(a[0], a[1], a[2] || 0);
    const vb = toThree(b[0], b[1], b[2] || 0);

    const vi = values[e]?.[0] || 0;
    const vj = values[e]?.[1] || 0;
    if (Math.abs(vi) < 1e-10 && Math.abs(vj) < 1e-10) continue;

    // Element direction and perpendicular (in the XY plane for 2D frames)
    // Convention: positive moment draws on tension side
    // For beam (+X dir): perp = +Y (upward, tension side for positive M)
    // For column (+Y dir): perp = -X (left side for positive M)
    const dir = vb.clone().sub(va).normalize();
    // Cross Z × dir gives perpendicular pointing to the "positive" side
    let perp = new THREE.Vector3().crossVectors(new THREE.Vector3(0, 0, 1), dir);
    if (perp.length() < 0.01) perp = new THREE.Vector3().crossVectors(new THREE.Vector3(0, 1, 0), dir);
    perp.normalize();

    // Build filled polygon: element line + offset curve
    const positions: number[] = [];
    const colors: number[] = [];

    for (let s = 0; s < nSegs; s++) {
      const t0 = s / nSegs, t1 = (s + 1) / nSegs;
      const val0 = vi + (vj - vi) * t0;
      const val1 = vi + (vj - vi) * t1;

      const p0 = va.clone().lerp(vb.clone(), t0);
      const p1 = va.clone().lerp(vb.clone(), t1);
      const d0 = p0.clone().add(perp.clone().multiplyScalar(val0 * scale));
      const d1 = p1.clone().add(perp.clone().multiplyScalar(val1 * scale));

      // Two triangles: p0, d0, d1 and p0, d1, p1
      positions.push(p0.x, p0.y, p0.z, d0.x, d0.y, d0.z, d1.x, d1.y, d1.z);
      positions.push(p0.x, p0.y, p0.z, d1.x, d1.y, d1.z, p1.x, p1.y, p1.z);

      // Color based on sign
      const c0 = new THREE.Color(val0 >= 0 ? colorPos : colorNeg);
      const c1 = new THREE.Color(val1 >= 0 ? colorPos : colorNeg);
      for (let i = 0; i < 3; i++) colors.push(c0.r, c0.g, c0.b);
      for (let i = 0; i < 3; i++) colors.push(c1.r, c1.g, c1.b);
    }

    const geom = new THREE.BufferGeometry();
    geom.setAttribute("position", new THREE.Float32BufferAttribute(positions, 3));
    geom.setAttribute("color", new THREE.Float32BufferAttribute(colors, 3));
    const mat = new THREE.MeshBasicMaterial({
      vertexColors: true,
      side: THREE.DoubleSide,
      transparent: true,
      opacity: 0.6,
    });
    scene.add(new THREE.Mesh(geom, mat));

    // Outline — closed polygon: element line + diagram curve
    const outlinePos: number[] = [];
    // Start at element start
    outlinePos.push(va.x, va.y, va.z);
    // Diagram curve
    for (let s = 0; s <= nSegs; s++) {
      const t = s / nSegs;
      const val = vi + (vj - vi) * t;
      const p = va.clone().lerp(vb.clone(), t);
      const d = p.clone().add(perp.clone().multiplyScalar(val * scale));
      outlinePos.push(d.x, d.y, d.z);
    }
    // Close back to element end
    outlinePos.push(vb.x, vb.y, vb.z);
    const outGeom = new THREE.BufferGeometry();
    outGeom.setAttribute("position", new THREE.Float32BufferAttribute(outlinePos, 3));
    const outMat = new THREE.LineBasicMaterial({ color: 0x666666, transparent: true, opacity: 0.8 });
    scene.add(new THREE.Line(outGeom, outMat));

    // Value labels at start and end of diagram
    if (Math.abs(vi) > 1e-6) {
      const posI = va.clone().add(perp.clone().multiplyScalar(vi * scale));
      scene.add(makeTextSprite(vi.toFixed(1), posI, vi >= 0 ? colorPos : colorNeg));
    }
    if (Math.abs(vj) > 1e-6) {
      const posJ = vb.clone().add(perp.clone().multiplyScalar(vj * scale));
      scene.add(makeTextSprite(vj.toFixed(1), posJ, vj >= 0 ? colorPos : colorNeg));
    }
  }
}

// ─── TEXT SPRITE ───
function makeTextSprite(text: string, position: THREE.Vector3, _color: number): THREE.Sprite {
  const canvas = document.createElement("canvas");
  canvas.width = 128;
  canvas.height = 48;
  const ctx = canvas.getContext("2d")!;
  // White background pill
  ctx.fillStyle = "rgba(255,255,255,0.85)";
  ctx.beginPath();
  ctx.roundRect(4, 4, 120, 40, 6);
  ctx.fill();
  ctx.strokeStyle = "#999";
  ctx.lineWidth = 1;
  ctx.stroke();
  // Black text
  ctx.font = "bold 26px sans-serif";
  ctx.fillStyle = "#222";
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  ctx.fillText(text, 64, 24);

  const tex = new THREE.CanvasTexture(canvas);
  tex.minFilter = THREE.LinearFilter;
  const mat = new THREE.SpriteMaterial({ map: tex, depthTest: false, transparent: true });
  const sprite = new THREE.Sprite(mat);
  sprite.position.copy(position);
  sprite.scale.set(1.2, 0.45, 1);
  sprite.renderOrder = 999;
  return sprite;
}

// ─── CONTROLS PANEL ───
function createControlsPanel(
  container: HTMLElement,
  scene: THREE.Scene,
  renderer: THREE.WebGLRenderer,
  camera: THREE.Camera
) {
  const panel = document.createElement("div");
  panel.style.cssText = `
    position:absolute; top:8px; right:8px; background:rgba(240,240,240,0.9);
    padding:8px; border-radius:6px; color:#333; font:11px sans-serif;
    display:flex; flex-direction:column; gap:4px; pointer-events:auto;
  `;
  container.style.position = "relative";

  const groups = scene.children;
  const btn = (label: string) => {
    const b = document.createElement("button");
    b.textContent = label;
    b.style.cssText = "background:#f0f0f0;color:#333;border:1px solid #ccc;border-radius:3px;padding:3px 8px;cursor:pointer;font:11px sans-serif;";
    b.onmouseenter = () => b.style.background = "#ddd";
    b.onmouseleave = () => b.style.background = "#f0f0f0";
    return b;
  };

  // Reset view
  const resetBtn = btn("↺ Reset View");
  resetBtn.onclick = () => {
    camera.position.set(0, 0, 10);
    (camera as THREE.PerspectiveCamera).lookAt(0, 0, 0);
  };
  panel.appendChild(resetBtn);

  // Screenshot
  const ssBtn = btn("📷 Screenshot");
  ssBtn.onclick = () => {
    renderer.render(scene, camera);
    const link = document.createElement("a");
    link.download = "calcpad_frame.png";
    link.href = renderer.domElement.toDataURL("image/png");
    link.click();
  };
  panel.appendChild(ssBtn);

  container.appendChild(panel);
}
