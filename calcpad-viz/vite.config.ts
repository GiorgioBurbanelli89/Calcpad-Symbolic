import { defineConfig } from "vite";
import { resolve } from "path";

// Build como librería UMD — genera un solo archivo calcpad-viz.js
// que se puede cargar con <script> en el HTML de CalcpadCE
export default defineConfig({
  build: {
    lib: {
      entry: resolve(__dirname, "src/index.ts"), // punto de entrada
      name: "CalcpadViz",                         // nombre global: window.CalcpadViz
      fileName: "calcpad-viz",                     // genera calcpad-viz.js
      formats: ["umd"],                            // Universal Module Definition
    },
    outDir: "dist",          // carpeta de salida
    sourcemap: true,         // para debug
    minify: "esbuild",       // minificación rápida
    rollupOptions: {
      // Three.js se incluye en el bundle (no external)
    },
  },
});
