// index.ts — Punto de entrada del bundle calcpad-viz
// Exporta todas las funciones de visualización como window.CalcpadViz

import { fem2d } from "./fem2d";
import { fem3d } from "./fem3d";
import { chart } from "./chart";

// Exportar para uso como módulo ES
export { fem2d, fem3d, chart };

// Exportar como global para uso en <script> tags del HTML de CalcpadCE
// CalcpadCE genera: CalcpadViz.fem2d("container_id", { datos })
const CalcpadViz = { fem2d, fem3d, chart };

// Registrar en window para acceso global desde el HTML generado
if (typeof window !== "undefined") {
  (window as any).CalcpadViz = CalcpadViz;
}

export default CalcpadViz;
