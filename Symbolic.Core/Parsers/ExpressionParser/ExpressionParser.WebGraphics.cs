// ─────────────────────────────────────────────────────────────────────────
// Web graphics block directives — Phase 1 (8 libraries beyond #plotly/#svg)
//
//   #three   ... #end three     — Three.js 3D
//   #mermaid ... #end mermaid   — Mermaid flowcharts/sequence/gantt
//   #canvas  ... #end canvas    — HTML5 Canvas 2D
//   #cyto    ... #end cyto      — Cytoscape graphs
//   #dot     ... #end dot       — Graphviz via viz.js
//   #jsx     ... #end jsx       — JSXGraph interactive geometry
//   #map     ... #end map       — Leaflet maps
//   #math    ... #end math      — KaTeX LaTeX rendering
//   #chart   ... #end chart     — Chart.js (simple charts)
//
// Body content is captured raw (never tokenised, never evaluated). The end
// directive emits a <div> container, the library's CDN <script src=...>
// (once per document), and a small init wrapper that runs the user's code.
// ─────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using System.Text;

namespace Calcpad.Core
{
    public partial class ExpressionParser
    {
        internal enum WebGraphicKind
        {
            Three, Mermaid, Canvas, Cyto, Dot, Jsx, Map, Math, Chart
        }

        // Active block state
        internal bool _insideWebGraphicBlock;
        internal WebGraphicKind _webGraphicKind;
        internal int _webGraphicWidth;
        internal int _webGraphicHeight;
        internal StringBuilder _webGraphicBuffer;
        internal bool _webGraphicSavedVisible;
        internal int _webGraphicSbPositionBeforeLine = -1;

        // Track which CDN scripts have been emitted in the current document so
        // we don't repeat the <script src=...> tag.
        private readonly HashSet<WebGraphicKind> _webGraphicLibsLoaded = new();
        private static int _webGraphicCounter = 0;

        private void ParseKeywordWebGraphic(System.ReadOnlySpan<char> s, WebGraphicKind kind)
        {
            // Sintaxis: "#name", "#name 600 400", default 700×400 (Three: 600×500)
            var text = s.ToString().Trim();
            var parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            _webGraphicWidth = 700;
            _webGraphicHeight = kind == WebGraphicKind.Three ? 500 : 400;
            if (parts.Length >= 2 && int.TryParse(parts[1], out var w) && w > 0) _webGraphicWidth = w;
            if (parts.Length >= 3 && int.TryParse(parts[2], out var h) && h > 0) _webGraphicHeight = h;

            _insideWebGraphicBlock = true;
            _webGraphicKind = kind;
            _webGraphicBuffer = new StringBuilder(2048);
            _webGraphicSavedVisible = _isVisible;
            _webGraphicSbPositionBeforeLine = -1;
        }

        /// <summary>Capture one body line into the active web-graphic buffer
        /// (called from the main Parse loop for both cached and non-cached lines).</summary>
        internal void ProcessWebGraphicLine(string line)
        {
            if (_webGraphicBuffer is null || !_webGraphicSavedVisible) return;
            _webGraphicBuffer.AppendLine(line);
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
                var content = _webGraphicBuffer.ToString().Trim();
                var html = kind switch
                {
                    WebGraphicKind.Three => RenderThree(content),
                    WebGraphicKind.Mermaid => RenderMermaid(content),
                    WebGraphicKind.Canvas => RenderCanvas(content),
                    WebGraphicKind.Cyto => RenderCyto(content),
                    WebGraphicKind.Dot => RenderDot(content),
                    WebGraphicKind.Jsx => RenderJsx(content),
                    WebGraphicKind.Map => RenderMap(content),
                    WebGraphicKind.Math => RenderMath(content),
                    WebGraphicKind.Chart => RenderChart(content),
                    _ => string.Empty
                };
                _sb.Append(html);
            }
            _webGraphicBuffer = null;
        }

        /// <summary>Emit the library's CDN &lt;script&gt; tag, but only once per
        /// document. Subsequent #plotly/#three/etc. blocks reuse the loaded lib.</summary>
        private string LoadLibrary(WebGraphicKind kind)
        {
            if (_webGraphicLibsLoaded.Contains(kind)) return string.Empty;
            _webGraphicLibsLoaded.Add(kind);
            return kind switch
            {
                WebGraphicKind.Three =>
                    "<script type=\"importmap\">{\"imports\":{\"three\":\"https://unpkg.com/three@0.160.0/build/three.module.js\"," +
                    "\"three/addons/\":\"https://unpkg.com/three@0.160.0/examples/jsm/\"}}</script>\n",
                WebGraphicKind.Mermaid =>
                    "<script type=\"module\">" +
                    "import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.esm.min.mjs';" +
                    "mermaid.initialize({startOnLoad:true});" +
                    "window.addEventListener('load',()=>mermaid.run({querySelector:'.mermaid'}));" +
                    "</script>\n",
                WebGraphicKind.Cyto =>
                    "<script src=\"https://cdn.jsdelivr.net/npm/cytoscape@3.28.1/dist/cytoscape.min.js\"></script>\n",
                WebGraphicKind.Dot =>
                    "<script src=\"https://cdn.jsdelivr.net/npm/viz.js@2.1.2/viz.js\"></script>" +
                    "<script src=\"https://cdn.jsdelivr.net/npm/viz.js@2.1.2/full.render.js\"></script>\n",
                WebGraphicKind.Jsx =>
                    "<link rel=\"stylesheet\" href=\"https://cdn.jsdelivr.net/npm/jsxgraph@1.10/distrib/jsxgraph.css\"/>" +
                    "<script src=\"https://cdn.jsdelivr.net/npm/jsxgraph@1.10/distrib/jsxgraphcore.min.js\"></script>\n",
                WebGraphicKind.Map =>
                    "<link rel=\"stylesheet\" href=\"https://unpkg.com/leaflet@1.9.4/dist/leaflet.css\"/>" +
                    "<script src=\"https://unpkg.com/leaflet@1.9.4/dist/leaflet.js\"></script>\n",
                WebGraphicKind.Math =>
                    "<link rel=\"stylesheet\" href=\"https://cdn.jsdelivr.net/npm/katex@0.16.9/dist/katex.min.css\"/>" +
                    "<script src=\"https://cdn.jsdelivr.net/npm/katex@0.16.9/dist/katex.min.js\"></script>\n",
                WebGraphicKind.Chart =>
                    "<script src=\"https://cdn.jsdelivr.net/npm/chart.js@4.4.0\"></script>\n",
                _ => string.Empty
            };
        }

        // ── Three.js ──────────────────────────────────────────────────────
        // Body is JS that has access to: scene, camera, renderer, THREE,
        // OrbitControls, container, width, height (already declared by wrapper).
        private string RenderThree(string content)
        {
            var id = $"three_{++_webGraphicCounter}";
            var sb = new StringBuilder(2048);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append(LoadLibrary(WebGraphicKind.Three));
            sb.Append($"<div id=\"{id}\" style=\"width:{_webGraphicWidth}px;height:{_webGraphicHeight}px;border:1px solid #ccc;background:#fafafa;margin:auto\"></div>\n");
            sb.Append("<script type=\"module\">\n");
            sb.Append("import * as THREE from 'three';\n");
            sb.Append("import {OrbitControls} from 'three/addons/controls/OrbitControls.js';\n");
            sb.Append("(function(){\n");
            sb.Append($"  const container = document.getElementById('{id}');\n");
            sb.Append($"  const width = {_webGraphicWidth};\n");
            sb.Append($"  const height = {_webGraphicHeight};\n");
            sb.Append("  const scene = new THREE.Scene();\n");
            sb.Append("  scene.background = new THREE.Color(0xfafafa);\n");
            sb.Append("  const camera = new THREE.PerspectiveCamera(60, width/height, 0.1, 1000);\n");
            sb.Append("  camera.position.set(5,5,5); camera.lookAt(0,0,0);\n");
            sb.Append("  const renderer = new THREE.WebGLRenderer({antialias:true});\n");
            sb.Append("  renderer.setSize(width, height); container.appendChild(renderer.domElement);\n");
            sb.Append("  const controls = new OrbitControls(camera, renderer.domElement);\n");
            sb.Append("  scene.add(new THREE.AmbientLight(0xffffff, 0.5));\n");
            sb.Append("  const dl = new THREE.DirectionalLight(0xffffff, 0.8); dl.position.set(5,10,7); scene.add(dl);\n");
            sb.Append("  // ─ user code ─\n");
            sb.Append(content).Append('\n');
            sb.Append("  // ─ end user code ─\n");
            sb.Append("  function animate(){requestAnimationFrame(animate);controls.update();renderer.render(scene,camera);}\n");
            sb.Append("  animate();\n");
            sb.Append("})();\n");
            sb.Append("</script>\n</div>\n");
            return sb.ToString();
        }

        // ── Mermaid ───────────────────────────────────────────────────────
        // Body is Mermaid DSL (flowchart, sequence, gantt, classDiagram, …)
        private string RenderMermaid(string content)
        {
            var id = $"mermaid_{++_webGraphicCounter}";
            var sb = new StringBuilder(1024);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append($"<div id=\"{id}\" class=\"mermaid\" style=\"max-width:{_webGraphicWidth}px;margin:auto;text-align:center\">\n");
            sb.Append(content).Append('\n');
            sb.Append("</div>\n");
            sb.Append(LoadLibrary(WebGraphicKind.Mermaid));
            sb.Append("</div>\n");
            return sb.ToString();
        }

        // ── HTML5 Canvas ──────────────────────────────────────────────────
        // Body is JS with access to: canvas, ctx (Canvas2DContext), width, height
        private string RenderCanvas(string content)
        {
            var id = $"canvas_{++_webGraphicCounter}";
            var sb = new StringBuilder(1024);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append($"<canvas id=\"{id}\" width=\"{_webGraphicWidth}\" height=\"{_webGraphicHeight}\" style=\"border:1px solid #ccc;background:#fafafa;display:block;margin:auto\"></canvas>\n");
            sb.Append("<script>\n(function(){\n");
            sb.Append($"  const canvas = document.getElementById('{id}');\n");
            sb.Append("  const ctx = canvas.getContext('2d');\n");
            sb.Append($"  const width = {_webGraphicWidth};\n");
            sb.Append($"  const height = {_webGraphicHeight};\n");
            sb.Append(content).Append('\n');
            sb.Append("})();\n</script>\n</div>\n");
            return sb.ToString();
        }

        // ── Cytoscape ─────────────────────────────────────────────────────
        // Body is JS that defines `elements` and optionally `style`/`layout`.
        private string RenderCyto(string content)
        {
            var id = $"cyto_{++_webGraphicCounter}";
            var sb = new StringBuilder(1024);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append(LoadLibrary(WebGraphicKind.Cyto));
            sb.Append($"<div id=\"{id}\" style=\"width:{_webGraphicWidth}px;height:{_webGraphicHeight}px;border:1px solid #ccc;margin:auto\"></div>\n");
            sb.Append("<script>(function(){\n");
            sb.Append("  function init(){\n");
            sb.Append("    if (typeof cytoscape === 'undefined'){setTimeout(init,200);return;}\n");
            sb.Append($"    const container = document.getElementById('{id}');\n");
            sb.Append("    let elements=[], style=undefined, layout={name:'cose'};\n");
            sb.Append(content).Append('\n');
            sb.Append("    cytoscape({container, elements, style, layout});\n");
            sb.Append("  } init();\n})();</script>\n</div>\n");
            return sb.ToString();
        }

        // ── Graphviz DOT ──────────────────────────────────────────────────
        // Body is DOT graph source (digraph G { a -> b; ... }).
        private string RenderDot(string content)
        {
            var id = $"dot_{++_webGraphicCounter}";
            var sb = new StringBuilder(1024);
            // DOT body must be embedded as JS string literal — escape backslashes/quotes/newlines.
            var escaped = content.Replace("\\", "\\\\").Replace("`", "\\`").Replace("$", "\\$");
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append(LoadLibrary(WebGraphicKind.Dot));
            sb.Append($"<div id=\"{id}\" style=\"max-width:{_webGraphicWidth}px;margin:auto;text-align:center\"></div>\n");
            sb.Append("<script>(function(){\n");
            sb.Append("  function init(){\n");
            sb.Append("    if (typeof Viz === 'undefined'){setTimeout(init,200);return;}\n");
            sb.Append("    const viz = new Viz();\n");
            sb.Append("    viz.renderSVGElement(`").Append(escaped).Append("`)\n");
            sb.Append($"      .then(svg => document.getElementById('{id}').appendChild(svg))\n");
            sb.Append($"      .catch(e => document.getElementById('{id}').textContent = 'DOT error: ' + e.message);\n");
            sb.Append("  } init();\n})();</script>\n</div>\n");
            return sb.ToString();
        }

        // ── JSXGraph ──────────────────────────────────────────────────────
        // Body is JS with access to `board` (a JSXGraph board centered at origin).
        private string RenderJsx(string content)
        {
            var id = $"jsx_{++_webGraphicCounter}";
            var sb = new StringBuilder(1024);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append(LoadLibrary(WebGraphicKind.Jsx));
            sb.Append($"<div id=\"{id}\" class=\"jxgbox\" style=\"width:{_webGraphicWidth}px;height:{_webGraphicHeight}px;margin:auto\"></div>\n");
            sb.Append("<script>(function(){\n");
            sb.Append("  function init(){\n");
            sb.Append("    if (typeof JXG === 'undefined'){setTimeout(init,200);return;}\n");
            sb.Append($"    const board = JXG.JSXGraph.initBoard('{id}', {{boundingbox:[-5,5,5,-5], axis:true, showCopyright:false, showNavigation:true}});\n");
            sb.Append(content).Append('\n');
            sb.Append("  } init();\n})();</script>\n</div>\n");
            return sb.ToString();
        }

        // ── Leaflet maps ──────────────────────────────────────────────────
        // Body is JS with access to `map` (a Leaflet map centered at 0,0 zoom 2).
        private string RenderMap(string content)
        {
            var id = $"map_{++_webGraphicCounter}";
            var sb = new StringBuilder(1024);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append(LoadLibrary(WebGraphicKind.Map));
            sb.Append($"<div id=\"{id}\" style=\"width:{_webGraphicWidth}px;height:{_webGraphicHeight}px;margin:auto\"></div>\n");
            sb.Append("<script>(function(){\n");
            sb.Append("  function init(){\n");
            sb.Append("    if (typeof L === 'undefined'){setTimeout(init,200);return;}\n");
            sb.Append($"    const map = L.map('{id}').setView([0,0], 2);\n");
            sb.Append("    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {attribution:'© OpenStreetMap'}).addTo(map);\n");
            sb.Append(content).Append('\n');
            sb.Append("  } init();\n})();</script>\n</div>\n");
            return sb.ToString();
        }

        // ── KaTeX (LaTeX rendering) ───────────────────────────────────────
        // Body is plain LaTeX source. Renders inside a centered <div>.
        private string RenderMath(string content)
        {
            var id = $"math_{++_webGraphicCounter}";
            var sb = new StringBuilder(1024);
            var escaped = content.Replace("\\", "\\\\").Replace("`", "\\`").Replace("$", "\\$");
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append(LoadLibrary(WebGraphicKind.Math));
            sb.Append($"<div id=\"{id}\" style=\"text-align:center;margin:1em auto;max-width:{_webGraphicWidth}px;font-size:1.1em\"></div>\n");
            sb.Append("<script>(function(){\n");
            sb.Append("  function init(){\n");
            sb.Append("    if (typeof katex === 'undefined'){setTimeout(init,200);return;}\n");
            sb.Append("    katex.render(`").Append(escaped).Append($"`, document.getElementById('{id}'), {{throwOnError:false, displayMode:true}});\n");
            sb.Append("  } init();\n})();</script>\n</div>\n");
            return sb.ToString();
        }

        // ── Chart.js ──────────────────────────────────────────────────────
        // Body is a JS object literal: { type: 'bar', data: {...}, options: {...} }
        private string RenderChart(string content)
        {
            var id = $"chart_{++_webGraphicCounter}";
            var sb = new StringBuilder(1024);
            sb.Append("<div").Append(HtmlId).Append(">\n");
            sb.Append(LoadLibrary(WebGraphicKind.Chart));
            sb.Append($"<div style=\"width:{_webGraphicWidth}px;height:{_webGraphicHeight}px;margin:auto\"><canvas id=\"{id}\"></canvas></div>\n");
            sb.Append("<script>(function(){\n");
            sb.Append("  function init(){\n");
            sb.Append("    if (typeof Chart === 'undefined'){setTimeout(init,200);return;}\n");
            sb.Append("    const config = ").Append(content).Append(";\n");
            sb.Append($"    new Chart(document.getElementById('{id}'), config);\n");
            sb.Append("  } init();\n})();</script>\n</div>\n");
            return sb.ToString();
        }

        /// <summary>Reset Web Graphics state at the start of each parse so the
        /// "loaded libs" tracking doesn't bleed across documents.</summary>
        private void ResetWebGraphicsState()
        {
            _webGraphicLibsLoaded.Clear();
            _insideWebGraphicBlock = false;
            _webGraphicBuffer = null;
            _webGraphicSbPositionBeforeLine = -1;
        }
    }
}
