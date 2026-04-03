// fem3d.ts — Malla FEM 3D interactiva con Three.js
// Convención: datos de entrada usan Z-arriba (ingeniería)
// Three.js usa Y-arriba → se hace swap Y↔Z internamente

import * as THREE from "three";
import { OrbitControls } from "three/examples/jsm/controls/OrbitControls.js";
import { getColormap } from "./utils/colormap";

// Datos de entrada (serializados desde CalcpadCE C#)
export interface Fem3DData {
  nodes: number[][];      // [[x,y,z], ...] coordenadas 3D (Z-arriba)
  elements: number[][];   // [[n1,n2,n3], ...] o [[n1,n2,n3,n4], ...]
  values?: number[];      // valores por nodo para colorear
  deformed?: number[][];  // [[dx,dy,dz], ...] desplazamientos (Z-arriba)
  options?: Fem3DOptions;
}

export interface Fem3DOptions {
  width?: number;
  height?: number;
  scale?: number;         // escala de deformada (default: 1)
  palette?: string;       // colormap: jet, rainbow, viridis, coolwarm
  title?: string;
  wireframe?: boolean;    // mostrar bordes (default: true)
}

// Swap Y↔Z: convierte (x,y,z) ingeniería → (x,z,y) Three.js
function toThree(x: number, y: number, z: number): [number, number, number] {
  return [x, z, y]; // Y-arriba en Three.js = Z-arriba en ingeniería
}

// Función principal: renderiza malla FEM 3D en un contenedor
export function fem3d(containerId: string, data: Fem3DData): void {
  const container = document.getElementById(containerId);
  if (!container) return;

  const opts = data.options || {};
  const W = opts.width || 600;
  const H = opts.height || 400;
  const defScale = opts.scale || 1;
  const showWire = opts.wireframe !== false;
  const cmap = getColormap(opts.palette || "jet");

  // Escena Three.js
  const scene = new THREE.Scene();
  scene.background = new THREE.Color(0x1a1a2e);

  // Cámara perspectiva
  const camera = new THREE.PerspectiveCamera(50, W / H, 0.1, 1000);

  // Renderer WebGL
  const renderer = new THREE.WebGLRenderer({ antialias: true });
  renderer.setSize(W, H);
  renderer.setPixelRatio(window.devicePixelRatio);
  container.appendChild(renderer.domElement);

  // Controles de órbita
  const controls = new OrbitControls(camera, renderer.domElement);
  controls.enableDamping = true;
  controls.dampingFactor = 0.1;

  // Bounding box en coordenadas de ingeniería
  let xmin = Infinity, xmax = -Infinity;
  let ymin = Infinity, ymax = -Infinity;
  let zmin = Infinity, zmax = -Infinity;
  for (const [x, y, z] of data.nodes) {
    if (x < xmin) xmin = x; if (x > xmax) xmax = x;
    if (y < ymin) ymin = y; if (y > ymax) ymax = y;
    const zz = z || 0;
    if (zz < zmin) zmin = zz; if (zz > zmax) zmax = zz;
  }

  // Centro y tamaño en coordenadas Three.js (swapped)
  const [tcx, tcy, tcz] = toThree(
    (xmin + xmax) / 2, (ymin + ymax) / 2, (zmin + zmax) / 2
  );
  const size = Math.max(xmax - xmin, ymax - ymin, zmax - zmin) || 1;

  // Cámara: posición isométrica mirando al centro
  camera.position.set(tcx + size * 1.2, tcy + size * 0.8, tcz + size * 1.2);
  controls.target.set(tcx, tcy, tcz);

  // Luces
  scene.add(new THREE.AmbientLight(0x606060, 2));
  const dirLight = new THREE.DirectionalLight(0xffffff, 1.2);
  dirLight.position.set(tcx + size * 2, tcy + size * 2, tcz + size);
  scene.add(dirLight);

  // Min/max de valores para colormap
  let vmin = 0, vmax = 1;
  if (data.values && data.values.length > 0) {
    vmin = Math.min(...data.values);
    vmax = Math.max(...data.values);
    if (vmin === vmax) vmax = vmin + 1;
  }

  // Crear geometría de la malla FEM
  const geometry = new THREE.BufferGeometry();
  const positions: number[] = [];
  const colors: number[] = [];
  const wirePositions: number[] = [];

  for (const elem of data.elements) {
    const verts: THREE.Vector3[] = [];
    const vertColors: [number, number, number][] = [];

    for (const ni of elem) {
      const [nx, ny, nz] = data.nodes[ni];
      // Desplazamientos en coord ingeniería, luego swap
      const ddx = data.deformed ? data.deformed[ni][0] * defScale : 0;
      const ddy = data.deformed ? data.deformed[ni][1] * defScale : 0;
      const ddz = data.deformed ? (data.deformed[ni][2] || 0) * defScale : 0;
      // Swap Y↔Z para Three.js
      const [tx, ty, tz] = toThree(nx + ddx, ny + ddy, (nz || 0) + ddz);
      verts.push(new THREE.Vector3(tx, ty, tz));

      // Color por valor
      if (data.values) {
        const t = (data.values[ni] - vmin) / (vmax - vmin);
        const rgb = cmap(t);
        vertColors.push([rgb[0] / 255, rgb[1] / 255, rgb[2] / 255]);
      } else {
        vertColors.push([0, 0.8, 0.4]);
      }
    }

    // Triangular: quad → 2 triángulos, tri → 1
    const tris = elem.length === 4
      ? [[0, 1, 2], [0, 2, 3]]
      : [[0, 1, 2]];

    for (const [a, b, c] of tris) {
      positions.push(verts[a].x, verts[a].y, verts[a].z);
      positions.push(verts[b].x, verts[b].y, verts[b].z);
      positions.push(verts[c].x, verts[c].y, verts[c].z);
      colors.push(...vertColors[a], ...vertColors[b], ...vertColors[c]);
    }

    // Wireframe
    if (showWire) {
      for (let i = 0; i < verts.length; i++) {
        const v1 = verts[i];
        const v2 = verts[(i + 1) % verts.length];
        wirePositions.push(v1.x, v1.y, v1.z, v2.x, v2.y, v2.z);
      }
    }
  }

  // Malla sólida
  geometry.setAttribute("position", new THREE.Float32BufferAttribute(positions, 3));
  geometry.setAttribute("color", new THREE.Float32BufferAttribute(colors, 3));
  geometry.computeVertexNormals();
  const material = new THREE.MeshLambertMaterial({
    vertexColors: true,
    side: THREE.DoubleSide,
    transparent: true,
    opacity: 0.85,
  });
  scene.add(new THREE.Mesh(geometry, material));

  // Wireframe (bordes)
  if (showWire && wirePositions.length > 0) {
    const wireGeo = new THREE.BufferGeometry();
    wireGeo.setAttribute("position", new THREE.Float32BufferAttribute(wirePositions, 3));
    const wireMat = new THREE.LineBasicMaterial({ color: 0x000000, opacity: 0.5, transparent: true });
    scene.add(new THREE.LineSegments(wireGeo, wireMat));
  }

  // Grid en plano XZ (suelo en Three.js = plano XY en ingeniería)
  const grid = new THREE.GridHelper(size * 1.5, 10, 0x444444, 0x333333);
  grid.position.set(tcx, 0, tcz);
  scene.add(grid);

  // Ejes: X=rojo, Y(Three)=verde=Z(ing), Z(Three)=azul=Y(ing)
  const axes = new THREE.AxesHelper(size * 0.3);
  axes.position.set(xmin - size * 0.1, 0, 0);
  scene.add(axes);

  // Loop de animación
  function animate() {
    requestAnimationFrame(animate);
    controls.update();
    renderer.render(scene, camera);
  }
  animate();

  // Título
  if (opts.title) {
    const titleDiv = document.createElement("div");
    titleDiv.style.cssText = "text-align:center; color:#ddd; font-size:13px; font-family:sans-serif; margin-top:4px;";
    titleDiv.textContent = opts.title;
    container.insertBefore(titleDiv, container.firstChild);
  }
}
