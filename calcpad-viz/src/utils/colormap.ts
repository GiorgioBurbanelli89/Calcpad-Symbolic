// Mapas de color para visualización de resultados FEM
// Cada función recibe t ∈ [0, 1] y retorna [r, g, b] ∈ [0, 255]

// Interpolación lineal entre puntos de color
function lerp(a: number, b: number, f: number): number {
  return Math.round(a + (b - a) * f);
}

// Interpolar en una tabla de colores
function lerpTable(t: number, table: [number, number, number, number][]): [number, number, number] {
  t = Math.max(0, Math.min(1, t));
  for (let i = 0; i < table.length - 1; i++) {
    const [t0, r0, g0, b0] = table[i];
    const [t1, r1, g1, b1] = table[i + 1];
    if (t <= t1) {
      const f = (t - t0) / (t1 - t0);
      return [lerp(r0, r1, f), lerp(g0, g1, f), lerp(b0, b1, f)];
    }
  }
  const last = table[table.length - 1];
  return [last[1], last[2], last[3]];
}

// Jet: azul oscuro → cian → verde → amarillo → rojo oscuro (MATLAB)
// t=0: azul, t=0.25: cian, t=0.5: verde, t=0.75: amarillo, t=1: rojo
export function jet(t: number): [number, number, number] {
  return lerpTable(t, [
    [0.00,   0,   0, 128],  // azul oscuro
    [0.10,   0,   0, 255],  // azul
    [0.35,   0, 255, 255],  // cian
    [0.50,   0, 255,   0],  // verde
    [0.65, 255, 255,   0],  // amarillo
    [0.90, 255,   0,   0],  // rojo
    [1.00, 128,   0,   0],  // rojo oscuro
  ]);
}

// Rainbow: rojo → amarillo → verde → cian → azul
export function rainbow(t: number): [number, number, number] {
  return lerpTable(t, [
    [0.00, 255,   0,   0],  // rojo
    [0.25, 255, 255,   0],  // amarillo
    [0.50,   0, 255,   0],  // verde
    [0.75,   0, 255, 255],  // cian
    [1.00,   0,   0, 255],  // azul
  ]);
}

// Viridis: púrpura → azul → verde → amarillo
export function viridis(t: number): [number, number, number] {
  return lerpTable(t, [
    [0.00,  68,   1,  84],
    [0.25,  59,  82, 139],
    [0.50,  33, 145, 140],
    [0.75,  94, 201,  98],
    [1.00, 253, 231,  37],
  ]);
}

// Coolwarm: azul → blanco → rojo (divergente, para valores con signo)
export function coolwarm(t: number): [number, number, number] {
  return lerpTable(t, [
    [0.00,  59,  76, 192],  // azul
    [0.25, 141, 176, 254],  // azul claro
    [0.50, 245, 245, 245],  // blanco
    [0.75, 245, 150, 118],  // rosa
    [1.00, 180,   4,  38],  // rojo
  ]);
}

// Turbo: similar a jet pero perceptualmente mejor
export function turbo(t: number): [number, number, number] {
  return lerpTable(t, [
    [0.00,  48,  18,  59],
    [0.15,  69, 117, 180],
    [0.30,  49, 199, 149],
    [0.45, 116, 238,  74],
    [0.60, 217, 240,  39],
    [0.75, 253, 174,  26],
    [1.00, 122,   4,   3],
  ]);
}

// Mapas disponibles por nombre
const colormaps: Record<string, (t: number) => [number, number, number]> = {
  jet, rainbow, viridis, coolwarm, turbo,
};

// Obtener colormap por nombre (default: jet)
export function getColormap(name: string): (t: number) => [number, number, number] {
  return colormaps[name.toLowerCase()] || jet;
}

// Convertir [r,g,b] a string CSS
export function rgbToHex(rgb: [number, number, number]): string {
  return `#${rgb.map(c => c.toString(16).padStart(2, "0")).join("")}`;
}
