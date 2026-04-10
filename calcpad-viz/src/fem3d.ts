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

  // Cámara perspectiva — near/far se ajustan al tamaño del modelo mas tarde
  const camera = new THREE.PerspectiveCamera(50, W / H, 0.1, 100000);

  // Renderer WebGL — preserveDrawingBuffer permite screenshots
  const renderer = new THREE.WebGLRenderer({
    antialias: true,
    preserveDrawingBuffer: true
  });
  renderer.setSize(W, H);
  renderer.setPixelRatio(window.devicePixelRatio);

  // Wrapper flex: canvas + leyenda a la derecha (estilo Abaqus/SAP2000)
  const wrapper = document.createElement("div");
  wrapper.style.cssText = "display:inline-flex; align-items:flex-start; gap:8px; background:#1a1a2e; padding:4px;";
  wrapper.appendChild(renderer.domElement);
  container.appendChild(wrapper);

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
  // Ajustar near/far según tamaño del modelo
  camera.near = Math.max(0.001, size * 0.001);
  camera.far = size * 100;
  camera.updateProjectionMatrix();

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

    // Triangulacion segun tipo de elemento
    // - tri3 (3 nodos): 1 triangulo
    // - quad4 (4 nodos): 2 triangulos (1 cara)
    // - tet4 (4 nodos 3D): 4 caras triangulares = 4 triangulos
    //   NOTA: tet4 y quad4 tienen mismo length. Distinguimos por coplanaridad.
    // - hex8 (8 nodos 3D): 6 caras quads = 12 triangulos
    // - tet10 (10 nodos 3D): tratado como tet4 usando los 4 primeros nodos
    // - hex20 (20 nodos 3D): tratado como hex8 usando los 8 primeros nodos
    let tris: number[][] = [];
    let wireEdges: number[][] = [];

    if (elem.length === 3) {
      // Triangulo
      tris = [[0, 1, 2]];
      wireEdges = [[0, 1], [1, 2], [2, 0]];
    } else if (elem.length === 4) {
      // Podria ser quad4 (2D) o tet4 (3D)
      // Detectar por coplanaridad: si los 4 puntos son coplanares → quad
      const p0 = verts[0], p1 = verts[1], p2 = verts[2], p3 = verts[3];
      const v01 = new THREE.Vector3().subVectors(p1, p0);
      const v02 = new THREE.Vector3().subVectors(p2, p0);
      const v03 = new THREE.Vector3().subVectors(p3, p0);
      const normal = new THREE.Vector3().crossVectors(v01, v02);
      const volumen = Math.abs(normal.dot(v03));
      const escala = Math.max(v01.length(), v02.length(), v03.length());
      const isCoplanar = escala > 0 && volumen / (escala * escala * escala) < 1e-6;

      if (isCoplanar) {
        // Quad4: 2 triangulos
        tris = [[0, 1, 2], [0, 2, 3]];
        wireEdges = [[0, 1], [1, 2], [2, 3], [3, 0]];
      } else {
        // Tet4: 4 caras triangulares (orden Abaqus C3D4)
        // Cara 1: 0,1,2 (base)
        // Cara 2: 0,1,3
        // Cara 3: 1,2,3
        // Cara 4: 0,2,3
        tris = [
          [0, 1, 2], [0, 1, 3], [1, 2, 3], [0, 2, 3]
        ];
        wireEdges = [
          [0, 1], [1, 2], [2, 0],  // base
          [0, 3], [1, 3], [2, 3]   // apex
        ];
      }
    } else if (elem.length === 8) {
      // Hex8 (C3D8) — 6 caras quads = 12 triangulos
      // Orden de nodos Abaqus:
      //   Bottom: 0,1,2,3 (z=0)
      //   Top:    4,5,6,7 (z=L)
      // Las 6 caras (outward normal):
      //   Bottom: 0,3,2,1 (normal -z)
      //   Top:    4,5,6,7 (normal +z)
      //   Front:  0,1,5,4 (normal -y)
      //   Right:  1,2,6,5 (normal +x)
      //   Back:   2,3,7,6 (normal +y)
      //   Left:   3,0,4,7 (normal -x)
      const faces = [
        [0, 3, 2, 1],  // bottom
        [4, 5, 6, 7],  // top
        [0, 1, 5, 4],  // front
        [1, 2, 6, 5],  // right
        [2, 3, 7, 6],  // back
        [3, 0, 4, 7],  // left
      ];
      for (const f of faces) {
        tris.push([f[0], f[1], f[2]]);
        tris.push([f[0], f[2], f[3]]);
      }
      // Wireframe: 12 aristas del hexaedro
      wireEdges = [
        // Aristas inferiores (z=0)
        [0, 1], [1, 2], [2, 3], [3, 0],
        // Aristas superiores (z=L)
        [4, 5], [5, 6], [6, 7], [7, 4],
        // Aristas verticales
        [0, 4], [1, 5], [2, 6], [3, 7],
      ];
    } else if (elem.length === 10) {
      // Tet10 (C3D10) — usar solo los 4 primeros nodos como tet4
      tris = [
        [0, 1, 2], [0, 1, 3], [1, 2, 3], [0, 2, 3]
      ];
      wireEdges = [
        [0, 1], [1, 2], [2, 0], [0, 3], [1, 3], [2, 3]
      ];
    } else if (elem.length === 20) {
      // Hex20 (C3D20) — usar solo los 8 primeros nodos como hex8
      const faces = [
        [0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4],
        [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7],
      ];
      for (const f of faces) {
        tris.push([f[0], f[1], f[2]]);
        tris.push([f[0], f[2], f[3]]);
      }
      wireEdges = [
        [0, 1], [1, 2], [2, 3], [3, 0],
        [4, 5], [5, 6], [6, 7], [7, 4],
        [0, 4], [1, 5], [2, 6], [3, 7],
      ];
    } else {
      // Fallback: primer triangulo
      tris = [[0, 1, 2]];
      wireEdges = [[0, 1], [1, 2], [2, 0]];
    }

    for (const [a, b, c] of tris) {
      positions.push(verts[a].x, verts[a].y, verts[a].z);
      positions.push(verts[b].x, verts[b].y, verts[b].z);
      positions.push(verts[c].x, verts[c].y, verts[c].z);
      colors.push(...vertColors[a], ...vertColors[b], ...vertColors[c]);
    }

    // Wireframe
    if (showWire) {
      for (const [i, j] of wireEdges) {
        if (i < verts.length && j < verts.length) {
          const v1 = verts[i];
          const v2 = verts[j];
          wirePositions.push(v1.x, v1.y, v1.z, v2.x, v2.y, v2.z);
        }
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
    transparent: false,
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

  // Leyenda de color (estilo Abaqus/SAP2000) — barra lateral con valores
  if (data.values && data.values.length > 0) {
    const nBands = 12;
    const legendH = H;
    const legend = document.createElement("div");
    legend.style.cssText = "display:flex; flex-direction:column; color:#fff; font-family:sans-serif; font-size:11px; padding:4px 2px; height:" + legendH + "px; flex-shrink:0;";

    // Titulo (pequeño, arriba)
    const legendTitle = document.createElement("div");
    legendTitle.style.cssText = "text-align:center; font-size:10px; color:#ccc; margin-bottom:3px; padding:0 4px; flex-shrink:0;";
    legendTitle.textContent = opts.title || "Value";
    legend.appendChild(legendTitle);

    // Container para barra + labels (flex: 1 para que tome toda la altura restante)
    const bandsContainer = document.createElement("div");
    bandsContainer.style.cssText = "display:flex; flex-direction:row; flex:1; min-height:0;";

    // Barra de color (bandas discretas como Abaqus)
    const colorBar = document.createElement("div");
    colorBar.style.cssText = "display:flex; flex-direction:column; width:22px; border:1px solid #666; flex-shrink:0;";

    // Labels numericos (uno por cada banda + extremos)
    const labelCol = document.createElement("div");
    labelCol.style.cssText = "display:flex; flex-direction:column; justify-content:space-between; margin-left:4px; font-size:10px; line-height:1; padding:1px 0;";

    // Crear bandas de arriba a abajo (max → min)
    for (let i = nBands - 1; i >= 0; i--) {
      const t = i / (nBands - 1);
      const rgb = cmap(t);
      const band = document.createElement("div");
      band.style.cssText = "flex:1; background:rgb(" + rgb[0] + "," + rgb[1] + "," + rgb[2] + ");";
      colorBar.appendChild(band);
    }

    // Labels de arriba a abajo (max → min)
    const nLabels = 7;
    for (let i = 0; i < nLabels; i++) {
      const t = 1 - i / (nLabels - 1);
      const val = vmin + t * (vmax - vmin);
      const label = document.createElement("div");
      label.style.cssText = "white-space:nowrap; text-align:left; color:#fff;";
      // Formato numero: cientifico si es muy chico o muy grande
      const absVal = Math.abs(val);
      let txt: string;
      if (absVal === 0) txt = "0";
      else if (absVal < 0.01 || absVal >= 10000) txt = val.toExponential(2);
      else txt = val.toPrecision(4);
      label.textContent = txt;
      labelCol.appendChild(label);
    }

    bandsContainer.appendChild(colorBar);
    bandsContainer.appendChild(labelCol);
    legend.appendChild(bandsContainer);
    wrapper.appendChild(legend);
  }

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
