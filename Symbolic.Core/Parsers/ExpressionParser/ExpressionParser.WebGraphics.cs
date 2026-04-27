// ─────────────────────────────────────────────────────────────────────────
// Web Graphics directives — Plotly, Three.js, Mermaid, HTML5 Canvas.
//
// El usuario escribe DSL puro entre  #<libreria> ... #end <libreria>
// y el parser inyecta:
//   1. Un <div> contenedor con id único
//   2. El <script src="https://cdn..."> de la librería (una sola vez por HTML)
//   3. Un <script> que pasa el contenido del bloque a la API correcta
//
// SIN que el usuario escriba ningún `<>` HTML.
// ─────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Text;

namespace Calcpad.Core
{
    public partial class ExpressionParser
    {
        internal enum WebGraphicKind {
            Plotly, Three, Mermaid, Canvas,
            // Fase 2:
            Cyto,    // Cytoscape — grafos científicos (sparsity de matrices)
            Dot,     // Graphviz vía viz.js — grafos declarativos DOT
            Jsx,     // JSXGraph — geometría dinámica interactiva
            Map,     // Leaflet — mapas geográficos
            Math,    // KaTeX — fórmulas LaTeX completas
            // Fase 3: 10 librerías adicionales
            Mathbox,  // MathBox 2 — math viz 3D, isosurfaces, vector fields
            D3,       // D3.js v7 — custom data-driven plots
            Echarts,  // Apache ECharts 5 — sankey, parallel, heatmap, treemap
            Vega,     // Vega-Lite 5 — declarative JSON charts
            Visnet,   // vis-network 9 — networks dinámicos
            P5,       // p5.js 1 — creative coding, animaciones
            Matter,   // Matter.js — physics 2D rígidos
            Cannon,   // Cannon-es — physics 3D rígidos
            Geogebra, // GeoGebra applet
            Chart,    // Chart.js 4 — gráficos simples
            // Fase 4: animaciones
            Anime,    // anime.js v4 — animaciones generales
            Manim     // animaciones matemáticas estilo 3blue1brown (vía MathBox + helpers)
        }

        // Estado del bloque activo
        internal bool _insideWebGraphicBlock;
        internal WebGraphicKind _webGraphicKind;
        internal StringBuilder _webGraphicBuffer;
        internal bool _webGraphicSavedVisible;
        internal int _webGraphicSbPositionBeforeLine = -1;
        internal int _webGraphicWidth;
        internal int _webGraphicHeight;
        internal string _webGraphicArgs;   // todo lo que va después de "#plotly ..."

        // Para inyectar el script de la librería UNA SOLA VEZ por HTML output:
        // como _sb se reinicia por cada documento, podemos usar un HashSet
        // que vive en la instancia del parser. Lo limpiamos al inicio de cada
        // Parse() — pero para no romper nada existente, lo verificamos lazy.
        private readonly HashSet<WebGraphicKind> _webGraphicLibsLoaded = new();

        // Contador para IDs únicos de elementos web-graphics dentro del HTML
        private int _webGraphicCounter = 0;


        private void ParseKeywordWebGraphic(ReadOnlySpan<char> s, WebGraphicKind kind)
        {
            // Sintaxis: "#plotly", "#plotly 600 400", "#plotly graph_name"
            // Lo que sigue al keyword se guarda como _webGraphicArgs y la
            // primera y segunda palabras numéricas se interpretan como w, h.
            var text = s.ToString().Trim();
            // Quitar el "#nombre " del inicio
            int spaceIdx = text.IndexOf(' ');
            _webGraphicArgs = spaceIdx > 0 ? text[(spaceIdx + 1)..].Trim() : "";

            // Defaults
            _webGraphicWidth = 700;
            _webGraphicHeight = kind == WebGraphicKind.Three ? 500 : 400;

            // Intentar leer w y h numéricos al inicio de los args
            if (_webGraphicArgs.Length > 0)
            {
                var parts = _webGraphicArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 1 && int.TryParse(parts[0], out var w) && w > 0)
                    _webGraphicWidth = w;
                if (parts.Length >= 2 && int.TryParse(parts[1], out var h) && h > 0)
                    _webGraphicHeight = h;
            }

            _insideWebGraphicBlock = true;
            _webGraphicKind = kind;
            _webGraphicBuffer = new StringBuilder(2048);
            _webGraphicSavedVisible = _isVisible;
            _webGraphicSbPositionBeforeLine = -1;
        }


        private void ParseKeywordEndWebGraphic(WebGraphicKind kind)
        {
            if (!_insideWebGraphicBlock || _webGraphicBuffer is null)
            {
                AppendError($"#end {kind.ToString().ToLowerInvariant()}",
                    $"No matching #{kind.ToString().ToLowerInvariant()}", _currentLine);
                return;
            }
            if (_webGraphicKind != kind)
            {
                AppendError($"#end {kind.ToString().ToLowerInvariant()}",
                    $"Expected #end {_webGraphicKind.ToString().ToLowerInvariant()}", _currentLine);
                _insideWebGraphicBlock = false;
                _webGraphicBuffer = null;
                return;
            }

            _insideWebGraphicBlock = false;
            _webGraphicSbPositionBeforeLine = -1;

            if (_webGraphicSavedVisible)
            {
                var content = _webGraphicBuffer.ToString();
                var html = kind switch
                {
                    WebGraphicKind.Plotly => RenderPlotly(content),
                    WebGraphicKind.Three => RenderThree(content),
                    WebGraphicKind.Mermaid => RenderMermaid(content),
                    WebGraphicKind.Canvas => RenderCanvas(content),
                    WebGraphicKind.Cyto => RenderCyto(content),
                    WebGraphicKind.Dot => RenderDot(content),
                    WebGraphicKind.Jsx => RenderJsx(content),
                    WebGraphicKind.Map => RenderMap(content),
                    WebGraphicKind.Math => RenderMath(content),
                    WebGraphicKind.Mathbox => RenderMathbox(content),
                    WebGraphicKind.D3 => RenderD3(content),
                    WebGraphicKind.Echarts => RenderEcharts(content),
                    WebGraphicKind.Vega => RenderVega(content),
                    WebGraphicKind.Visnet => RenderVisnet(content),
                    WebGraphicKind.P5 => RenderP5(content),
                    WebGraphicKind.Matter => RenderMatter(content),
                    WebGraphicKind.Cannon => RenderCannon(content),
                    WebGraphicKind.Geogebra => RenderGeogebra(content),
                    WebGraphicKind.Chart => RenderChart(content),
                    WebGraphicKind.Anime => RenderAnime(content),
                    WebGraphicKind.Manim => RenderManim(content),
                    _ => ""
                };
                _sb.Append(html);
            }
            _webGraphicBuffer = null;
        }


        /// <summary>Capture a line of content into the active web-graphic buffer.
        /// Restaura los operadores ASCII que el lexer de Calcpad había
        /// sustituido a Unicode (≤ ≥ ≡ ≢ ≠) — sino el JS/JSON queda roto.</summary>
        internal void ProcessWebGraphicLine(string line)
        {
            if (_webGraphicBuffer is null || !_webGraphicSavedVisible) return;
            _webGraphicBuffer.AppendLine(RestoreAsciiOperators(line));
        }

        /// <summary>Reverse Calcpad's Unicode operator substitutions back to ASCII
        /// for embedding raw JS/JSON in browser-side scripts.
        /// Calcpad lexer hace: <= → ≤  ;  >= → ≥  ;  == → ≡  ;  != → ≢  ;  &lt;&gt; → ≠
        /// Acá revertimos cada char Unicode al par ASCII original.</summary>
        private static string RestoreAsciiOperators(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s
                .Replace("\u2264", "<=")
                .Replace("\u2265", ">=")
                .Replace("\u2261", "==")
                .Replace("\u2262", "!=")
                .Replace("\u2260", "!=");
        }


        // ─────────────────────────────────────────────────────────────────
        // RENDER: Plotly
        //   Sintaxis: el contenido es un objeto JS literal con data y layout
        //
        //   #plotly 700 400
        //   {
        //     data: [{x:[1,2,3], y:[4,5,6], type:'scatter'}],
        //     layout: {title: 'demo'}
        //   }
        //   #end plotly
        // ─────────────────────────────────────────────────────────────────
        private string RenderPlotly(string content)
        {
            var id = $"plotly_{++_webGraphicCounter}";
            var sb = new StringBuilder(2048);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append($"<div id=\"{id}\" style=\"width:{_webGraphicWidth}px;height:{_webGraphicHeight}px\"></div>\n");
            sb.Append(LoadLibrary(WebGraphicKind.Plotly));
            sb.Append("<script>\n");
            sb.Append("(function(){\n");
            sb.Append("  var spec = ").Append(content.Trim()).Append(";\n");
            sb.Append("  var data = spec.data || [];\n");
            sb.Append("  var layout = spec.layout || {};\n");
            sb.Append($"  Plotly.newPlot('{id}', data, layout, {{responsive:true}});\n");
            sb.Append("})();\n");
            sb.Append("</script>\n");
            sb.Append("</div>\n");
            return sb.ToString();
        }


        // ─────────────────────────────────────────────────────────────────
        // RENDER: Three.js
        //   Sintaxis: el contenido es código JavaScript que tiene acceso a
        //   variables `scene`, `camera`, `renderer`, `THREE`, `width`, `height`.
        //
        //   #three 600 500
        //     const cube = new THREE.Mesh(
        //         new THREE.BoxGeometry(1,1,1),
        //         new THREE.MeshStandardMaterial({color:0xffd966}));
        //     scene.add(cube);
        //     camera.position.z = 4;
        //     scene.add(new THREE.AmbientLight(0xffffff, 0.5));
        //     scene.add(new THREE.DirectionalLight(0xffffff, 0.8));
        //   #end three
        // ─────────────────────────────────────────────────────────────────
        private string RenderThree(string content)
        {
            var id = $"three_{++_webGraphicCounter}";
            var sb = new StringBuilder(2048);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append($"<div id=\"{id}\" style=\"width:{_webGraphicWidth}px;height:{_webGraphicHeight}px;");
            sb.Append("border:1px solid #ccc;background:#fafafa\"></div>\n");
            sb.Append(LoadLibrary(WebGraphicKind.Three));
            sb.Append("<script type=\"module\">\n");
            // Three.js usa el bare specifier 'three' internamente (OrbitControls
            // hace `import * as THREE from 'three'`). Sin importmap el browser
            // tira "Failed to resolve module specifier 'three'". Inyectamos el
            // map una sola vez (LoadLibrary se encarga). Acá importamos como
            // THREE y OrbitControls usando el alias.
            sb.Append("import * as THREE from 'three';\n");
            sb.Append("import {OrbitControls} from 'three/addons/controls/OrbitControls.js';\n");
            sb.Append("(function(){\n");
            sb.Append($"  const container = document.getElementById('{id}');\n");
            sb.Append($"  const width = {_webGraphicWidth};\n");
            sb.Append($"  const height = {_webGraphicHeight};\n");
            sb.Append("  const scene = new THREE.Scene();\n");
            sb.Append("  scene.background = new THREE.Color(0xfafafa);\n");
            sb.Append("  const camera = new THREE.PerspectiveCamera(60, width/height, 0.1, 1000);\n");
            sb.Append("  camera.position.set(5, 5, 5); camera.lookAt(0, 0, 0);\n");
            sb.Append("  const renderer = new THREE.WebGLRenderer({antialias:true});\n");
            sb.Append("  renderer.setSize(width, height);\n");
            sb.Append("  container.appendChild(renderer.domElement);\n");
            sb.Append("  const controls = new OrbitControls(camera, renderer.domElement);\n");
            sb.Append("  // ─ user code ─\n");
            sb.Append(content);
            sb.Append("\n  // ─ end user code ─\n");
            sb.Append("  function animate(){requestAnimationFrame(animate);controls.update();renderer.render(scene, camera);}\n");
            sb.Append("  animate();\n");
            sb.Append("})();\n");
            sb.Append("</script>\n");
            sb.Append("</div>\n");
            return sb.ToString();
        }


        // ─────────────────────────────────────────────────────────────────
        // RENDER: Mermaid
        //   El contenido es DSL Mermaid puro (flowchart, sequence, gantt,
        //   classDiagram, pie, gitGraph, etc.). Mermaid lee el `<div class
        //   ="mermaid">` y lo reemplaza por el SVG renderizado.
        //
        //   #mermaid
        //   flowchart TD
        //     A[DEAD] --> B[1.4D]
        //     A --> C[1.2D + 1.6L]
        //   #end mermaid
        // ─────────────────────────────────────────────────────────────────
        private string RenderMermaid(string content)
        {
            var id = $"mermaid_{++_webGraphicCounter}";
            var sb = new StringBuilder(1024);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append($"<div id=\"{id}\" class=\"mermaid\" style=\"max-width:{_webGraphicWidth}px\">\n");
            sb.Append(content);
            sb.Append("</div>\n");
            sb.Append(LoadLibrary(WebGraphicKind.Mermaid));
            sb.Append("</div>\n");
            return sb.ToString();
        }


        // ─────────────────────────────────────────────────────────────────
        // RENDER: HTML5 Canvas
        //   El contenido es JavaScript con acceso a `ctx` (Canvas2D context),
        //   `canvas`, `width`, `height`.
        //
        //   #canvas 500 300
        //     ctx.fillStyle = '#ffd966';
        //     ctx.fillRect(50, 50, 200, 100);
        //     ctx.font = '20px sans-serif';
        //     ctx.fillText('Hola', 60, 120);
        //   #end canvas
        // ─────────────────────────────────────────────────────────────────
        private string RenderCanvas(string content)
        {
            var id = $"canvas_{++_webGraphicCounter}";
            var sb = new StringBuilder(1024);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append($"<canvas id=\"{id}\" width=\"{_webGraphicWidth}\" height=\"{_webGraphicHeight}\" ");
            sb.Append("style=\"border:1px solid #ccc;background:#fafafa\"></canvas>\n");
            sb.Append("<script>\n");
            sb.Append("(function(){\n");
            sb.Append($"  const canvas = document.getElementById('{id}');\n");
            sb.Append("  const ctx = canvas.getContext('2d');\n");
            sb.Append($"  const width = {_webGraphicWidth};\n");
            sb.Append($"  const height = {_webGraphicHeight};\n");
            sb.Append("  // ─ user code ─\n");
            sb.Append(content);
            sb.Append("\n  // ─ end user code ─\n");
            sb.Append("})();\n");
            sb.Append("</script>\n");
            sb.Append("</div>\n");
            return sb.ToString();
        }


        // ─────────────────────────────────────────────────────────────────
        // Carga la librería desde CDN una SOLA VEZ por HTML output.
        // Si ya se cargó antes (porque hubo un bloque previo de la misma
        // librería), retorna string vacío.
        // ─────────────────────────────────────────────────────────────────
        private string LoadLibrary(WebGraphicKind kind)
        {
            if (_webGraphicLibsLoaded.Contains(kind)) return "";
            _webGraphicLibsLoaded.Add(kind);

            return kind switch
            {
                WebGraphicKind.Plotly =>
                    "<script src=\"https://cdn.plot.ly/plotly-2.35.2.min.js\"></script>\n",

                WebGraphicKind.Three =>
                    // Importmap para que el bare specifier 'three' resuelva
                    // a la URL real desde unpkg. Lo inyectamos UNA SOLA VEZ
                    // por documento. Tiene que ir ANTES de cualquier <script
                    // type="module"> que use 'three'.
                    "<script type=\"importmap\">\n" +
                    "{\n" +
                    "  \"imports\": {\n" +
                    "    \"three\": \"https://unpkg.com/three@0.160.0/build/three.module.js\",\n" +
                    "    \"three/addons/\": \"https://unpkg.com/three@0.160.0/examples/jsm/\"\n" +
                    "  }\n" +
                    "}\n" +
                    "</script>\n",

                WebGraphicKind.Mermaid =>
                    "<script type=\"module\">\n" +
                    "import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.esm.min.mjs';\n" +
                    "mermaid.initialize({startOnLoad:true, theme:'default'});\n" +
                    "</script>\n",

                WebGraphicKind.Canvas => "",   // API nativa, no requiere lib

                WebGraphicKind.Cyto =>
                    "<script src=\"https://unpkg.com/cytoscape@3/dist/cytoscape.min.js\"></script>\n",

                WebGraphicKind.Dot =>
                    // viz-js es Graphviz compilado a WASM, ~2MB pero render
                    // de DOT bonito y rápido.
                    "<script type=\"module\">\n" +
                    "import { instance } from 'https://unpkg.com/@viz-js/viz@3.7.0/lib/viz-standalone.mjs';\n" +
                    "window.__vizPromise = instance();\n" +
                    "</script>\n",

                WebGraphicKind.Jsx =>
                    "<link rel=\"stylesheet\" href=\"https://cdn.jsdelivr.net/npm/jsxgraph@1.10.0/distrib/jsxgraph.css\">\n" +
                    "<script src=\"https://cdn.jsdelivr.net/npm/jsxgraph@1.10.0/distrib/jsxgraphcore.min.js\"></script>\n",

                WebGraphicKind.Map =>
                    "<link rel=\"stylesheet\" href=\"https://unpkg.com/leaflet@1.9.4/dist/leaflet.css\">\n" +
                    "<script src=\"https://unpkg.com/leaflet@1.9.4/dist/leaflet.js\"></script>\n",

                WebGraphicKind.Math =>
                    "<link rel=\"stylesheet\" href=\"https://cdn.jsdelivr.net/npm/katex@0.16.9/dist/katex.min.css\">\n" +
                    "<script src=\"https://cdn.jsdelivr.net/npm/katex@0.16.9/dist/katex.min.js\"></script>\n",

                // Fase 3 — 10 librerías adicionales
                WebGraphicKind.Mathbox =>
                    // MathBox 2.3.1 UMD bundle requires:
                    //  1. window.THREE (UMD) — Three.js dropped UMD after r147,
                    //     so we use 0.146.0 which is the last version with both
                    //     UMD bundle and examples/js/controls/OrbitControls.js
                    //     loadable as a script tag (no ESM required).
                    //  2. THREE.OrbitControls global — needed for controls.klass.
                    //  3. window.MathBox factory — exposed by mathbox bundle.
                    "<link rel=\"stylesheet\" href=\"https://cdn.jsdelivr.net/npm/mathbox@2.3.1/build/mathbox.css\">\n" +
                    "<script src=\"https://unpkg.com/three@0.146.0/build/three.min.js\"></script>\n" +
                    "<script src=\"https://unpkg.com/three@0.146.0/examples/js/controls/OrbitControls.js\"></script>\n" +
                    "<script src=\"https://cdn.jsdelivr.net/npm/mathbox@2.3.1/build/bundle/mathbox.min.js\"></script>\n",

                WebGraphicKind.D3 =>
                    "<script src=\"https://cdn.jsdelivr.net/npm/d3@7.8.5/dist/d3.min.js\"></script>\n",

                WebGraphicKind.Echarts =>
                    "<script src=\"https://cdn.jsdelivr.net/npm/echarts@5.4.3/dist/echarts.min.js\"></script>\n",

                WebGraphicKind.Vega =>
                    "<script src=\"https://cdn.jsdelivr.net/npm/vega@5.30.0/build/vega.min.js\"></script>\n" +
                    "<script src=\"https://cdn.jsdelivr.net/npm/vega-lite@5.21.0/build/vega-lite.min.js\"></script>\n" +
                    "<script src=\"https://cdn.jsdelivr.net/npm/vega-embed@6.26.0/build/vega-embed.min.js\"></script>\n",

                WebGraphicKind.Visnet =>
                    "<link rel=\"stylesheet\" href=\"https://cdn.jsdelivr.net/npm/vis-network@9.1.9/dist/dist/vis-network.min.css\">\n" +
                    "<script src=\"https://cdn.jsdelivr.net/npm/vis-network@9.1.9/standalone/umd/vis-network.min.js\"></script>\n",

                WebGraphicKind.P5 =>
                    "<script src=\"https://cdn.jsdelivr.net/npm/p5@1.10.0/lib/p5.min.js\"></script>\n",

                WebGraphicKind.Matter =>
                    "<script src=\"https://cdn.jsdelivr.net/npm/matter-js@0.20.0/build/matter.min.js\"></script>\n",

                WebGraphicKind.Cannon =>
                    // Cannon-es es ESM; lo importamos en cada bloque, pero pre-cargamos como
                    // import map para que `import * as CANNON from 'cannon-es'` funcione.
                    "<script type=\"importmap\">\n" +
                    "{\n" +
                    "  \"imports\": {\n" +
                    "    \"cannon-es\": \"https://cdn.jsdelivr.net/npm/cannon-es@0.20.0/dist/cannon-es.js\"\n" +
                    "  }\n" +
                    "}\n" +
                    "</script>\n",

                WebGraphicKind.Geogebra =>
                    "<script src=\"https://www.geogebra.org/apps/deployggb.js\"></script>\n",

                WebGraphicKind.Chart =>
                    "<script src=\"https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js\"></script>\n",

                // Fase 4 — animaciones
                WebGraphicKind.Anime =>
                    // anime.js v3 (UMD, expone window.anime)
                    "<script src=\"https://cdn.jsdelivr.net/npm/animejs@3.2.2/lib/anime.min.js\"></script>\n",

                WebGraphicKind.Manim =>
                    // #manim usa MathBox como motor (estilo 3blue1brown).
                    // Misma carga que #mathbox: Three.js UMD r146 + OrbitControls + MathBox.
                    "<link rel=\"stylesheet\" href=\"https://cdn.jsdelivr.net/npm/mathbox@2.3.1/build/mathbox.css\">\n" +
                    "<script src=\"https://unpkg.com/three@0.146.0/build/three.min.js\"></script>\n" +
                    "<script src=\"https://unpkg.com/three@0.146.0/examples/js/controls/OrbitControls.js\"></script>\n" +
                    "<script src=\"https://cdn.jsdelivr.net/npm/mathbox@2.3.1/build/bundle/mathbox.min.js\"></script>\n",

                _ => ""
            };
        }


        // ─────────────────────────────────────────────────────────────────
        // RENDER: Cytoscape — grafos científicos (sparsity matrix, networks)
        //   #cyto 700 500
        //   {
        //     elements: [
        //       {data:{id:'a'}}, {data:{id:'b'}}, {data:{id:'c'}},
        //       {data:{id:'ab', source:'a', target:'b'}}
        //     ],
        //     style: [{selector:'node', style:{label:'data(id)'}}],
        //     layout: {name:'cose'}
        //   }
        //   #end cyto
        // ─────────────────────────────────────────────────────────────────
        private string RenderCyto(string content)
        {
            var id = $"cyto_{++_webGraphicCounter}";
            var sb = new StringBuilder(2048);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append($"<div id=\"{id}\" style=\"width:{_webGraphicWidth}px;height:{_webGraphicHeight}px;");
            sb.Append("border:1px solid #ccc;background:#fafafa\"></div>\n");
            sb.Append(LoadLibrary(WebGraphicKind.Cyto));
            sb.Append("<script>\n");
            sb.Append("(function(){\n");
            sb.Append("  var spec = ").Append(content.Trim()).Append(";\n");
            sb.Append($"  spec.container = document.getElementById('{id}');\n");
            sb.Append("  cytoscape(spec);\n");
            sb.Append("})();\n");
            sb.Append("</script>\n");
            sb.Append("</div>\n");
            return sb.ToString();
        }


        // ─────────────────────────────────────────────────────────────────
        // RENDER: Graphviz DOT
        //   #dot 600 400
        //   digraph G {
        //     rankdir=LR;
        //     A -> B [label="x"];
        //     B -> C;
        //   }
        //   #end dot
        // ─────────────────────────────────────────────────────────────────
        private string RenderDot(string content)
        {
            var id = $"dot_{++_webGraphicCounter}";
            var sb = new StringBuilder(2048);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append($"<div id=\"{id}\" style=\"max-width:{_webGraphicWidth}px;text-align:center\"></div>\n");
            sb.Append(LoadLibrary(WebGraphicKind.Dot));
            sb.Append("<script type=\"module\">\n");
            sb.Append("(async function(){\n");
            sb.Append("  const viz = await window.__vizPromise;\n");
            sb.Append($"  const dot = ").Append(JsString(content)).Append(";\n");
            sb.Append($"  const el = document.getElementById('{id}');\n");
            sb.Append("  el.appendChild(viz.renderSVGElement(dot));\n");
            sb.Append("})();\n");
            sb.Append("</script>\n");
            sb.Append("</div>\n");
            return sb.ToString();
        }


        // ─────────────────────────────────────────────────────────────────
        // RENDER: JSXGraph — geometría dinámica
        //   #jsx 500 500
        //     const board = JXG.JSXGraph.initBoard('JSXBOARD', {
        //         boundingbox:[-5,5,5,-5], axis:true});
        //     const A = board.create('point',[1,2],{name:'A'});
        //     const B = board.create('point',[3,4],{name:'B'});
        //     const seg = board.create('segment',[A,B]);
        //   #end jsx
        //
        // El parser inyecta 'JSXBOARD' como el id del board del user.
        // ─────────────────────────────────────────────────────────────────
        private string RenderJsx(string content)
        {
            var id = $"jsx_{++_webGraphicCounter}";
            var sb = new StringBuilder(2048);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append($"<div id=\"{id}\" class=\"jxgbox\" style=\"width:{_webGraphicWidth}px;");
            sb.Append($"height:{_webGraphicHeight}px;border:1px solid #ccc\"></div>\n");
            sb.Append(LoadLibrary(WebGraphicKind.Jsx));
            sb.Append("<script>\n");
            sb.Append("(function(){\n");
            sb.Append($"  const JSXBOARD = '{id}';\n");
            sb.Append("  // ─ user code ─\n");
            sb.Append(content);
            sb.Append("\n  // ─ end user code ─\n");
            sb.Append("})();\n");
            sb.Append("</script>\n");
            sb.Append("</div>\n");
            return sb.ToString();
        }


        // ─────────────────────────────────────────────────────────────────
        // RENDER: Leaflet — mapas geográficos
        //   #map 600 400
        //     const map = L.map(MAPID).setView([-0.18, -78.47], 12);
        //     L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
        //                 {attribution:'OSM'}).addTo(map);
        //     L.marker([-0.18, -78.47]).addTo(map).bindPopup('Quito');
        //   #end map
        // ─────────────────────────────────────────────────────────────────
        private string RenderMap(string content)
        {
            var id = $"map_{++_webGraphicCounter}";
            var sb = new StringBuilder(2048);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append($"<div id=\"{id}\" style=\"width:{_webGraphicWidth}px;height:{_webGraphicHeight}px;");
            sb.Append("border:1px solid #ccc\"></div>\n");
            sb.Append(LoadLibrary(WebGraphicKind.Map));
            sb.Append("<script>\n");
            sb.Append("(function(){\n");
            sb.Append($"  const MAPID = '{id}';\n");
            sb.Append("  // ─ user code ─\n");
            sb.Append(content);
            sb.Append("\n  // ─ end user code ─\n");
            sb.Append("})();\n");
            sb.Append("</script>\n");
            sb.Append("</div>\n");
            return sb.ToString();
        }


        // ─────────────────────────────────────────────────────────────────
        // RENDER: KaTeX — LaTeX puro a math display
        //   #math
        //     \frac{\partial^2 w}{\partial x^2} + \frac{\partial^2 w}{\partial y^2} = \frac{q}{D}
        //   #end math
        //
        // Soporta múltiples líneas, cada una se renderiza como display math.
        // ─────────────────────────────────────────────────────────────────
        private string RenderMath(string content)
        {
            var id = $"math_{++_webGraphicCounter}";
            var sb = new StringBuilder(1024);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append($"<div id=\"{id}\" style=\"max-width:{_webGraphicWidth}px;\"></div>\n");
            sb.Append(LoadLibrary(WebGraphicKind.Math));
            sb.Append("<script>\n");
            sb.Append("(function(){\n");
            sb.Append($"  const el = document.getElementById('{id}');\n");
            sb.Append($"  const latex = ").Append(JsString(content.Trim())).Append(";\n");
            sb.Append("  // KaTeX render display: cada línea no vacía es un display math separado.\n");
            sb.Append("  const lines = latex.split('\\n').filter(l => l.trim().length > 0);\n");
            sb.Append("  lines.forEach(line => {\n");
            sb.Append("    const div = document.createElement('div');\n");
            sb.Append("    div.style.margin = '0.6em 0';\n");
            sb.Append("    try { katex.render(line, div, {displayMode:true, throwOnError:false}); }\n");
            sb.Append("    catch(e) { div.textContent = e.message; div.style.color='#c33'; }\n");
            sb.Append("    el.appendChild(div);\n");
            sb.Append("  });\n");
            sb.Append("})();\n");
            sb.Append("</script>\n");
            sb.Append("</div>\n");
            return sb.ToString();
        }


        // Helper: convertir un string C# a literal JS-safe (escape de comillas
        // y newlines). Usado por los renderers que pasan el contenido como
        // string a la API JS (Graphviz, KaTeX).
        private static string JsString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "\"\"";
            return "\"" + s
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "")
                + "\"";
        }

        // ═════════════════════════════════════════════════════════════════
        // FASE 3 — 10 librerías adicionales de visualización web
        // ═════════════════════════════════════════════════════════════════

        // ─────────────────────────────────────────────────────────────────
        // RENDER: MathBox 2 — visualización matemática 3D (built on Three.js)
        //   Ideal para: isosurfaces de f(x,y,z), campos vectoriales, surfaces
        //   paramétricas, animaciones matemáticas estilo 3blue1brown.
        //
        //   #mathbox 700 500
        //     mathbox.set('focus', 3);
        //     var view = mathbox.cartesian({range:[[-2,2],[-2,2],[-2,2]], scale:[1,1,1]});
        //     view.axis({axis:1}); view.axis({axis:2}); view.axis({axis:3});
        //     view.grid({divideX:10, divideY:10});
        //     view.area({width:64, height:64,
        //       expr:function(emit,x,y,i,j){emit(x, y, Math.sin(x)*Math.cos(y));}});
        //     view.surface({shaded:true, color:0x3090ff});
        //   #end mathbox
        // ─────────────────────────────────────────────────────────────────
        private string RenderMathbox(string content)
        {
            var id = $"mathbox_{++_webGraphicCounter}";
            var sb = new StringBuilder(2048);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append($"<div id=\"{id}\" style=\"width:{_webGraphicWidth}px;height:{_webGraphicHeight}px;");
            sb.Append("border:1px solid #ccc;background:#fafafa\"></div>\n");
            sb.Append(LoadLibrary(WebGraphicKind.Mathbox));
            sb.Append("<script>\n");
            sb.Append("(function(){\n");
            sb.Append($"  var container = document.getElementById('{id}');\n");
            sb.Append("  // MathBox 2 UMD: window.THREE viene de three.min.js (cargado antes),\n");
            sb.Append("  // window.MathBox es el namespace que expone mathBox() factory.\n");
            sb.Append("  var mathbox = MathBox.mathBox({\n");
            sb.Append("    plugins: ['core', 'controls', 'cursor'],\n");
            sb.Append("    controls: { klass: THREE.OrbitControls },\n");
            sb.Append("    element: container,\n");
            sb.Append($"   camera: {{ near: 0.1, far: 1000 }},\n");
            sb.Append($"   size: {{ width: {_webGraphicWidth}, height: {_webGraphicHeight} }}\n");
            sb.Append("  });\n");
            sb.Append("  var three = mathbox.three || mathbox;\n");
            sb.Append("  if (three.camera) three.camera.position.set(2.5, 2.5, 2.5);\n");
            sb.Append("  if (three.renderer) three.renderer.setClearColor(new THREE.Color(0xfafafa), 1.0);\n");
            sb.Append("  // ─ user code ─\n");
            sb.Append(content);
            sb.Append("\n  // ─ end user code ─\n");
            sb.Append("})();\n");
            sb.Append("</script>\n");
            sb.Append("</div>\n");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────
        // RENDER: D3.js — custom data-driven plots
        //   Acceso a `svg` (D3 selection del SVG ya creado), `width`, `height`.
        //
        //   #d3 600 400
        //     const data = [10, 25, 30, 45, 60];
        //     const x = d3.scaleLinear().domain([0,4]).range([40, width-20]);
        //     const y = d3.scaleLinear().domain([0,60]).range([height-30, 20]);
        //     svg.selectAll('circle').data(data).enter().append('circle')
        //       .attr('cx', (d,i) => x(i)).attr('cy', d => y(d))
        //       .attr('r', 5).attr('fill', 'steelblue');
        //   #end d3
        // ─────────────────────────────────────────────────────────────────
        private string RenderD3(string content)
        {
            var id = $"d3_{++_webGraphicCounter}";
            var sb = new StringBuilder(2048);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append($"<svg id=\"{id}\" width=\"{_webGraphicWidth}\" height=\"{_webGraphicHeight}\" ");
            sb.Append("style=\"border:1px solid #ccc;background:#fafafa\"></svg>\n");
            sb.Append(LoadLibrary(WebGraphicKind.D3));
            sb.Append("<script>\n");
            sb.Append("(function(){\n");
            sb.Append($"  const svg = d3.select('#{id}');\n");
            sb.Append($"  const width = {_webGraphicWidth};\n");
            sb.Append($"  const height = {_webGraphicHeight};\n");
            sb.Append("  // ─ user code ─\n");
            sb.Append(content);
            sb.Append("\n  // ─ end user code ─\n");
            sb.Append("})();\n");
            sb.Append("</script>\n");
            sb.Append("</div>\n");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────
        // RENDER: Apache ECharts 5
        //   Sintaxis: el contenido es el `option` JS literal.
        //
        //   #echarts 700 400
        //   {
        //     title: {text: 'Demo'},
        //     xAxis: {data: ['A','B','C','D']},
        //     yAxis: {},
        //     series: [{name:'sales', type:'bar', data:[5,20,36,10]}]
        //   }
        //   #end echarts
        // ─────────────────────────────────────────────────────────────────
        private string RenderEcharts(string content)
        {
            var id = $"echarts_{++_webGraphicCounter}";
            var sb = new StringBuilder(2048);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append($"<div id=\"{id}\" style=\"width:{_webGraphicWidth}px;height:{_webGraphicHeight}px\"></div>\n");
            sb.Append(LoadLibrary(WebGraphicKind.Echarts));
            sb.Append("<script>\n");
            sb.Append("(function(){\n");
            sb.Append($"  var chart = echarts.init(document.getElementById('{id}'));\n");
            sb.Append("  var option = ").Append(content.Trim()).Append(";\n");
            sb.Append("  chart.setOption(option);\n");
            sb.Append("  window.addEventListener('resize', () => chart.resize());\n");
            sb.Append("})();\n");
            sb.Append("</script>\n");
            sb.Append("</div>\n");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────
        // RENDER: Vega-Lite — declarative JSON charts
        //   Sintaxis: JSON Vega-Lite spec.
        //
        //   #vega 600 400
        //   {
        //     "data": {"values":[{"x":1,"y":2},{"x":2,"y":3}]},
        //     "mark": "bar",
        //     "encoding": {"x":{"field":"x","type":"quantitative"},
        //                  "y":{"field":"y","type":"quantitative"}}
        //   }
        //   #end vega
        // ─────────────────────────────────────────────────────────────────
        private string RenderVega(string content)
        {
            var id = $"vega_{++_webGraphicCounter}";
            var sb = new StringBuilder(2048);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append($"<div id=\"{id}\" style=\"width:{_webGraphicWidth}px;min-height:{_webGraphicHeight}px\"></div>\n");
            sb.Append(LoadLibrary(WebGraphicKind.Vega));
            sb.Append("<script>\n");
            sb.Append("(function(){\n");
            sb.Append("  var spec = ").Append(content.Trim()).Append(";\n");
            sb.Append($"  if (!spec.width) spec.width = {_webGraphicWidth - 40};\n");
            sb.Append($"  if (!spec.height) spec.height = {_webGraphicHeight - 40};\n");
            sb.Append($"  vegaEmbed('#{id}', spec, {{actions:false}});\n");
            sb.Append("})();\n");
            sb.Append("</script>\n");
            sb.Append("</div>\n");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────
        // RENDER: vis-network — networks dinámicos
        //   #visnet 700 500
        //   {
        //     nodes: [{id:1, label:'A'}, {id:2, label:'B'}],
        //     edges: [{from:1, to:2}],
        //     options: {physics:{enabled:true}}
        //   }
        //   #end visnet
        // ─────────────────────────────────────────────────────────────────
        private string RenderVisnet(string content)
        {
            var id = $"visnet_{++_webGraphicCounter}";
            var sb = new StringBuilder(2048);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append($"<div id=\"{id}\" style=\"width:{_webGraphicWidth}px;height:{_webGraphicHeight}px;");
            sb.Append("border:1px solid #ccc;background:#fafafa\"></div>\n");
            sb.Append(LoadLibrary(WebGraphicKind.Visnet));
            sb.Append("<script>\n");
            sb.Append("(function(){\n");
            sb.Append("  var spec = ").Append(content.Trim()).Append(";\n");
            sb.Append($"  var container = document.getElementById('{id}');\n");
            sb.Append("  var data = {nodes: new vis.DataSet(spec.nodes||[]), edges: new vis.DataSet(spec.edges||[])};\n");
            sb.Append("  var options = spec.options || {};\n");
            sb.Append("  new vis.Network(container, data, options);\n");
            sb.Append("})();\n");
            sb.Append("</script>\n");
            sb.Append("</div>\n");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────
        // RENDER: p5.js — creative coding & animations
        //   Acceso a p5 sketch with setup() / draw() global functions.
        //
        //   #p5 600 400
        //     function setup() { createCanvas(width, height); }
        //     function draw() {
        //       background(220);
        //       ellipse(mouseX, mouseY, 50, 50);
        //     }
        //   #end p5
        // ─────────────────────────────────────────────────────────────────
        private string RenderP5(string content)
        {
            var id = $"p5_{++_webGraphicCounter}";
            var sb = new StringBuilder(2048);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append($"<div id=\"{id}\" style=\"width:{_webGraphicWidth}px;");
            sb.Append("border:1px solid #ccc;background:#fafafa\"></div>\n");
            sb.Append(LoadLibrary(WebGraphicKind.P5));
            sb.Append("<script>\n");
            sb.Append("(function(){\n");
            sb.Append($"  var width = {_webGraphicWidth};\n");
            sb.Append($"  var height = {_webGraphicHeight};\n");
            sb.Append("  var sketch = function(p) {\n");
            sb.Append("    var setup = null, draw = null, mousePressed = null;\n");
            sb.Append("    // wrap user code into closure with p5 instance\n");
            sb.Append("    var w = width, h = height;\n");
            sb.Append("    var fn = new Function('p', 'width', 'height',\n");
            sb.Append("      `with(p){\n");
            sb.Append(content.Replace("\\", "\\\\").Replace("`", "\\`"));
            sb.Append("\n      if (typeof setup==='function') p.setup = setup;\n");
            sb.Append("      if (typeof draw==='function') p.draw = draw;\n");
            sb.Append("      if (typeof mousePressed==='function') p.mousePressed = mousePressed;\n");
            sb.Append("      }`);\n");
            sb.Append("    fn(p, w, h);\n");
            sb.Append("  };\n");
            sb.Append($"  new p5(sketch, '{id}');\n");
            sb.Append("})();\n");
            sb.Append("</script>\n");
            sb.Append("</div>\n");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────
        // RENDER: Matter.js — physics 2D
        //   Acceso a `engine`, `world`, `render`, `Matter`, `width`, `height`.
        //
        //   #matter 600 400
        //     var ground = Matter.Bodies.rectangle(width/2, height-25, width, 50, {isStatic:true});
        //     var ball = Matter.Bodies.circle(width/2, 50, 30, {restitution:0.8});
        //     Matter.World.add(world, [ground, ball]);
        //   #end matter
        // ─────────────────────────────────────────────────────────────────
        private string RenderMatter(string content)
        {
            var id = $"matter_{++_webGraphicCounter}";
            var sb = new StringBuilder(2048);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append($"<div id=\"{id}\" style=\"width:{_webGraphicWidth}px;height:{_webGraphicHeight}px;");
            sb.Append("border:1px solid #ccc;background:#fafafa\"></div>\n");
            sb.Append(LoadLibrary(WebGraphicKind.Matter));
            sb.Append("<script>\n");
            sb.Append("(function(){\n");
            sb.Append($"  var width = {_webGraphicWidth}, height = {_webGraphicHeight};\n");
            sb.Append("  var engine = Matter.Engine.create();\n");
            sb.Append("  var world = engine.world;\n");
            sb.Append($"  var render = Matter.Render.create({{element:document.getElementById('{id}'),engine:engine,\n");
            sb.Append("    options:{width:width,height:height,wireframes:false,background:'#fafafa'}});\n");
            sb.Append("  // ─ user code ─\n");
            sb.Append(content);
            sb.Append("\n  // ─ end user code ─\n");
            sb.Append("  Matter.Render.run(render);\n");
            sb.Append("  var runner = Matter.Runner.create();\n");
            sb.Append("  Matter.Runner.run(runner, engine);\n");
            sb.Append("})();\n");
            sb.Append("</script>\n");
            sb.Append("</div>\n");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────
        // RENDER: Cannon-es — physics 3D (renderiza con Three.js)
        //   Acceso a `world` (CANNON.World), `scene`, `camera`, `renderer`,
        //   `THREE`, `CANNON`, `width`, `height`.
        //
        //   #cannon 700 500
        //     var groundBody = new CANNON.Body({type:CANNON.Body.STATIC,shape:new CANNON.Plane()});
        //     groundBody.quaternion.setFromAxisAngle(new CANNON.Vec3(1,0,0), -Math.PI/2);
        //     world.addBody(groundBody);
        //     var sphereBody = new CANNON.Body({mass:1,shape:new CANNON.Sphere(1)});
        //     sphereBody.position.set(0,5,0);
        //     world.addBody(sphereBody);
        //   #end cannon
        // ─────────────────────────────────────────────────────────────────
        private string RenderCannon(string content)
        {
            var id = $"cannon_{++_webGraphicCounter}";
            var sb = new StringBuilder(2048);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append($"<div id=\"{id}\" style=\"width:{_webGraphicWidth}px;height:{_webGraphicHeight}px;");
            sb.Append("border:1px solid #ccc;background:#fafafa\"></div>\n");
            // Cannon-es needs the THREE importmap too (same as #three)
            sb.Append(LoadLibrary(WebGraphicKind.Three));
            sb.Append(LoadLibrary(WebGraphicKind.Cannon));
            sb.Append("<script type=\"module\">\n");
            sb.Append("import * as THREE from 'three';\n");
            sb.Append("import * as CANNON from 'cannon-es';\n");
            sb.Append("import {OrbitControls} from 'three/addons/controls/OrbitControls.js';\n");
            sb.Append("(function(){\n");
            sb.Append($"  const container = document.getElementById('{id}');\n");
            sb.Append($"  const width = {_webGraphicWidth}, height = {_webGraphicHeight};\n");
            sb.Append("  const scene = new THREE.Scene();\n");
            sb.Append("  scene.background = new THREE.Color(0xfafafa);\n");
            sb.Append("  const camera = new THREE.PerspectiveCamera(60, width/height, 0.1, 1000);\n");
            sb.Append("  camera.position.set(8, 8, 8); camera.lookAt(0, 0, 0);\n");
            sb.Append("  const renderer = new THREE.WebGLRenderer({antialias:true});\n");
            sb.Append("  renderer.setSize(width, height);\n");
            sb.Append("  container.appendChild(renderer.domElement);\n");
            sb.Append("  const controls = new OrbitControls(camera, renderer.domElement);\n");
            sb.Append("  scene.add(new THREE.AmbientLight(0xffffff, 0.5));\n");
            sb.Append("  scene.add(new THREE.DirectionalLight(0xffffff, 0.8));\n");
            sb.Append("  const world = new CANNON.World({gravity: new CANNON.Vec3(0,-9.82,0)});\n");
            sb.Append("  // ─ user code ─\n");
            sb.Append(content);
            sb.Append("\n  // ─ end user code ─\n");
            sb.Append("  function animate(){requestAnimationFrame(animate);world.step(1/60);controls.update();renderer.render(scene,camera);}\n");
            sb.Append("  animate();\n");
            sb.Append("})();\n");
            sb.Append("</script>\n");
            sb.Append("</div>\n");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────
        // RENDER: GeoGebra — applet matemático interactivo
        //   El contenido es un objeto JS con la propiedad `commands` (array de
        //   strings con comandos GeoGebra) y otras opciones.
        //
        //   #geogebra 700 500
        //   {
        //     appName:'graphing',
        //     commands:['f(x) = sin(x)', 'g(x) = cos(x)']
        //   }
        //   #end geogebra
        // ─────────────────────────────────────────────────────────────────
        private string RenderGeogebra(string content)
        {
            var id = $"geogebra_{++_webGraphicCounter}";
            var sb = new StringBuilder(2048);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append($"<div id=\"{id}\" style=\"width:{_webGraphicWidth}px;height:{_webGraphicHeight}px\"></div>\n");
            sb.Append(LoadLibrary(WebGraphicKind.Geogebra));
            sb.Append("<script>\n");
            sb.Append("(function(){\n");
            sb.Append("  var spec = ").Append(content.Trim()).Append(";\n");
            sb.Append($"  var params = Object.assign({{appName:'graphing',width:{_webGraphicWidth},height:{_webGraphicHeight},showAlgebraInput:true,showToolBar:true,showMenuBar:false,language:'es',\n");
            sb.Append("    appletOnLoad:function(api){if(spec.commands){spec.commands.forEach(c=>api.evalCommand(c));}}}, spec);\n");
            sb.Append("  delete params.commands;\n");
            sb.Append("  var applet = new GGBApplet(params, true);\n");
            sb.Append($"  applet.inject('{id}');\n");
            sb.Append("})();\n");
            sb.Append("</script>\n");
            sb.Append("</div>\n");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────
        // RENDER: Chart.js 4 — gráficos simples
        //   El contenido es la config Chart.js (type, data, options).
        //
        //   #chart 600 400
        //   {
        //     type: 'line',
        //     data: {
        //       labels: ['Ene','Feb','Mar'],
        //       datasets: [{label:'Sales', data:[12,19,3], borderColor:'rgb(75,192,192)'}]
        //     },
        //     options: {responsive:true}
        //   }
        //   #end chart
        // ─────────────────────────────────────────────────────────────────
        private string RenderChart(string content)
        {
            var id = $"chart_{++_webGraphicCounter}";
            var sb = new StringBuilder(2048);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append($"<canvas id=\"{id}\" width=\"{_webGraphicWidth}\" height=\"{_webGraphicHeight}\" ");
            sb.Append("style=\"max-width:100%\"></canvas>\n");
            sb.Append(LoadLibrary(WebGraphicKind.Chart));
            sb.Append("<script>\n");
            sb.Append("(function(){\n");
            sb.Append($"  var ctx = document.getElementById('{id}');\n");
            sb.Append("  var config = ").Append(content.Trim()).Append(";\n");
            sb.Append("  new Chart(ctx, config);\n");
            sb.Append("})();\n");
            sb.Append("</script>\n");
            sb.Append("</div>\n");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────
        // RENDER: anime.js — animaciones generales
        //   Acceso a `anime`, `width`, `height`, `container` (el div parent).
        //   Crea elementos con SVG/HTML directamente o anima los del container.
        //
        //   #anime 600 200
        //     container.innerHTML = '<div class="box" style="width:50px;height:50px;background:#ffd966;position:absolute"></div>';
        //     anime({
        //       targets: container.querySelector('.box'),
        //       translateX: 250,
        //       rotate: 360,
        //       duration: 2000,
        //       loop: true,
        //       easing: 'easeInOutQuad'
        //     });
        //   #end anime
        // ─────────────────────────────────────────────────────────────────
        private string RenderAnime(string content)
        {
            var id = $"anime_{++_webGraphicCounter}";
            var sb = new StringBuilder(2048);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append($"<div id=\"{id}\" style=\"width:{_webGraphicWidth}px;height:{_webGraphicHeight}px;");
            sb.Append("border:1px solid #ccc;background:#fafafa;position:relative;overflow:hidden\"></div>\n");
            sb.Append(LoadLibrary(WebGraphicKind.Anime));
            sb.Append("<script>\n");
            sb.Append("(function(){\n");
            sb.Append($"  var container = document.getElementById('{id}');\n");
            sb.Append($"  var width = {_webGraphicWidth}, height = {_webGraphicHeight};\n");
            sb.Append("  // ─ user code ─\n");
            sb.Append(content);
            sb.Append("\n  // ─ end user code ─\n");
            sb.Append("})();\n");
            sb.Append("</script>\n");
            sb.Append("</div>\n");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────
        // RENDER: Manim-style — animaciones matemáticas (built on MathBox)
        //   Manim como tal (Python+Cairo) no puede correr en browser. En su
        //   lugar usamos MathBox que cubre el mismo dominio (math viz 3D
        //   animado) con API JavaScript. El user escribe DSL MathBox y lo
        //   tratamos como una animación matemática estilo 3blue1brown.
        //
        //   #manim 700 500
        //     mathbox.set('focus', 3);
        //     var view = mathbox.cartesian({range:[[-3,3],[-2,2],[-2,2]], scale:[1,1,1]});
        //     view.axis({axis:1}); view.axis({axis:2}); view.axis({axis:3});
        //     view.area({width:64, height:64, axes:[1,2],
        //       expr:function(emit,x,y,i,j){
        //         var t = mathbox.three.clock.getElapsedTime();
        //         emit(x, y, Math.sin(x + t)*Math.cos(y));
        //       },
        //       channels:3, live:true});
        //     view.surface({shaded:true, color:0x3090ff});
        //   #end manim
        // ─────────────────────────────────────────────────────────────────
        private string RenderManim(string content)
        {
            var id = $"manim_{++_webGraphicCounter}";
            var sb = new StringBuilder(2048);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append($"<div id=\"{id}\" style=\"width:{_webGraphicWidth}px;height:{_webGraphicHeight}px;");
            sb.Append("border:1px solid #222;background:#000\"></div>\n");
            sb.Append(LoadLibrary(WebGraphicKind.Manim));
            sb.Append("<script>\n");
            sb.Append("(function(){\n");
            sb.Append($"  var container = document.getElementById('{id}');\n");
            sb.Append("  var mathbox = MathBox.mathBox({\n");
            sb.Append("    plugins: ['core', 'controls', 'cursor'],\n");
            sb.Append("    controls: { klass: THREE.OrbitControls },\n");
            sb.Append("    element: container,\n");
            sb.Append($"   camera: {{ near: 0.1, far: 1000 }},\n");
            sb.Append($"   size: {{ width: {_webGraphicWidth}, height: {_webGraphicHeight} }}\n");
            sb.Append("  });\n");
            sb.Append("  var three = mathbox.three || mathbox;\n");
            sb.Append("  if (three.camera) three.camera.position.set(2.5, 2.5, 2.5);\n");
            sb.Append("  // estilo manim: fondo negro, math en colores brillantes\n");
            sb.Append("  if (three.renderer) three.renderer.setClearColor(new THREE.Color(0x000000), 1.0);\n");
            sb.Append("  // ─ user code ─\n");
            sb.Append(content);
            sb.Append("\n  // ─ end user code ─\n");
            sb.Append("})();\n");
            sb.Append("</script>\n");
            sb.Append("</div>\n");
            return sb.ToString();
        }
    }
}
