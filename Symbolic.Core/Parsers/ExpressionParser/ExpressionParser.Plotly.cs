// ─────────────────────────────────────────────────────────────────────────
// #plotly … #end plotly directive — embeds a Plotly.js chart from a JSON
// object literal. The user writes pure Plotly spec (no HTML, no script
// tags); the parser injects the <div>, the CDN <script src=...>, and a
// small wrapper that calls Plotly.newPlot(id, spec.data, spec.layout).
//
// Syntax:
//   #plotly                 → defaults: 700 × 400
//   #plotly 800 500         → custom width × height (pixels)
//   { data: [...], layout: {...} }
//   #end plotly
//
// Inside the block, EVERY line is treated as a literal raw text — no
// expression evaluation, no markdown, no whitespace stripping. The body
// gets concatenated as-is and dropped into a JS object literal via
// `var spec = <body>;`.
// ─────────────────────────────────────────────────────────────────────────

using System.Text;

namespace Calcpad.Core
{
    public partial class ExpressionParser
    {
        // State for an in-flight #plotly block
        internal bool _insidePlotlyBlock;
        internal int _plotlyWidth;
        internal int _plotlyHeight;
        internal StringBuilder _plotlyBuffer;
        internal bool _plotlySavedVisible;
        internal int _plotlySbPositionBeforeLine = -1;
        // Counter for unique <div id="plotly_N"> across the whole document
        private static int _plotlyCounter = 0;
        // Inject the CDN <script src=...> only once per HtmlResult
        private bool _plotlyCdnEmitted = false;

        private void ParseKeywordPlotly(System.ReadOnlySpan<char> s)
        {
            // "#plotly" / "#plotly 800 400" — strip the directive name then
            // parse up-to-two integer args as width / height.
            var text = s.ToString().Trim();
            var parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            _plotlyWidth = 700;
            _plotlyHeight = 400;
            if (parts.Length >= 2 && int.TryParse(parts[1], out var w) && w > 0)
                _plotlyWidth = w;
            if (parts.Length >= 3 && int.TryParse(parts[2], out var h) && h > 0)
                _plotlyHeight = h;

            _insidePlotlyBlock = true;
            _plotlyBuffer = new StringBuilder(2048);
            _plotlySavedVisible = _isVisible;
            _plotlySbPositionBeforeLine = -1;
        }

        /// <summary>Capture one body line into the active #plotly buffer.</summary>
        internal void ProcessPlotlyLine(string line)
        {
            if (_plotlyBuffer is null || !_plotlySavedVisible) return;
            _plotlyBuffer.AppendLine(line);
        }

        private void ParseKeywordEndPlotly()
        {
            if (!_insidePlotlyBlock || _plotlyBuffer is null)
            {
                AppendError("#end plotly", "No matching #plotly", _currentLine);
                return;
            }
            _insidePlotlyBlock = false;
            _plotlySbPositionBeforeLine = -1;

            if (_plotlySavedVisible)
            {
                var content = _plotlyBuffer.ToString().Trim();
                var id = $"plotly_{++_plotlyCounter}";
                var sb = new StringBuilder(2048);
                sb.Append("<div").Append(HtmlId).Append(">\n");
                // Inject the Plotly CDN script ONCE per document. Multiple
                // <script src=...> tags for the same CDN URL are harmless
                // but we keep the HTML compact.
                if (!_plotlyCdnEmitted)
                {
                    sb.Append("<script src=\"https://cdn.plot.ly/plotly-2.26.0.min.js\" charset=\"utf-8\"></script>\n");
                    _plotlyCdnEmitted = true;
                }
                sb.Append($"<div id=\"{id}\" style=\"width:{_plotlyWidth}px;height:{_plotlyHeight}px;margin:auto\"></div>\n");
                sb.Append("<script>\n");
                sb.Append("(function(){\n");
                sb.Append("  function init(){\n");
                sb.Append("    if (typeof Plotly === \"undefined\") { setTimeout(init, 200); return; }\n");
                sb.Append("    var spec = ").Append(content).Append(";\n");
                sb.Append("    var data = spec.data || [];\n");
                sb.Append("    var layout = spec.layout || {};\n");
                sb.Append($"    Plotly.newPlot(\"{id}\", data, layout, {{responsive:true}});\n");
                sb.Append("  }\n");
                sb.Append("  init();\n");
                sb.Append("})();\n");
                sb.Append("</script>\n");
                sb.Append("</div>\n");
                _sb.Append(sb.ToString());
            }
            _plotlyBuffer = null;
        }

        /// <summary>Reset Plotly-block state at the start of each parse so we
        /// don't leak _plotlyCdnEmitted across documents.</summary>
        private void ResetPlotlyState()
        {
            _plotlyCdnEmitted = false;
            _insidePlotlyBlock = false;
            _plotlyBuffer = null;
            _plotlySbPositionBeforeLine = -1;
        }
    }
}
