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
        internal enum WebGraphicKind { Plotly, Three, Mermaid, Canvas }

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

                _ => ""
            };
        }
    }
}
