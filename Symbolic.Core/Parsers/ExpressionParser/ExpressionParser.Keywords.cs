
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Calcpad.Core
{
    public partial class ExpressionParser
    {
        private enum Keyword
        {
            None,
            Hide,
            Show,
            Pre,
            Post,
            Val,
            Equ,
            Noc,
            NoSub,
            NoVar,
            VarSub,
            Const,
            Split,
            Wrap,
            Deg,
            Rad,
            Gra,
            Round,
            Format,
            If,
            Else_If,
            Else,
            End_If,
            While,
            For,
            Repeat,
            Loop,
            Break,
            Continue,
            Local,
            Global,
            Pause,
            Input,
            Md,
            Read,
            Write,
            Append,
            Phasor,
            Complex,
            Function,
            End_Function,
            Deq,
            End_Deq,
            Inl,
            Blk,
            End_Blk,
            Cen,
            End_Cen,
            Pgb,
            Margen,
            End_Margen,
            Sym,
            End_Sym,
            Python,
            End_Python,
            Maxima,
            End_Maxima,
            Pip,
            SkipLine,
            Svg,
            End_Svg,
            Plotly,
            End_Plotly,
            // Web graphics phase 1 — visualisation libraries (CDN-loaded)
            Three,        // Three.js 3D
            End_Three,
            Mermaid,      // Flowcharts / sequence / gantt
            End_Mermaid,
            Canvas,       // HTML5 Canvas 2D
            End_Canvas,
            Cyto,         // Cytoscape (graphs / networks)
            End_Cyto,
            Dot,          // Graphviz via viz.js
            End_Dot,
            Jsx,          // JSXGraph (interactive geometry)
            End_Jsx,
            Map,          // Leaflet (maps)
            End_Map,
            // Mathbox MUST come before Math (it's a longer prefix; otherwise
            // "#mathbox" matches "#math" first because GetKeyword iterates the
            // 'm' bucket in declaration order and StartsWith returns true for
            // the shorter keyword).
            Mathbox,      // MathBox 2 — math viz 3D, isosurfaces, vector fields
            End_Mathbox,
            Math,         // KaTeX (LaTeX rendering)
            End_Math,
            Chart,        // Chart.js (simple charts)
            End_Chart,
            // Web graphics phase 3 — advanced viz (9)
            D3,           // D3.js v7 — custom data-driven plots
            End_D3,
            Echarts,      // Apache ECharts 5 — sankey, parallel, heatmap, treemap
            End_Echarts,
            Vega,         // Vega-Lite 5 — declarative JSON charts
            End_Vega,
            Visnet,       // vis-network 9 — networks dinámicos
            End_Visnet,
            P5,           // p5.js 1 — creative coding
            End_P5,
            Matter,       // Matter.js — physics 2D rígidos
            End_Matter,
            Cannon,       // Cannon-es — physics 3D rígidos
            End_Cannon,
            Geogebra,     // GeoGebra applet
            End_Geogebra,
            // Web graphics phase 4 — animations (2)
            Anime,        // anime.js v3 — DOM/SVG animations
            End_Anime,
            Manim         // animaciones tipo 3blue1brown via MathBox dark theme
            ,
            End_Manim
        }
        private enum KeywordResult  
        {
            None,
            Continue,
            Break
        }

        private Keyword _previousKeyword = Keyword.None;
        private bool _insideBlk = false;
        private bool _insideDeqBlock = false;
        private bool _insideCenBlock = false;
        private static string[] KeywordNames;
        private static Keyword[] KeywordValues;
        private static List<int>[] KeywordIndex;
        private static int MaxKeywordLength;

        private static void InitKeyWordStrings()
        {
            var n = 'z' - 'a';
            KeywordNames = Enum.GetNames<Keyword>().Skip(1).ToArray();
            MaxKeywordLength = KeywordNames.Max(s => s.Length);
            KeywordValues = Enum.GetValues<Keyword>().Skip(1).ToArray();
            KeywordIndex = new List<int>[n];
            for (int i = 0, len = KeywordNames.Length; i < len; ++i)
            {
                var lower = KeywordNames[i].ToLowerInvariant().Replace('_', ' ');
                KeywordNames[i] = lower;
                var j = lower[0] - 'a';
                if (KeywordIndex[j] is null)
                    KeywordIndex[j] = [i];
                else
                    KeywordIndex[j].Add(i);
            }
            // Sort each bucket by keyword length DESCENDING so that longer
            // keywords match first when they share a common prefix.
            // E.g. "#mathbox" must match Mathbox (7), not Math (4).
            for (int j = 0; j < n; j++)
                if (KeywordIndex[j] is not null)
                    KeywordIndex[j].Sort((a, b) =>
                        KeywordNames[b].Length.CompareTo(KeywordNames[a].Length));
        }

        private static Keyword GetKeyword(ReadOnlySpan<char> s)
        {
            var n = Math.Min(MaxKeywordLength, s.Length - 1);
            if (n < 3)
                return Keyword.None;

            var i = char.ToLowerInvariant(s[1]) - 'a';
            if (i < 0 || i >= KeywordNames.Length)
                return Keyword.None;

            var ind = KeywordIndex[i];
            if (ind is null)
                return Keyword.None;

            Span<char> lower = stackalloc char[n];
            s.Slice(1, n).ToLowerInvariant(lower);
            for (int j = 0; j < ind.Count; ++j)
            {
                var k = ind[j];
                if (lower.StartsWith(KeywordNames[k]))
                    return KeywordValues[k];
            }
            return Keyword.None;
        }

        KeywordResult ParseKeyword(ReadOnlySpan<char> s, ref Keyword keyword)
        {
            if (_isPausedByUser)
                keyword = Keyword.Pause;
            else if (s[0] == '#' && keyword == Keyword.None)
                keyword = GetKeyword(s);

            // Markdown-style headings: "# Title", "## Subtitle", ... "###### h6"
            // Activates automatically when a '#'-prefixed line has no matching
            // keyword and follows the markdown pattern (1–6 hashes + space + text).
            // Lets users write natural Markdown headings without the ugly "'# "
            // text-prefix workaround.
            if (keyword == Keyword.None && s.Length > 2 && s[0] == '#')
            {
                int hashCount = 0;
                while (hashCount < s.Length && hashCount < 6 && s[hashCount] == '#')
                    hashCount++;
                if (hashCount > 0 && hashCount < s.Length && s[hashCount] == ' ')
                {
                    var titleText = s[(hashCount + 1)..].Trim().ToString();
                    if (_isVisible && !string.IsNullOrEmpty(titleText))
                    {
                        // HTML-encode text content (preserves < > & but as safe text).
                        // If the user wants inline HTML in the title, they can still
                        // use the classic "Title syntax. Here we treat as plain text.
                        var safe = System.Web.HttpUtility.HtmlEncode(titleText);
                        _sb.Append($"<h{hashCount}{HtmlId}{HtmlLineClass}>{safe}</h{hashCount}>\n");
                    }
                    return KeywordResult.Continue;
                }
            }

            if (keyword == Keyword.None)
                return KeywordResult.None;

            switch (keyword)
            {
                case Keyword.Hide:
                    _isVisible = false;
                    break;
                case Keyword.Show:
                    _isVisible = true;
                    break;
                case Keyword.Pre:
                    _isVisible = !_calculate;
                    break;
                case Keyword.Post:
                    _isVisible = _calculate;
                    break;
                case Keyword.Input:
                    return ParseKeywordInput();
                case Keyword.Pause:
                    return ParseKeywordPause();
                case Keyword.Val:
                    _isVal = 1;
                    break;
                case Keyword.Equ:
                    _isVal = 0;
                    break;
                case Keyword.Noc:
                    _isVal = -1;
                    break;
                case Keyword.Deq:
                    // If #deq has content after it → inline mode
                    // If #deq alone on line → start block mode
                    var deqContent = s.Length > 4 ? s[4..].Trim() : ReadOnlySpan<char>.Empty;
                    if (deqContent.IsEmpty || deqContent.IsWhiteSpace())
                        _insideDeqBlock = true;
                    else
                        ParseKeywordDeq(s);
                    return KeywordResult.Continue;
                case Keyword.End_Deq:
                    _insideDeqBlock = false;
                    return KeywordResult.Continue;
                case Keyword.Inl:
                    ParseKeywordColumns(s, false);
                    return KeywordResult.Continue;
                case Keyword.Blk:
                    ParseKeywordBlkStart();
                    return KeywordResult.Continue;
                case Keyword.End_Blk:
                    ParseKeywordBlkEnd();
                    return KeywordResult.Continue;
                case Keyword.Cen:
                    // Inline: #cen expr → centered single line
                    // Block: #cen alone → start block
                    var cenContent = s.Length > 4 ? s[4..].Trim() : ReadOnlySpan<char>.Empty;
                    if (cenContent.IsEmpty || cenContent.IsWhiteSpace())
                    {
                        _insideCenBlock = true;
                        _sb.Append("<div class=\"cen-blk\">\n");
                    }
                    else
                    {
                        // Inline center: parse the expression and wrap in centered div
                        ParseKeywordCenInline(s);
                    }
                    return KeywordResult.Continue;
                case Keyword.End_Cen:
                    _insideCenBlock = false;
                    _sb.Append("</div>\n");
                    return KeywordResult.Continue;
                case Keyword.Pgb:
                    _sb.Append($"<div class=\"pgb\"></div>\n");
                    return KeywordResult.Continue;
                case Keyword.Margen:
                    // #margen 20 → 20mm each side. Default 15mm.
                    var margenVal = "15";
                    var margenContent = s.Length > 7 ? s[7..].Trim() : ReadOnlySpan<char>.Empty;
                    if (!margenContent.IsEmpty && !margenContent.IsWhiteSpace())
                        margenVal = margenContent.ToString().Trim();
                    _sb.Append($"<div style=\"margin:0 auto;padding:5mm {margenVal}mm;max-width:calc(190mm - {margenVal}mm - {margenVal}mm);text-align:justify;line-height:160%\">\n");
                    return KeywordResult.Continue;
                case Keyword.End_Margen:
                    _sb.Append("</div>\n");
                    return KeywordResult.Continue;
                case Keyword.Sym:
                    ParseKeywordSym(s);
                    return KeywordResult.Continue;
                case Keyword.End_Sym:
                    _insideSymBlock = false;
                    return KeywordResult.Continue;
                case Keyword.Python:
                    ParseKeywordPython(s);
                    return KeywordResult.Continue;
                case Keyword.End_Python:
                    ParseKeywordEndPython();
                    return KeywordResult.Continue;
                case Keyword.Maxima:
                    ParseKeywordMaxima(s);
                    return KeywordResult.Continue;
                case Keyword.End_Maxima:
                    ParseKeywordEndMaxima();
                    return KeywordResult.Continue;
                case Keyword.Pip:
                    ParseKeywordPip(s);
                    return KeywordResult.Continue;
                case Keyword.Svg:
                    ParseKeywordSvg(s);
                    return KeywordResult.Continue;
                case Keyword.End_Svg:
                    ParseKeywordEndSvg();
                    return KeywordResult.Continue;
                case Keyword.Plotly:
                    ParseKeywordPlotly(s);
                    return KeywordResult.Continue;
                case Keyword.End_Plotly:
                    ParseKeywordEndPlotly();
                    return KeywordResult.Continue;
                case Keyword.Three:
                    ParseKeywordWebGraphic(s, WebGraphicKind.Three);
                    return KeywordResult.Continue;
                case Keyword.End_Three:
                    ParseKeywordEndWebGraphic(WebGraphicKind.Three);
                    return KeywordResult.Continue;
                case Keyword.Mermaid:
                    ParseKeywordWebGraphic(s, WebGraphicKind.Mermaid);
                    return KeywordResult.Continue;
                case Keyword.End_Mermaid:
                    ParseKeywordEndWebGraphic(WebGraphicKind.Mermaid);
                    return KeywordResult.Continue;
                case Keyword.Canvas:
                    ParseKeywordWebGraphic(s, WebGraphicKind.Canvas);
                    return KeywordResult.Continue;
                case Keyword.End_Canvas:
                    ParseKeywordEndWebGraphic(WebGraphicKind.Canvas);
                    return KeywordResult.Continue;
                case Keyword.Cyto:
                    ParseKeywordWebGraphic(s, WebGraphicKind.Cyto);
                    return KeywordResult.Continue;
                case Keyword.End_Cyto:
                    ParseKeywordEndWebGraphic(WebGraphicKind.Cyto);
                    return KeywordResult.Continue;
                case Keyword.Dot:
                    ParseKeywordWebGraphic(s, WebGraphicKind.Dot);
                    return KeywordResult.Continue;
                case Keyword.End_Dot:
                    ParseKeywordEndWebGraphic(WebGraphicKind.Dot);
                    return KeywordResult.Continue;
                case Keyword.Jsx:
                    ParseKeywordWebGraphic(s, WebGraphicKind.Jsx);
                    return KeywordResult.Continue;
                case Keyword.End_Jsx:
                    ParseKeywordEndWebGraphic(WebGraphicKind.Jsx);
                    return KeywordResult.Continue;
                case Keyword.Map:
                    ParseKeywordWebGraphic(s, WebGraphicKind.Map);
                    return KeywordResult.Continue;
                case Keyword.End_Map:
                    ParseKeywordEndWebGraphic(WebGraphicKind.Map);
                    return KeywordResult.Continue;
                case Keyword.Math:
                    ParseKeywordWebGraphic(s, WebGraphicKind.Math);
                    return KeywordResult.Continue;
                case Keyword.End_Math:
                    ParseKeywordEndWebGraphic(WebGraphicKind.Math);
                    return KeywordResult.Continue;
                case Keyword.Chart:
                    ParseKeywordWebGraphic(s, WebGraphicKind.Chart);
                    return KeywordResult.Continue;
                case Keyword.End_Chart:
                    ParseKeywordEndWebGraphic(WebGraphicKind.Chart);
                    return KeywordResult.Continue;
                // Fase 3 — 10 librerías adicionales
                case Keyword.Mathbox:
                    ParseKeywordWebGraphic(s, WebGraphicKind.Mathbox);
                    return KeywordResult.Continue;
                case Keyword.End_Mathbox:
                    ParseKeywordEndWebGraphic(WebGraphicKind.Mathbox);
                    return KeywordResult.Continue;
                case Keyword.D3:
                    ParseKeywordWebGraphic(s, WebGraphicKind.D3);
                    return KeywordResult.Continue;
                case Keyword.End_D3:
                    ParseKeywordEndWebGraphic(WebGraphicKind.D3);
                    return KeywordResult.Continue;
                case Keyword.Echarts:
                    ParseKeywordWebGraphic(s, WebGraphicKind.Echarts);
                    return KeywordResult.Continue;
                case Keyword.End_Echarts:
                    ParseKeywordEndWebGraphic(WebGraphicKind.Echarts);
                    return KeywordResult.Continue;
                case Keyword.Vega:
                    ParseKeywordWebGraphic(s, WebGraphicKind.Vega);
                    return KeywordResult.Continue;
                case Keyword.End_Vega:
                    ParseKeywordEndWebGraphic(WebGraphicKind.Vega);
                    return KeywordResult.Continue;
                case Keyword.Visnet:
                    ParseKeywordWebGraphic(s, WebGraphicKind.Visnet);
                    return KeywordResult.Continue;
                case Keyword.End_Visnet:
                    ParseKeywordEndWebGraphic(WebGraphicKind.Visnet);
                    return KeywordResult.Continue;
                case Keyword.P5:
                    ParseKeywordWebGraphic(s, WebGraphicKind.P5);
                    return KeywordResult.Continue;
                case Keyword.End_P5:
                    ParseKeywordEndWebGraphic(WebGraphicKind.P5);
                    return KeywordResult.Continue;
                case Keyword.Matter:
                    ParseKeywordWebGraphic(s, WebGraphicKind.Matter);
                    return KeywordResult.Continue;
                case Keyword.End_Matter:
                    ParseKeywordEndWebGraphic(WebGraphicKind.Matter);
                    return KeywordResult.Continue;
                case Keyword.Cannon:
                    ParseKeywordWebGraphic(s, WebGraphicKind.Cannon);
                    return KeywordResult.Continue;
                case Keyword.End_Cannon:
                    ParseKeywordEndWebGraphic(WebGraphicKind.Cannon);
                    return KeywordResult.Continue;
                case Keyword.Geogebra:
                    ParseKeywordWebGraphic(s, WebGraphicKind.Geogebra);
                    return KeywordResult.Continue;
                case Keyword.End_Geogebra:
                    ParseKeywordEndWebGraphic(WebGraphicKind.Geogebra);
                    return KeywordResult.Continue;
                // Fase 4 — animaciones
                case Keyword.Anime:
                    ParseKeywordWebGraphic(s, WebGraphicKind.Anime);
                    return KeywordResult.Continue;
                case Keyword.End_Anime:
                    ParseKeywordEndWebGraphic(WebGraphicKind.Anime);
                    return KeywordResult.Continue;
                case Keyword.Manim:
                    ParseKeywordWebGraphic(s, WebGraphicKind.Manim);
                    return KeywordResult.Continue;
                case Keyword.End_Manim:
                    ParseKeywordEndWebGraphic(WebGraphicKind.Manim);
                    return KeywordResult.Continue;
                case Keyword.NoSub:
                    _parser.VariableSubstitution = MathParser.VariableSubstitutionOptions.VariablesOnly;
                    break;
                case Keyword.NoVar:
                    _parser.VariableSubstitution = MathParser.VariableSubstitutionOptions.SubstitutionsOnly;
                    break;
                case Keyword.VarSub:
                    _parser.VariableSubstitution = MathParser.VariableSubstitutionOptions.VariablesAndSubstitutions;
                    break;
                case Keyword.Const:
                    _parser.IsConst = true;
                    return KeywordResult.None;
                case Keyword.Split:
                    _parser.Split = true;
                    break;
                case Keyword.Wrap:
                    _parser.Split = false;
                    break;
                case Keyword.Deg:
                    _parser.Degrees = 0;
                    break;
                case Keyword.Rad:
                    _parser.Degrees = 1;
                    break;
                case Keyword.Gra:
                    _parser.Degrees = 2;
                    break;
                case Keyword.Round:
                    ParseKeywordRound(s);
                    break;
                case Keyword.Format:
                    ParseKeywordFormat(s);
                    break;
                case Keyword.Repeat:
                    ParseKeywordRepeat(s);
                    break;
                case Keyword.For:
                    ParseKeywordFor(s);
                    break;
                case Keyword.While:
                    ParseKeywordWhile(s);
                    break;
                case Keyword.Loop:
                    ParseKeywordLoop(s);
                    break;
                case Keyword.Break:
                    if (ParseKeywordBreak())
                        return KeywordResult.Break;
                    break;
                case Keyword.Continue:
                    ParseKeywordContinue();
                    break;
                case Keyword.Md:
                    ParseKeywordMd(s);
                    break;
                case Keyword.Read:
                    ParseKeywordRead(s);
                    break;
                case Keyword.Write:
                case Keyword.Append:
                    ParseKeywordWrite(s, keyword);
                    break;
                case Keyword.Phasor:
                    _parser.Phasor = true;
                    break;
                case Keyword.Complex:
                    _parser.Phasor = false;
                    break;
                case Keyword.Function:
                    ParseKeywordFunction(s);
                    break;
                case Keyword.End_Function:
                    ParseKeywordEndFunction();
                    break;
                default:
                    if (keyword != Keyword.Global && keyword != Keyword.Local)
                        return KeywordResult.None;
                    break;
            }
            return KeywordResult.Continue;
        }

        KeywordResult ParseKeywordInput()
        {
            if (_condition.IsSatisfied)
            {
                _previousKeyword = Keyword.Input;
                if (_calculate)
                {
                    _startLine = _currentLine + 1;
                    _pauseCharCount = _sb.Length;
                    _calculate = false;
                    return KeywordResult.Continue;
                }
                return KeywordResult.Break;
            }
            return _calculate ? KeywordResult.Continue : KeywordResult.Break;
        }

        KeywordResult ParseKeywordPause()
        {
            if (_condition.IsSatisfied && (_calculate || _startLine > 0))
            {
                if (_calculate)
                {
                    if (_isPausedByUser)
                        _startLine = _currentLine;
                    else
                        _startLine = _currentLine + 1;
                }

                if (_previousKeyword != Keyword.Input)
                    _pauseCharCount = _sb.Length;

                _previousKeyword = Keyword.Pause;
                _isPausedByUser = false;
                return KeywordResult.Break;
            }
            if (_isVisible && !_calculate)
                _sb.Append($"<p{HtmlId} class=\"{HtmlLineMarker}cond\">#pause</p>");

            return KeywordResult.Continue;
        }

        private void ParseKeywordRound(ReadOnlySpan<char> s)
        {
            if (s.Length > 6)
            {
                var expr = s[6..].Trim();
                if (expr.SequenceEqual("default"))
                    Settings.Math.Decimals = _decimals;
                else if (int.TryParse(expr, out int n))
                    Settings.Math.Decimals = n;
                else
                {
                    try
                    {
                        _parser.Parse(expr);
                        _parser.Calculate();
                        Settings.Math.Decimals = (int)Math.Round(_parser.Real, MidpointRounding.AwayFromZero);
                    }
                    catch (MathParserException ex)
                    {
                        AppendError(s.ToString(), ex.Message, _currentLine);
                    }
                }
            }
            else
                Settings.Math.Decimals = _decimals;
        }

        private void ParseKeywordRepeat(ReadOnlySpan<char> s)
        {
            ReadOnlySpan<char> expression = s.Length > 7 ? // #repeat - 7    
                s[7..].Trim() :
                [];

            if (_calculate)
            {
                if (_condition.IsSatisfied)
                {
                    var count = 0d;
                    if (!expression.IsWhiteSpace())
                    {
                        try
                        {
                            _parser.Parse(expression);
                            _parser.Calculate();
                            if (_parser.Real > Loop.MaxCount)
                                AppendError(s.ToString(), string.Format(Messages.Number_of_iterations_exceeds_the_maximum_0, Loop.MaxCount), _currentLine);
                            else
                                count = Math.Round(_parser.Real, MidpointRounding.AwayFromZero);
                        }
                        catch (MathParserException ex)
                        {
                            AppendError(s.ToString(), ex.Message, _currentLine);
                        }
                    }
                    else
                        count = -1d;

                    _loops.Push(new RepeatLoop(_currentLine, count, _condition.Id));
                }
            }
            else if (_isVisible)
            {
                if (expression.IsWhiteSpace())
                    _sb.Append($"<p{HtmlId} class=\"{HtmlLineMarker}cond\">#repeat</p><div class=\"indent\">");
                else
                {
                    try
                    {
                        _parser.Parse(expression);
                        _sb.Append($"<p{HtmlId}{HtmlLineClass}><span class=\"cond\">#repeat</span> <span class=\"eq\">{_parser.ToHtml()}</span></p><div class=\"indent\">");
                    }
                    catch (MathParserException ex)
                    {
                        AppendError(s.ToString(), ex.Message, _currentLine);
                    }
                }
            }
        }

        private void ParseKeywordFor(ReadOnlySpan<char> s)
        {
            ReadOnlySpan<char> expression = s.Length > 4 ? // #for - 4
                s[4..].Trim() :
                [];

            if (expression.IsWhiteSpace())
                throw Exceptions.ExpressionEmpty();

            (int loopStart, int loopEnd) = GetForLoopLimits(expression);
            if (loopStart > -1 &&
                loopEnd > loopStart)
            {
                var varName = expression[..loopStart].Trim().ToString();
                var startExpr = expression[(loopStart + 1)..loopEnd].Trim();
                var endExpr = expression[(loopEnd + 1)..].Trim();
                if (Validator.IsVariable(varName))
                {
                    if (_calculate)
                    {
                        if (_condition.IsSatisfied)
                        {
                            try
                            {
                                _parser.Parse(startExpr);
                                _parser.Calculate();
                                var r1 = _parser.Result;
                                var u1 = _parser.Units;
                                _parser.Parse(endExpr);
                                _parser.Calculate();
                                var r2 = _parser.Result;
                                var u2 = _parser.Units;
                                IScalarValue start, end;
                                if (r1.IsReal && r2.IsReal)
                                {
                                    start = new RealValue(r1.Re, u1);
                                    end = new RealValue(r2.Re, u2);
                                }
                                else
                                {
                                    start = new ComplexValue(r1, u1);
                                    end = new ComplexValue(r2, u2);
                                }
                                var count = Math.Abs((end - start).Re) + 1;
                                if (count > Loop.MaxCount)
                                {
                                    AppendError(s.ToString(), string.Format(Messages.Number_of_iterations_exceeds_the_maximum_0, Loop.MaxCount), _currentLine);
                                    return;
                                }
                                var counter = _parser.GetVariableRef(varName);
                                _loops.Push(new ForLoop(_currentLine, start, end, counter, _condition.Id));
                                _parser.SetVariable(varName, start);
                            }
                            catch (MathParserException ex)
                            {
                                AppendError(s.ToString(), ex.Message, _currentLine);
                            }
                        }
                    }
                    else if (_isVisible)
                    {
                        try
                        {
                            var varHtml = new HtmlWriter(null, _parser.Phasor).FormatVariable(varName, string.Empty, false);
                            _parser.Parse(startExpr);
                            var startHtml = _parser.ToHtml();
                            _parser.Parse(endExpr);
                            var endHtml = _parser.ToHtml();
                            _sb.Append($"<p{HtmlId}{HtmlLineClass}><span class=\"cond\">#for</span> <span class=\"eq\">{varHtml} = {startHtml} : {endHtml}</span></p><div class=\"indent\">");
                        }
                        catch (MathParserException ex)
                        {
                            AppendError(s.ToString(), ex.Message, _currentLine);
                        }
                    }
                }
            }
        }

        private void ParseKeywordWhile(ReadOnlySpan<char> s)
        {
            ReadOnlySpan<char> expression = s.Length > 6 ? // #while - 6
                s[7..].Trim() :
                [];

            if (expression.IsWhiteSpace())
                throw Exceptions.ExpressionEmpty();

            if (_calculate)
            {
                if (_condition.IsSatisfied)
                {
                    try
                    {
                        var commentStart = expression.IndexOf('\'');
                        var condition = commentStart < 0 ? expression : expression[..commentStart];
                        _parser.Parse(condition);
                        _parser.Calculate();
                        _condition.SetCondition(Keyword.While - Keyword.If);
                        _condition.Check(_parser.Result);
                        if (_condition.IsSatisfied)
                        {
                            _loops.Push(new WhileLoop(_currentLine, expression.ToString(), _condition.Id));
                            if (commentStart >= 0)
                                ParseTokens(GetTokens(expression[commentStart..]), false, false);
                        }
                    }
                    catch (MathParserException ex)
                    {
                        AppendError(s.ToString(), ex.Message, _currentLine);
                    }
                }
            }
            else if (_isVisible)
            {
                try
                {
                    _sb.Append($"<p{HtmlId}{HtmlLineClass}><span class=\"cond\">#while</span> ");
                    ParseTokens(GetTokens(expression), true, false);
                    _sb.Append("</p><div class=\"indent\">");
                }
                catch (MathParserException ex)
                {
                    AppendError(s.ToString(), ex.Message, _currentLine);
                }
            }
        }

        private void ParseKeywordLoop(ReadOnlySpan<char> s)
        {
            if (_calculate)
            {
                if (_condition.IsSatisfied)
                {
                    if (_loops.Count == 0)
                        AppendError(s.ToString(), Messages.loop_without_a_corresponding_repeat, _currentLine);
                    else
                    {
                        var next = _loops.Peek();
                        if (next.Id != _condition.Id)
                            AppendError(s.ToString(), Messages.Entangled_if__end_if__and_repeat__loop_blocks, _currentLine);
                        else if (!Iterate(next, true))
                            _loops.Pop();
                    }
                }
                else if (_condition.IsLoop)
                    _condition.SetCondition(Condition.RemoveConditionKeyword);
            }
            else if (_isVisible)
                _sb.Append($"</div><p{HtmlId} class=\"{HtmlLineMarker}cond\">#loop</p>");
        }

        private bool Iterate(Loop loop, bool removeWhileCondition)
        {
            if (loop is ForLoop forLoop)
                forLoop.IncrementCounter();
            else if (loop is WhileLoop whileLoop)
            {
                var expression = whileLoop.Condition;
                var commentStart = expression.IndexOfAny(['\'', '"']);
                if (commentStart < 0)
                    commentStart = expression.Length;

                var condition = expression.AsSpan(0, commentStart);
                _parser.Parse(condition);
                _parser.Calculate();
                _condition.Check(_parser.Result);
                if (_condition.IsSatisfied)
                {
                    if (commentStart < expression.Length - 1)
                        ParseTokens(GetTokens(expression.AsSpan(commentStart)), false, false);
                }
                else
                {
                    if (removeWhileCondition)
                        _condition.SetCondition(Condition.RemoveConditionKeyword);

                    loop.Break();
                }
            }
            if (loop.Iterate(ref _currentLine))
            {
                _parser.ResetStack();
                return true;
            }
            return false;
        }

        private bool ParseKeywordBreak()
        {
            if (_calculate)
            {
                if (_condition.IsSatisfied)
                {
                    if (_loops.Count != 0)
                        _loops.Peek().Break();
                    else
                        return true;
                }
            }
            else if (_isVisible)
                _sb.Append($"<p{HtmlId} class=\"{HtmlLineMarker}cond\">#break</p>");

            return false;
        }

        internal void ParseKeywordContinue()
        {
            if (_calculate)
            {
                if (_condition.IsSatisfied)
                {
                    if (_loops.Count == 0)
                        AppendError("#continue", Messages.continue_without_a_corresponding_repeat, _currentLine);
                    else
                    {
                        var loop = _loops.Peek();
                        if (Iterate(loop, false))
                            while (_condition.Id > loop.Id)
                                _condition.SetCondition(Condition.RemoveConditionKeyword);
                        else
                            loop.Break();
                    }
                }
            }
            else if (_isVisible)
                _sb.Append($"<p{HtmlId} class=\"{HtmlLineMarker}cond\">#continue</p>");
        }

        private static (int, int) GetForLoopLimits(ReadOnlySpan<char> expression)
        {
            (int start, int end) = (-1, -1);
            int n1 = 0, n2 = 0, n3 = 0;
            for (int i = 0, len = expression.Length; i < len; ++i)
            {
                switch (expression[i])
                {
                    case '=': start = i; break;
                    case ':' when n1 == 0 && n2 == 0 && n3 == 0: end = i; return (start, end);
                    case '(': ++n1; break;
                    case ')': --n1; break;
                    case '{': ++n2; break;
                    case '}': --n2; break;
                    case '[': ++n3; break;
                    case ']': --n3; break;
                }
            }
            return (start, end);
        }

        private void ParseKeywordFormat(ReadOnlySpan<char> s)
        {
            if (s.Length > 7)
            {
                var expr = s[7..].Trim();
                if (expr.SequenceEqual("default"))
                    Settings.Math.FormatString = null;
                else
                {
                    var format = expr.ToString();
                    if (Validator.IsValidFormatString(format))
                        Settings.Math.FormatString = format;
                    else
                        AppendError("#format " + format, Messages.Invalid_format_string_0, _currentLine);
                }
            }
            else
                Settings.Math.FormatString = null;
        }

        private void ParseKeywordMd(ReadOnlySpan<char> s)
        {
            if (s.Length > 3)
            {
                var expr = s[3..].Trim();
                if (expr.Equals("on", StringComparison.OrdinalIgnoreCase))
                    _isMarkdownOn = true;
                else if (expr.Equals("off", StringComparison.OrdinalIgnoreCase))
                    _isMarkdownOn = false;
                else
                    AppendError(s.ToString(), string.Format(Messages.Invalid_keyword_0, expr.ToString()), _currentLine);
            }
            else
                _isMarkdownOn = true;
        }

        private void ParseKeywordRead(ReadOnlySpan<char> s)
        {
            if (_calculate)
            {
                if (_condition.IsSatisfied)
                {
                    var options = new ReadWriteOptions(s, 0);
                    if (options.Name.IsEmpty)
                        return;

                    var data = DataExchange.Read(options);
                    if (options.Type == 'V')
                        _parser.SetVector(options.Name, data, options.IsHp);
                    else
                        _parser.SetMatrix(options.Name, data, options.Type, options.IsHp);

                    if (_isVisible)
                        ReportDataExchageResult(options, "read from");
                }
            }
            else if (_isVisible)
                _sb.Append($"<p><span{HtmlId} class=\"{HtmlLineMarker}cond\">#read</span> {s[5..]}</p>");
        }

        private void ParseKeywordWrite(ReadOnlySpan<char> s, Keyword keyword)
        {
            if (_calculate)
            {
                if (_condition.IsSatisfied)
                {
                    var options = new ReadWriteOptions(s, keyword - Keyword.Read);
                    if (options.Name.IsEmpty)
                        return;

                    var m = _parser.GetMatrix(options.Name.ToString(), options.Type);
                    DataExchange.Write(options, m);
                    if (_isVisible)
                        ReportDataExchageResult(options, keyword == Keyword.Write ? "written to" : "appended to");
                }
            }
            else if (_isVisible)
                _sb.Append($"<p><span{HtmlId} class=\"{HtmlLineMarker}cond\">#write</span> {s[6..]}</p>");
        }

        private void ReportDataExchageResult(ReadWriteOptions options, string command)
        {
            var url = $"file:///{options.FullPath.Replace('\\', '/')}";
            _sb.Append($"<p{HtmlId}{HtmlLineClass}>")
               .Append($"Matrix <span class=\"eq\">{new HtmlWriter(Settings.Math, false).FormatVariable(options.Name.ToString(), string.Empty, true)}</span>")
               .Append($" was successfully {command} <a href=\"{url}\">{options.Path}.{options.Ext}</a>");
            if (options.IsExcel)
            {
                if (!options.Sheet.IsEmpty)
                    _sb.Append($"@{options.Sheet}");
                if (!options.Start.IsEmpty)
                    _sb.Append($"!{options.Start}");
                if (!options.End.IsEmpty)
                    _sb.Append($":{options.End}");
            }
            else
            {
                if (!options.Start.IsEmpty)
                    _sb.Append($"@{options.Start}");
                if (!options.End.IsEmpty)
                    _sb.Append($":{options.End}");

                _sb.Append($" <small>SEP</small>='{options.Separator}'");
            }
            _sb.Append($" <small>TYPE</small>={options.Type}");
            _sb.Append("</p>");
        }

        // #formeq expr1 = expr2 = expr3
        // Muestra ecuación simbólica con doble igualdad.
        // Divide por '=' (fuera de []) y renderiza cada parte como #noc con ' = ' entre ellas.
        // También soporta múltiples ecuaciones separadas por coma:
        //   #deq θ_x = -∂w/∂y, θ_y = ∂w/∂x
        // → renderiza cada ecuación y las une con ',  '
        private void ParseKeywordDeq(ReadOnlySpan<char> s)
        {
            _sb.Append($"<!-- FORMEQ CALLED: len={s.Length} -->");
            // Saltar "#deq "
            var spaceIdx = s.IndexOf(' ');
            if (spaceIdx < 0) return;
            var expr = s[(spaceIdx + 1)..].ToString();

            // Equation numbering: if expr contains @@(text), extract number label
            string eqNumber = null;
            var atIdx = expr.IndexOf("@@");
            if (atIdx >= 0)
            {
                eqNumber = expr[(atIdx + 2)..].Trim();
                expr = expr[..atIdx].Trim();
            }

            // Split by top-level ',' to allow multiple equations on one #deq line
            // e.g. "θ_x = -∂w/∂y, θ_y = ∂w/∂x"
            var equations = SplitTopLevelByChar(expr, ',').Select(e => e.Trim()).Where(e => e.Length > 0).ToList();
            if (equations.Count > 1)
            {
                // Render each sub-equation independently, join with commas
                var firstEqNumber = eqNumber;  // equation number applies only once (last eq)
                for (int e = 0; e < equations.Count; e++)
                {
                    if (e > 0) _sb.Append("<p>");
                    RenderDeqSingle(equations[e], e == equations.Count - 1 ? firstEqNumber : null);
                    if (e > 0) _sb.Append("</p>");
                }
                return;
            }

            RenderDeqSingle(expr, eqNumber);
        }

        /// <summary>Render a single equation (possibly with double equality a=b=c).</summary>
        private void RenderDeqSingle(string expr, string eqNumber)
        {
            // Pre-define variables to avoid unit interpretation
            PreDefineVariables(expr);

            // Dividir por '=' fuera de [] y ()
            var parts = SplitByEqualsOutsideBrackets(expr);

            // Renderizar cada parte como #noc y unir con ' = '
            var savedIsVal = _isVal;
            _isVal = -1; // modo #noc
            _parser.IsCalculation = false;

            var sb2 = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i].Trim();
                if (string.IsNullOrEmpty(part)) continue;

                if (i > 0) sb2.Append(_lastDeqSeparator);

                // PRIORIDAD: si la parte es una llamada a función literal
                // tipo "f(x;y)" o "N_1(ξ;η)" o "x(ξ)", renderizarla como
                // función display, no como producto. Antes este patrón
                // se interpretaba como N_1·ξ·η por el tokenizer normal.
                var fnCallHtml = TryRenderFunctionCallSignature(part);
                if (fnCallHtml != null)
                {
                    sb2.Append(fnCallHtml);
                    continue;
                }

                // Try special rendering for derivatives, primes, matrices
                var specialHtml = TryRenderDeqSpecial(part);
                if (specialHtml != null)
                {
                    sb2.Append(specialHtml);
                    continue;
                }

                // For arbitrary scalar expressions use our recursive scalar
                // renderer which builds fractions, products, exponents,
                // subscripts — matching Calcpad's native HTML structure.
                sb2.Append(RenderDeqScalar(part));
            }

            _isVal = savedIsVal;
            _parser.IsCalculation = _isVal != -1;

            if (sb2.Length > 0)
            {
                var sb2Str = sb2.ToString();
                var eqStyle = EqStyleForMatrix(sb2Str);
                if (eqNumber != null)
                    _sb.Append($"<p{HtmlId} class=\"{HtmlLineMarker}eqnum\"><span class=\"eq\"{eqStyle}>{sb2Str}</span><span class=\"eqn\">{eqNumber}</span></p>\n");
                else
                    _sb.Append($"<p{HtmlId}{HtmlLineClass}><span class=\"eq\"{eqStyle}>{sb2Str}</span></p>\n");
            }
            else
                _sb.Append($"<p{HtmlId}{HtmlLineClass}><span class=\"err\">#formeq: no output for '{expr}'</span></p>\n");
        }

        /// <summary>
        /// When the HTML contains a native Calcpad matrix, wrap the whole
        /// expression in inline-flex with align-items:center so that the
        /// '=' sign and any operator adjacent to the matrix align with
        /// the VERTICAL MIDDLE of the matrix brackets (not with the text
        /// baseline, which the Calcpad stylesheet would otherwise use).
        /// </summary>
        private static string EqStyleForMatrix(string html)
        {
            if (html != null && html.Contains("class=\"matrix"))
                return " style=\"display:inline-flex;align-items:center;flex-wrap:nowrap;gap:0 0.3em;\"";
            return "";
        }

        /// <summary>
        /// Render an expression that contains a matrix literal "[..|..|..]"
        /// anywhere inside (e.g. "A * [1,2|3,4] + B" or "(E·t^3/(..)) · [..|..]").
        /// Splits at the matrix brackets, renders the prefix/suffix through the
        /// normal parser and the matrix through the big-bracket template.
        /// Returns null if no matrix found.
        /// </summary>
        private string TryRenderExpressionWithMatrix(string expr)
        {
            if (string.IsNullOrEmpty(expr)) return null;
            // Find the first '[' at depth=0 that starts a matrix literal
            int depth = 0;
            int matStart = -1;
            int matEnd = -1;
            for (int i = 0; i < expr.Length; i++)
            {
                var c = expr[i];
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == '[' && depth == 0)
                {
                    // Find matching ']'
                    int br = 1;
                    for (int j = i + 1; j < expr.Length; j++)
                    {
                        if (expr[j] == '[') br++;
                        else if (expr[j] == ']')
                        {
                            br--;
                            if (br == 0)
                            {
                                // Check if this bracketed content has a '|' (matrix)
                                var inner = expr.Substring(i + 1, j - i - 1);
                                if (inner.Contains('|'))
                                {
                                    matStart = i;
                                    matEnd = j;
                                }
                                break;
                            }
                        }
                    }
                    if (matStart >= 0) break;
                }
            }
            if (matStart < 0) return null;

            var prefix = expr.Substring(0, matStart).Trim();
            var matExpr = expr.Substring(matStart, matEnd - matStart + 1);
            var suffix = expr.Substring(matEnd + 1).Trim();

            // Strip trailing operator from prefix and leading operator from suffix
            // so we can render them as separate operator glyphs outside the matrix.
            string prefixTrail = "";
            while (prefix.Length > 0 && "+-*/·×⋅ ".IndexOf(prefix[^1]) >= 0)
            {
                prefixTrail = prefix[^1] + prefixTrail;
                prefix = prefix[..^1].TrimEnd();
            }
            string suffixHead = "";
            while (suffix.Length > 0 && "+-*/·×⋅ ".IndexOf(suffix[0]) >= 0)
            {
                suffixHead += suffix[0];
                suffix = suffix[1..].TrimStart();
            }

            string renderChunk(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return "";
                return RenderDeqScalar(s);
            }

            string DispOps(string ops) => ops.Replace("*", "·").Trim();

            var sb = new System.Text.StringBuilder();
            if (prefix.Length > 0) sb.Append(renderChunk(prefix));
            var prefixOp = DispOps(prefixTrail);
            if (!string.IsNullOrEmpty(prefixOp)) sb.Append(' ').Append(prefixOp).Append(' ');
            var matHtml = TryRenderMatrixLiteral(matExpr);
            if (string.IsNullOrEmpty(matHtml)) return null;
            sb.Append(matHtml);
            var suffixOp = DispOps(suffixHead);
            if (!string.IsNullOrEmpty(suffixOp)) sb.Append(' ').Append(suffixOp).Append(' ');
            if (suffix.Length > 0) sb.Append(renderChunk(suffix));
            return sb.ToString();
        }

        // #cen inline: parse expression and center it
        // Supports: #cen 'text, #cen expr, #cen #deq expr @@(num)
        private void ParseKeywordCenInline(ReadOnlySpan<char> s)
        {
            var spaceIdx = s.IndexOf(' ');
            if (spaceIdx < 0) return;
            var content = s[(spaceIdx + 1)..].ToString().Trim();
            if (string.IsNullOrEmpty(content)) return;

            // If content starts with #deq → delegate to ParseKeywordDeq, then wrap result
            if (content.StartsWith("#deq ", StringComparison.OrdinalIgnoreCase))
            {
                // Save current sb position
                var posBefore = _sb.Length;
                ParseKeywordDeq(content.AsSpan());
                // Wrap the generated <p> with centering class
                var generated = _sb.ToString(posBefore, _sb.Length - posBefore);
                _sb.Remove(posBefore, _sb.Length - posBefore);
                // Add cen-line class to the <p> tag
                generated = generated.Replace("<p", "<p class=\"cen-line\"");
                generated = generated.Replace("class=\"eqnum\"", "class=\"eqnum cen-line\"");
                _sb.Append(generated);
                return;
            }

            // Check if it's a comment (starts with ')
            if (content.StartsWith("'"))
            {
                _sb.Append($"<p{HtmlId} class=\"{HtmlLineMarker}cen-line\">{content[1..]}</p>\n");
                return;
            }

            // Parse as equation
            var savedIsVal = _isVal;
            _parser.IsCalculation = true;
            try
            {
                _parser.Parse(content);
                var html = _parser.ToHtml();
                if (string.IsNullOrWhiteSpace(html))
                {
                    var w = new HtmlWriter(Settings.Math, _parser.Phasor);
                    var varHtml = w.FormatVariable(content, string.Empty, false);
                    var val = _parser.ResultAsString;
                    html = $"{varHtml} = {val}";
                }
                _sb.Append($"<p{HtmlId} class=\"{HtmlLineMarker}cen-line\"><span class=\"eq\">{html}</span></p>\n");
            }
            catch
            {
                // Fallback: try as #noc
                _isVal = -1;
                _parser.IsCalculation = false;
                try
                {
                    _parser.Parse(content, false);
                    var html = _parser.ToHtml();
                    _sb.Append($"<p{HtmlId} class=\"{HtmlLineMarker}cen-line\"><span class=\"eq\">{html}</span></p>\n");
                }
                catch
                {
                    _sb.Append($"<p{HtmlId} class=\"{HtmlLineMarker}cen-line\">{content}</p>\n");
                }
            }
            _isVal = savedIsVal;
            _parser.IsCalculation = _isVal != -1;
        }

        private void ParseKeywordBlkStart()
        {
            _insideBlk = true;
            _sb.Append($"<div class=\"col-blk-wrap\">\n");
        }

        private void ParseKeywordBlkEnd()
        {
            _insideBlk = false;
            _sb.Append("</div>\n");
        }

        // =====================================================================
        // #inl expr1 ; expr2 ; expr3   → inline columns (one line)
        // #blk ... #end blk            → block mode (each line = row of columns)
        //
        // Each expression separated by ';' gets its own column.
        // Expressions can be calculations (evaluated) or text ('comment).
        // Supports #deq-style decorative equations and normal calculations.
        // =====================================================================
        /// <summary>
        /// True iff <paramref name="s"/> is just one bare identifier (Latin
        /// or Greek letters, digits, underscore, subscript). Used by
        /// <see cref="ParseKeywordColumns"/> to override the default
        /// TEXT-first alternation rule so that `'a' kN/m` evaluates `a`.
        /// </summary>
        private static bool IsBareIdentifierForBlk(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            // First char must be a letter (Latin, Greek, or _ )
            char first = s[0];
            if (!char.IsLetter(first) && first != '_') return false;
            for (int i = 1; i < s.Length; i++)
            {
                char c = s[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '.')
                    return false;
            }
            return true;
        }

        private void ParseKeywordColumns(ReadOnlySpan<char> s, bool isBlock)
        {
            // Skip "#inl " or "#blk "
            var spaceIdx = s.IndexOf(' ');
            if (spaceIdx < 0) return;
            var content = s[(spaceIdx + 1)..].ToString();

            // Split by ';' at top level. `;` is a column separator when:
            //   1) Outside parentheses/brackets, AND
            //   2) EITHER outside a text region (`'`…`'`) — the new
            //      preferred convention `'cell1'; 'cell2'`,
            //   3) OR followed by `'` (after optional whitespace) — the
            //      legacy convention `'cell1 ; 'cell2`. The next `'`
            //      signals "start of new cell" so we treat the `;` as a
            //      separator even when text is still open.
            //
            // Inside a text region with NO trailing `'`, a literal `;`
            // stays as part of the text content (e.g. `'a sentence ; with
            // semicolon` is one cell).
            var parts = new List<string>();
            int depth = 0, last = 0;
            bool inText = false;
            for (int i = 0; i < content.Length; i++)
            {
                var c = content[i];
                if (c == '\'')
                {
                    inText = !inText;
                }
                else if (c == '(' || c == '[')
                {
                    if (!inText) depth++;
                }
                else if (c == ')' || c == ']')
                {
                    if (!inText) depth--;
                }
                else if (c == ';' && depth == 0)
                {
                    // Split unconditionally if outside text. If inside text,
                    // look ahead: a `'` after whitespace means "new cell
                    // starting" (legacy `'a ; 'b` syntax).
                    bool shouldSplit = !inText;
                    if (!shouldSplit)
                    {
                        int j = i + 1;
                        while (j < content.Length && (content[j] == ' ' || content[j] == '\t'))
                            j++;
                        if (j < content.Length && content[j] == '\'')
                        {
                            shouldSplit = true;
                            // Closing the implicit text region: the legacy
                            // `'a ; 'b` form leaves `'a` open at the split
                            // point. Reset the inText flag so the rest of
                            // the line tracks correctly.
                            inText = false;
                        }
                    }
                    if (shouldSplit)
                    {
                        parts.Add(content[last..i].Trim());
                        last = i + 1;
                    }
                }
            }
            parts.Add(content[last..].Trim());

            if (parts.Count == 0) return;

            // Render each part
            var savedIsVal = _isVal;
            var columns = new List<string>();

            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part))
                {
                    columns.Add("&nbsp;");
                    continue;
                }

                // Cell starts with ' — check for inline alternation
                // following Calcpad's standard line convention:
                //   `'` opens TEXT mode; each subsequent `'` toggles to EXPR
                //   and back to TEXT. So the fragment BETWEEN the 1st and
                //   2nd `'` is TEXT, between 2nd and 3rd is EXPR, etc.
                //
                // Examples (cell shown with leading `'`):
                //   `'simple text`              → text "simple text"
                //   `'a vale 'a' kN/m`          → "a vale " + expr `a` + " kN/m"
                //   `'b vale 'b' al final`      → "b vale " + expr `b` + " al final"
                //
                // To EVALUATE an expression with NO leading text inside the
                // cell, prefix with empty quotes: `'' 'a = 2 + 3' more text`
                // → "" + "" + expr `a = 2 + 3` + " more text".
                //
                // Implementation: split the cell (including leading `'`) by
                // `'`. fragments[0] is always "" (before opening quote).
                // ODD fragments are TEXT, EVEN fragments (≥2) are EXPR.
                if (part.StartsWith("'"))
                {
                    if (!part.AsSpan(1).Contains('\''))
                    {
                        // Pure text cell — no internal toggles. Strip the
                        // leading apostrophe and emit as text span.
                        columns.Add($"<span>{part[1..]}</span>");
                        continue;
                    }
                    // Special-case: cell is exactly `'text'` — one opening
                    // quote, content, one closing quote, optional trailing
                    // whitespace. Treat as pure text (no alternation, no
                    // variable-evaluation heuristic). This is the user's
                    // preferred convention: `'col1'; 'col2'; 'col3` →
                    // three pure-text cells, even when col1/col2 happen to
                    // look like bare identifiers.
                    var ptrim = part.TrimEnd();
                    if (ptrim.EndsWith('\'') && ptrim.Length >= 2)
                    {
                        // Find the second `'` — it must be at the very end
                        // for this special case (no other `'` in between).
                        int firstInner = part.IndexOf('\'', 1);
                        if (firstInner == ptrim.Length - 1)
                        {
                            var inner = part.Substring(1, firstInner - 1);
                            columns.Add($"<span>{inner}</span>");
                            continue;
                        }
                    }
                    var fragments = part.Split('\'');
                    var cellSb = new System.Text.StringBuilder();
                    // Detect the "alternation phase". Two conventions:
                    //   Standard (Calcpad line convention): fragments[1] = TEXT
                    //   Inverted: fragments[1] = EXPR
                    //
                    // Pick INVERTED ONLY IF fragments[1].Trim() is a bare
                    // identifier (single name with no spaces/operators) AND
                    // that name is already DEFINED as a variable in the
                    // parser. So `'a' kN/m` (with `a = 5` defined) inverts
                    // → expr `a` + text ` kN/m`. But `'texto'e = 4` where
                    // `texto` is NOT defined keeps the standard convention
                    // → text "texto" + expr `e = 4` (which assigns e).
                    string frag1Trim = fragments.Length >= 2 ? fragments[1].Trim() : "";
                    bool invertConvention = frag1Trim.Length > 0 &&
                                            IsBareIdentifierForBlk(frag1Trim) &&
                                            _parser.HasVariable(frag1Trim);
                    for (int fi = 0; fi < fragments.Length; fi++)
                    {
                        var frag = fragments[fi];
                        if (fi == 0)
                        {
                            // Always empty before the opening `'`; skip.
                            continue;
                        }
                        // Standard: odd fi → TEXT, even fi → EXPR
                        // Inverted: odd fi → EXPR, even fi → TEXT
                        bool isOddIndex = (fi & 1) == 1;
                        bool treatAsExpr = invertConvention ? isOddIndex : !isOddIndex;
                        if (!treatAsExpr)
                        {
                            // TEXT fragment
                            if (!string.IsNullOrEmpty(frag))
                                cellSb.Append($"<span>{frag}</span>");
                        }
                        else
                        {
                            // EXPRESSION fragment — parse + render
                            var fragTrim = frag.Trim();
                            if (string.IsNullOrEmpty(fragTrim))
                            {
                                cellSb.Append("<span>&nbsp;</span>");
                                continue;
                            }
                            _isVal = savedIsVal;
                            _parser.IsCalculation = true;
                            try
                            {
                                _parser.Parse(fragTrim);
                                _parser.Calculate();
                                var html = _parser.ToHtml();
                                if (string.IsNullOrWhiteSpace(html))
                                    html = System.Web.HttpUtility.HtmlEncode(fragTrim);
                                cellSb.Append($"<span class=\"eq\">{html}</span>");
                            }
                            catch
                            {
                                cellSb.Append($"<span class=\"err\">{System.Web.HttpUtility.HtmlEncode(frag)}</span>");
                            }
                        }
                    }
                    columns.Add(cellSb.ToString());
                    continue;
                }

                // Cell does NOT start with `'` but contains `'` internally
                // (e.g. `c = 3' texto'`). Treat as expression-first, then
                // alternate: EXPR / TEXT / EXPR / TEXT … so the user can
                // write `c = 3' suffix text'` and get the value of c plus
                // the text appended.
                if (!part.StartsWith("'") && part.Contains('\''))
                {
                    var fragments = part.Split('\'');
                    var cellSb = new System.Text.StringBuilder();
                    for (int fi = 0; fi < fragments.Length; fi++)
                    {
                        var frag = fragments[fi];
                        // EVEN fi → EXPR, ODD fi → TEXT
                        bool isEvenIndex = (fi & 1) == 0;
                        if (isEvenIndex)
                        {
                            // EXPR
                            var fragTrim = frag.Trim();
                            if (string.IsNullOrEmpty(fragTrim))
                                continue;
                            _isVal = savedIsVal;
                            _parser.IsCalculation = true;
                            try
                            {
                                _parser.Parse(fragTrim);
                                _parser.Calculate();
                                var html = _parser.ToHtml();
                                if (string.IsNullOrWhiteSpace(html))
                                    html = System.Web.HttpUtility.HtmlEncode(fragTrim);
                                cellSb.Append($"<span class=\"eq\">{html}</span>");
                            }
                            catch
                            {
                                cellSb.Append($"<span class=\"err\">{System.Web.HttpUtility.HtmlEncode(frag)}</span>");
                            }
                        }
                        else
                        {
                            // TEXT
                            if (!string.IsNullOrEmpty(frag))
                                cellSb.Append($"<span>{frag}</span>");
                        }
                    }
                    columns.Add(cellSb.ToString());
                    continue;
                }

                // Decorative patterns (matrix literals, partial derivatives,
                // prime notation, integral calls) — same renderer used by #deq.
                // Works for cells that START with '[' (matrix) or contain
                // special operators that the Calcpad parser can't format.
                if (part.StartsWith("[") && part.EndsWith("]") && part.Contains('|'))
                {
                    var matHtml = TryRenderMatrixLiteral(part);
                    if (!string.IsNullOrEmpty(matHtml))
                    {
                        columns.Add($"<span class=\"eq\"{EqStyleForMatrix(matHtml)}>{matHtml}</span>");
                        continue;
                    }
                }
                // For assignments "Name = [...|...]", split at first '=' outside
                // brackets and render LHS + " = " + matrix
                var eqIdx = FindTopLevelEquals(part);
                if (eqIdx > 0)
                {
                    var lhs = part.Substring(0, eqIdx).Trim();
                    var rhs = part.Substring(eqIdx + 1).Trim();
                    if (rhs.StartsWith("[") && rhs.EndsWith("]") && rhs.Contains('|'))
                    {
                        var matHtml = TryRenderMatrixLiteral(rhs);
                        if (!string.IsNullOrEmpty(matHtml))
                        {
                            var lhsHtml = DeqRenderVar(lhs);
                            var matContent = $"{lhsHtml} = {matHtml}";
                            columns.Add($"<span class=\"eq\"{EqStyleForMatrix(matContent)}>{matContent}</span>");
                            continue;
                        }
                    }
                }
                // Standalone decorative patterns that the normal parser can't
                // handle: ∂f/∂x, f'(x), integral(...), etc. Try TryRenderDeqSpecial.
                if (part.Contains('∂') || part.Contains('∫')
                    || part.StartsWith("integral(", StringComparison.OrdinalIgnoreCase))
                {
                    var specialHtml = TryRenderDeqSpecial(part);
                    if (!string.IsNullOrEmpty(specialHtml))
                    {
                        columns.Add($"<span class=\"eq\">{specialHtml}</span>");
                        continue;
                    }
                }

                // Symbolic operations (integrate, diff, simplify, solve, …):
                // route through SymbolicProcessor just like #sym does, so cells
                // can mix symbolic work with plain numeric calculations.
                if (SymbolicProcessor.IsSymbolicOp(part))
                {
                    var symRes = SymbolicProcessor.Process(part);
                    if (symRes.IsError)
                    {
                        columns.Add($"<span class=\"err\">{System.Web.HttpUtility.HtmlEncode(symRes.Error)}</span>");
                    }
                    else
                    {
                        var body = RenderSymResultBody(symRes);
                        columns.Add($"<span class=\"eq\">{body}</span>");
                    }
                    continue;
                }

                // Parse as real calculation (variables get assigned)
                _isVal = savedIsVal;
                _parser.IsCalculation = true;

                try
                {
                    _parser.Parse(part);
                    // Execute: scalar/vector/matrix assignments persist in the parser's
                    // variable table so subsequent code can reference them.
                    // Wrapped in try/catch because evaluation may fail for cells that
                    // reference undefined vars on the RHS — we still want to render them.
                    try
                    {
                        _parser.Calculate(false);
                    }
                    catch { }
                    var html = _parser.ToHtml();
                    // If html is just a variable name with no '=', append the result
                    if (string.IsNullOrWhiteSpace(html) || (!html.Contains('=') && !html.Contains("&gt;")))
                    {
                        var w = new HtmlWriter(Settings.Math, _parser.Phasor);
                        var varHtml = w.FormatVariable(part, string.Empty, false);
                        var val = _parser.ResultAsString;
                        html = $"{varHtml} = {val}";
                    }
                    columns.Add($"<span class=\"eq\">{html}</span>");
                }
                catch
                {
                    // Fallback: render as #noc (decorative)
                    _isVal = -1;
                    _parser.IsCalculation = false;
                    try
                    {
                        _parser.Parse(part, false);
                        var html = _parser.ToHtml();
                        if (!string.IsNullOrWhiteSpace(html))
                            columns.Add($"<span class=\"eq\">{html}</span>");
                        else
                            columns.Add($"<span>{part}</span>");
                    }
                    catch
                    {
                        columns.Add($"<span>{part}</span>");
                    }
                }
            }

            _isVal = savedIsVal;
            _parser.IsCalculation = _isVal != -1;

            // Build HTML: flexbox row with equal columns. Combine the
            // line-tracking class with col-blk/col-inl in ONE class attribute
            // (browsers drop a second `class="…"` silently, which broke flex).
            var cssClass = isBlock ? "col-blk" : "col-inl";
            var sb2 = new System.Text.StringBuilder();
            sb2.Append($"<div{HtmlId} class=\"{HtmlLineMarker}{cssClass}\">");
            foreach (var col in columns)
                sb2.Append($"<div class=\"col-cell\">{col}</div>");
            sb2.Append("</div>\n");
            _sb.Append(sb2);
        }

        /// <summary>
        /// Try to render special #deq patterns that the MathParser can't handle:
        /// - Leibniz derivatives: d^2v/dx^2, dv/dx, d^4v/dx^4, ∂^2u/∂x^2
        /// - Prime notation: v'(x), v''(x), f''''(x)
        /// - Partial derivatives: ∂f/∂x
        /// Returns null if the part is not a special pattern.
        /// </summary>
        [ThreadStatic] private static int _tryRenderDeqSpecialDepth;
        private string TryRenderDeqSpecial(string part)
        {
            // Guard against mutual recursion with TryRenderMultiTermDerivative
            // and RenderMultiplicativeFactors (both of which call back into
            // TryRenderDeqSpecial for sub-terms). 16 levels is more than
            // enough for realistic inputs; past that, bail with null so the
            // caller can fall back to parser-based rendering.
            if (_tryRenderDeqSpecialDepth >= 16) return null;
            _tryRenderDeqSpecialDepth++;
            try { return TryRenderDeqSpecialImpl(part); }
            finally { _tryRenderDeqSpecialDepth--; }
        }

        private string TryRenderDeqSpecialImpl(string part)
        {
            // --- Pattern -1c: integrate(expr; var; a; b) → ∫ₐᵇ expr · d var ---
            // Must come before matrix pattern so cells inside matrices can
            // detect integrate() by themselves.
            if (part.StartsWith("integrate(", StringComparison.Ordinal) && part.EndsWith(")"))
            {
                int innerStart = 10; // length of "integrate("
                int d = 1;
                int innerEnd = part.Length - 1;
                for (int i = innerStart; i < part.Length; i++)
                {
                    if (part[i] == '(') d++;
                    else if (part[i] == ')') { d--; if (d == 0) { innerEnd = i; break; } }
                }
                if (innerEnd == part.Length - 1)
                {
                    var inner = part.Substring(innerStart, innerEnd - innerStart);
                    var innerParts = new List<string>();
                    int depth = 0; int start = 0;
                    for (int i = 0; i < inner.Length; i++)
                    {
                        var c = inner[i];
                        if (c == '(' || c == '[' || c == '{') depth++;
                        else if (c == ')' || c == ']' || c == '}') depth--;
                        else if (c == ';' && depth == 0)
                        {
                            innerParts.Add(inner.Substring(start, i - start).Trim());
                            start = i + 1;
                        }
                    }
                    innerParts.Add(inner.Substring(start).Trim());
                    if (innerParts.Count >= 2)
                    {
                        var expr = innerParts[0];
                        var variable = innerParts[1];
                        string aStr = innerParts.Count >= 3 ? innerParts[2] : "";
                        string bStr = innerParts.Count >= 4 ? innerParts[3] : "";
                        string exprHtml;
                        try
                        {
                            _parser.Parse(expr, false);
                            exprHtml = _parser.ToHtml();
                            if (string.IsNullOrWhiteSpace(exprHtml))
                                exprHtml = DeqRenderVar(expr);
                        }
                        catch { exprHtml = DeqRenderVar(expr); }
                        string subHtml = "", supHtml = "";
                        if (!string.IsNullOrEmpty(aStr))
                        {
                            try { _parser.Parse(aStr, false); subHtml = _parser.ToHtml(); } catch { subHtml = DeqRenderVar(aStr); }
                        }
                        if (!string.IsNullOrEmpty(bStr))
                        {
                            try { _parser.Parse(bStr, false); supHtml = _parser.ToHtml(); } catch { supHtml = DeqRenderVar(bStr); }
                        }
                        var varHtml = DeqRenderVar(variable);
                        if (!string.IsNullOrEmpty(subHtml) && !string.IsNullOrEmpty(supHtml))
                            return $"<i>∫</i><sub>{subHtml}</sub><sup>{supHtml}</sup> {exprHtml} <i>d</i>{varHtml}";
                        return $"<i>∫</i> {exprHtml} <i>d</i>{varHtml}";
                    }
                }
            }

            // --- Pattern -1b: diff(expr; var) or diff(expr; var; n) → df/dx ---
            if (part.StartsWith("diff(", StringComparison.Ordinal) && part.EndsWith(")"))
            {
                int innerStart = 5; // length of "diff("
                int d = 1;
                int innerEnd = part.Length - 1;
                for (int i = innerStart; i < part.Length; i++)
                {
                    if (part[i] == '(') d++;
                    else if (part[i] == ')') { d--; if (d == 0) { innerEnd = i; break; } }
                }
                if (innerEnd == part.Length - 1)
                {
                    var inner = part.Substring(innerStart, innerEnd - innerStart);
                    var innerParts = new List<string>();
                    int depth = 0; int start = 0;
                    for (int i = 0; i < inner.Length; i++)
                    {
                        var c = inner[i];
                        if (c == '(' || c == '[' || c == '{') depth++;
                        else if (c == ')' || c == ']' || c == '}') depth--;
                        else if (c == ';' && depth == 0)
                        {
                            innerParts.Add(inner.Substring(start, i - start).Trim());
                            start = i + 1;
                        }
                    }
                    innerParts.Add(inner.Substring(start).Trim());
                    if (innerParts.Count >= 2)
                    {
                        var expr = innerParts[0];
                        var variable = innerParts[1];
                        string nStr = innerParts.Count >= 3 ? innerParts[2] : "1";
                        string exprHtml;
                        try
                        {
                            _parser.Parse(expr, false);
                            exprHtml = _parser.ToHtml();
                            if (string.IsNullOrWhiteSpace(exprHtml))
                                exprHtml = DeqRenderVar(expr);
                        }
                        catch { exprHtml = DeqRenderVar(expr); }
                        var varHtml = DeqRenderVar(variable);
                        string num, den;
                        if (nStr == "1")
                        {
                            num = "<i>d</i>";
                            den = $"<i>d</i>{varHtml}";
                        }
                        else
                        {
                            num = $"<i>d</i><sup>{nStr}</sup>";
                            den = $"<i>d</i>{varHtml}<sup>{nStr}</sup>";
                        }
                        return $"<span class=\"dvc\">{num}<span class=\"dvl\"></span>{den}</span> {exprHtml}";
                    }
                }
            }

            // --- Pattern -1: pdiff(expr; var) → ∂/∂var (expr) ---
            // pdiff(N_1(ξ;η); ξ) → ∂N_1(ξ,η)/∂ξ rendered as fraction
            // Must come before matrix pattern so cells inside matrices can
            // detect pdiff() by themselves.
            if (part.StartsWith("pdiff(", StringComparison.Ordinal) && part.EndsWith(")"))
            {
                int innerStart = 6; // length of "pdiff("
                int parenDepth = 1;
                int innerEnd = part.Length - 1;
                // Verify the closing paren matches the pdiff(
                int d = 1;
                for (int i = innerStart; i < part.Length; i++)
                {
                    if (part[i] == '(') d++;
                    else if (part[i] == ')') { d--; if (d == 0) { innerEnd = i; break; } }
                }
                if (innerEnd == part.Length - 1)
                {
                    var inner = part.Substring(innerStart, innerEnd - innerStart);
                    // Split inner at top-level ; only
                    var innerParts = new List<string>();
                    int depth = 0; int start = 0;
                    for (int i = 0; i < inner.Length; i++)
                    {
                        var c = inner[i];
                        if (c == '(' || c == '[' || c == '{') depth++;
                        else if (c == ')' || c == ']' || c == '}') depth--;
                        else if (c == ';' && depth == 0)
                        {
                            innerParts.Add(inner.Substring(start, i - start).Trim());
                            start = i + 1;
                        }
                    }
                    innerParts.Add(inner.Substring(start).Trim());
                    if (innerParts.Count == 2)
                    {
                        var expr = innerParts[0];
                        var variable = innerParts[1];
                        // Render expr using parser if possible (so user functions render nicely)
                        string exprHtml;
                        try
                        {
                            _parser.Parse(expr, false);
                            exprHtml = _parser.ToHtml();
                            if (string.IsNullOrWhiteSpace(exprHtml))
                                exprHtml = DeqRenderVar(expr);
                        }
                        catch { exprHtml = DeqRenderVar(expr); }
                        var varHtml = DeqRenderVar(variable);
                        return $"<span class=\"dvc\"><i>∂</i><span class=\"dvl\"></span><i>∂</i>{varHtml}</span> {exprHtml}";
                    }
                }
            }

            // --- Pattern 0: Matrix literal [row1 | row2 | row3] ---
            // Renders as a proper HTML matrix with brackets. Each row has
            // comma-separated cells. Each cell is recursively rendered so
            // partial derivatives / primes / etc. inside the matrix still work.
            // Also accept 1-row matrices [a; b; c] when they contain pdiff(),
            // diff() or integrate() so symbolic operators render as math notation.
            if (part.StartsWith("[") && part.EndsWith("]") &&
                (part.IndexOf('|') > 0 ||
                 part.Contains("pdiff(") ||
                 part.Contains("diff(") ||
                 part.Contains("integrate(")))
            {
                var matHtml = TryRenderMatrixLiteral(part);
                if (!string.IsNullOrEmpty(matHtml))
                    return matHtml;
            }
            // --- Pattern 0b: Expression with embedded matrix ---
            // e.g. "(E·t^3/(12(1-ν^2))) · [1, ν, 0 | ...]" or
            // "A * [1,2|3,4] + B" — find the [..|..] inside and render neighbors
            // with the normal parser, the matrix with the big-bracket template.
            if (part.IndexOf('|') > 0 && part.IndexOf('[') >= 0)
            {
                var mixedHtml = TryRenderExpressionWithMatrix(part);
                if (!string.IsNullOrEmpty(mixedHtml))
                    return mixedHtml;
            }
            // --- Pattern 0c: Multi-term expression with partial/total derivative ---
            // e.g. "∂w/∂x - θ_y", "∂θ_x/∂x - ∂θ_y/∂y", "a*∂f/∂x + b",
            //      "κ_x = -∂θ_y/∂x" is split earlier by '=', so this sees "-∂θ_y/∂x" alone
            // Split by top-level +/- and render each term, then join with operators.
            // hasDeriv: accept d^n, d<digits>, ∂^n, ∂<digits>, and identifiers
            // with Greek letters / subscripts (e.g. d^2φ_i/dx_j).
            bool hasDeriv = part.Contains('∂') || part.Contains("d/d") ||
                 System.Text.RegularExpressions.Regex.IsMatch(part,
                    @"[d∂](?:\^?\d+)?[a-zA-Zα-ωΑ-Ω_][a-zA-Zα-ωΑ-Ω0-9_]*\s*/\s*[d∂][a-zA-Zα-ωΑ-Ω_]");
            if (hasDeriv && HasTopLevelAddSub(part))
            {
                var multiTermHtml = TryRenderMultiTermDerivative(part);
                if (!string.IsNullOrEmpty(multiTermHtml))
                    return multiTermHtml;
            }
            // --- Pattern 0d: Single-term multiplicative with derivative ---
            // e.g. "∂N_i/∂x · u_i", "a·∂f/∂x", "2·∂^2v/∂x^2"
            if (hasDeriv && (part.Contains('·') || part.Contains('*') || part.Contains('×') || part.Contains('⋅')))
            {
                var mulHtml = RenderMultiplicativeFactors(part);
                if (!string.IsNullOrEmpty(mulHtml))
                    return mulHtml;
            }

            // --- Pattern 1: Leibniz derivative fractions ---
            // d^nf/dx^n, d^2v/dx^2, df/dx, ∂f/∂x, ∂^2u/∂x^2, ∂^2u/∂x∂y
            // Acepta signo opcional  -  al inicio: -∂^2w/∂x^2, -df/dx, etc.
            // También acepta factor multiplicativo al inicio: 2*∂^2w/∂x∂y, 3*d^2v/dx^2
            // Accept identifiers with Greek letters, digits, and subscripts in
            // both numerator (e.g. φ_i) and denominator variables (e.g. x_j).
            var leibnizMatch = System.Text.RegularExpressions.Regex.Match(part,
                @"^(-?)((?:\d+(?:\.\d+)?[·*])?)([d∂](?:\^(\d+))?)([a-zA-Zα-ωΑ-Ω_][a-zA-Zα-ωΑ-Ω0-9_]*)\s*/\s*([d∂])([a-zA-Zα-ωΑ-Ω_][a-zA-Zα-ωΑ-Ω0-9_]*)(?:\^(\d+))?(?:([d∂])([a-zA-Zα-ωΑ-Ω_][a-zA-Zα-ωΑ-Ω0-9_]*)(?:\^(\d+))?)?$");
            if (leibnizMatch.Success)
            {
                var signPrefix = leibnizMatch.Groups[1].Value; // "" o "-"
                var factPrefix = leibnizMatch.Groups[2].Value; // "" o "2*" por ejemplo
                var dSym = leibnizMatch.Groups[3].Value;   // d or ∂ or d^2 or ∂^2
                var order = leibnizMatch.Groups[4].Value;    // order number (empty for 1st)
                var func = leibnizMatch.Groups[5].Value;     // function name (v, u, f...)
                var dSym2 = leibnizMatch.Groups[6].Value;    // d or ∂ in denominator
                var var1 = leibnizMatch.Groups[7].Value;     // variable (x, y...)
                var order2 = leibnizMatch.Groups[8].Value;   // order in denominator
                var dSym3 = leibnizMatch.Groups[9].Value;    // second ∂ in denominator (mixed)
                var var2 = leibnizMatch.Groups[10].Value;    // second variable
                var order3 = leibnizMatch.Groups[11].Value;  // second order

                // Build numerator: d²v or ∂²u (con signo y factor opcionales al inicio)
                var numSb = new System.Text.StringBuilder();
                numSb.Append($"<i>{EscapeDeqChar(dSym[0])}</i>");
                if (!string.IsNullOrEmpty(order))
                    numSb.Append($"<sup>{order}</sup>");
                numSb.Append(DeqRenderVar(func));

                // Build denominator: dx² or ∂x² or ∂x∂y
                var denSb = new System.Text.StringBuilder();
                denSb.Append($"<i>{EscapeDeqChar(dSym2[0])}</i>");
                denSb.Append(DeqRenderVar(var1));
                if (!string.IsNullOrEmpty(order2))
                    denSb.Append($"<sup>{order2}</sup>");
                if (!string.IsNullOrEmpty(dSym3))
                {
                    denSb.Append($"<i>{EscapeDeqChar(dSym3[0])}</i>");
                    denSb.Append(DeqRenderVar(var2));
                    if (!string.IsNullOrEmpty(order3))
                        denSb.Append($"<sup>{order3}</sup>");
                }

                // Agregar signo y factor al inicio (si existen) antes del wrapper de fracción
                var prefix = new System.Text.StringBuilder();
                if (!string.IsNullOrEmpty(signPrefix))
                    prefix.Append("−"); // signo menos tipográfico
                if (!string.IsNullOrEmpty(factPrefix))
                {
                    // quitar el * final y mostrar el número (ej. "2*" → "2·")
                    var numStr = factPrefix.TrimEnd('*', '·');
                    prefix.Append(numStr);
                    prefix.Append("·");
                }
                return $"{prefix}<span class=\"dvc\">{numSb}<span class=\"dvl\"></span>{denSb}</span>";
            }

            // --- Pattern 1b: Integral function calls (Calcpad) ---
            // integral(body; var; a; b)            → ∫[a..b] body dvar
            // integral(integral(body; v1; a1; b1); v2; a2; b2)  → ∫∫ doble
            // Admite prefijo/sufijo (p.ej. K_e = integral(...)*detJ o A*integral(...)+B)
            // — reconocemos integral(...) en cualquier posición y renderizamos vecinos
            //   con el parser normal; el resto cae al parser fallback.
            if (part.IndexOf("integral(", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var mixedHtml = TryRenderExpressionWithIntegrals(part);
                if (!string.IsNullOrEmpty(mixedHtml))
                    return mixedHtml;
            }

            // --- Pattern 2: Prime notation: v'(x), v''(x), f''''(x), θ'  ---
            // Also handles: κ(x), v(x) without primes but with special chars
            var primeMatch = System.Text.RegularExpressions.Regex.Match(part,
                @"^(\w+)('{1,6})(?:\(([^)]*)\))?$");
            if (primeMatch.Success)
            {
                var funcName = primeMatch.Groups[1].Value;
                var primes = primeMatch.Groups[2].Value;
                var args = primeMatch.Groups[3].Value;

                var sb = new System.Text.StringBuilder();
                sb.Append(DeqRenderVar(funcName));
                // Render primes as superscript with proper prime characters
                var primeStr = new string('\u2032', primes.Length); // ′ (prime character)
                sb.Append($"<sup>{primeStr}</sup>");
                if (!string.IsNullOrEmpty(args))
                {
                    sb.Append('(');
                    sb.Append(DeqRenderVar(args));
                    sb.Append(')');
                }
                return sb.ToString();
            }

            // --- Helper: render Calcpad integral(...) con simbolo de integral ---
            // (metodo local; implementacion abajo)

            // --- Pattern 3: Simple function call with special chars: κ(x), Π(x) ---
            // These fail in parser because κ etc. are not recognized
            var funcCallMatch = System.Text.RegularExpressions.Regex.Match(part,
                @"^([κΠΣπθφψωαβγδεζηλμνξρστυχ∂∇Δ]\w*)\(([^)]*)\)$");
            if (funcCallMatch.Success)
            {
                var funcName = funcCallMatch.Groups[1].Value;
                var args = funcCallMatch.Groups[2].Value;
                return $"{DeqRenderVar(funcName)}({DeqRenderVar(args)})";
            }

            // --- Pattern 4: Standalone Leibniz without fraction: EI*d^4v/dx^4 or similar ---
            // Handle expressions with embedded Leibniz derivatives multiplied by constants
            var embeddedMatch = System.Text.RegularExpressions.Regex.Match(part,
                @"^(.+?)\*([d∂](?:\^(\d+))?)(\w+)\s*/\s*([d∂])(\w)(?:\^(\d+))?$");
            if (embeddedMatch.Success)
            {
                var prefix = embeddedMatch.Groups[1].Value.Trim();
                // Render prefix through parser, derivative as fraction
                string prefixHtml;
                try
                {
                    _parser.Parse(prefix, false);
                    prefixHtml = _parser.ToHtml();
                    if (string.IsNullOrWhiteSpace(prefixHtml))
                        prefixHtml = DeqRenderVar(prefix);
                }
                catch { prefixHtml = DeqRenderVar(prefix); }

                var dSym = embeddedMatch.Groups[2].Value;
                var order = embeddedMatch.Groups[3].Value;
                var func = embeddedMatch.Groups[4].Value;
                var dSym2 = embeddedMatch.Groups[5].Value;
                var var1 = embeddedMatch.Groups[6].Value;
                var order2 = embeddedMatch.Groups[7].Value;

                var numSb = new System.Text.StringBuilder();
                numSb.Append($"<i>{EscapeDeqChar(dSym[0])}</i>");
                if (!string.IsNullOrEmpty(order))
                    numSb.Append($"<sup>{order}</sup>");
                numSb.Append(DeqRenderVar(func));

                var denSb = new System.Text.StringBuilder();
                denSb.Append($"<i>{EscapeDeqChar(dSym2[0])}</i>");
                denSb.Append(DeqRenderVar(var1));
                if (!string.IsNullOrEmpty(order2))
                    denSb.Append($"<sup>{order2}</sup>");

                return $"{prefixHtml} · <span class=\"dvc\">{numSb}<span class=\"dvl\"></span>{denSb}</span>";
            }

            return null; // not a special pattern
        }

        /// <summary>
        /// Render Calcpad integral(body; var; a; b) calls as ∫ body dvar with limits.
        /// Supports nested integrals: integral(integral(H; y; -b; b); x; -a; a) → ∫∫
        /// </summary>
        private string TryRenderIntegralSpecial(string expr)
        {
            // Parse integral(args) — find matching paren
            if (!expr.StartsWith("integral(", StringComparison.OrdinalIgnoreCase))
                return null;
            var openIdx = expr.IndexOf('(');
            if (openIdx < 0) return null;
            var closeIdx = FindMatchingParen(expr, openIdx);
            if (closeIdx < 0 || closeIdx != expr.Length - 1) return null;

            // Split top-level args by ';'
            var args = SplitTopLevelBySemicolon(expr.Substring(openIdx + 1, closeIdx - openIdx - 1));
            if (args.Count < 2) return null;

            var body = args[0].Trim();
            var v = args.Count >= 2 ? args[1].Trim() : "x";
            var lo = args.Count >= 3 ? args[2].Trim() : null;
            var hi = args.Count >= 4 ? args[3].Trim() : null;

            // Render body: si es otro integral, recursivo; sino via DeqRenderVar o parser
            string bodyHtml;
            if (body.StartsWith("integral(", StringComparison.OrdinalIgnoreCase))
            {
                bodyHtml = TryRenderIntegralSpecial(body) ?? body;
            }
            else
            {
                // Usar el parser normal para el body; fallback a texto plano con DeqRenderVar
                try
                {
                    _parser.Parse(body, false);
                    bodyHtml = _parser.ToHtml();
                    if (string.IsNullOrWhiteSpace(bodyHtml))
                        bodyHtml = DeqRenderVar(body);
                }
                catch { bodyHtml = DeqRenderVar(body); }
            }

            var loHtml = lo is not null ? DeqRenderVar(lo) : "";
            var hiHtml = hi is not null ? DeqRenderVar(hi) : "";

            // Usar el template nativo de Calcpad para integrales/Nary:
            //   <span class="dvr"><small>{sup}</small><span class="nary">∫</span><small>{sub}</small></span>{expr}
            // donde expr = body + " d" + var
            var sb = new System.Text.StringBuilder();
            sb.Append("<span class=\"dvr\"><small>");
            sb.Append(hiHtml);
            sb.Append("</small><span class=\"nary\">∫</span><small>");
            sb.Append(loHtml);
            sb.Append("</small></span>");
            sb.Append(bodyHtml);
            sb.Append("\u2009<var>d</var>");
            sb.Append(DeqRenderVar(v));
            return sb.ToString();
        }

        /// <summary>
        /// Render una expresión que contiene una o varias llamadas a integral(...)
        /// como mezcla de HTML (big ∫ template) + fragmentos renderizados por el parser.
        /// Ejemplo: "K_e = integral(B_b^T*D_b*B_b; ξ; -1; 1)*detJ"
        ///   → "K_e = ∫... detJ"
        /// Devuelve null si no encuentra ningún integral(...) válido.
        /// </summary>
        private string TryRenderExpressionWithIntegrals(string expr)
        {
            var sb = new System.Text.StringBuilder();
            int cursor = 0;
            bool foundAny = false;
            while (cursor < expr.Length)
            {
                int idx = expr.IndexOf("integral(", cursor, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                {
                    // No hay más integrales — renderizar el resto como fragmento normal
                    sb.Append(RenderFragmentAsHtml(expr.Substring(cursor)));
                    break;
                }
                // Contenido antes del integral
                if (idx > cursor)
                    sb.Append(RenderFragmentAsHtml(expr.Substring(cursor, idx - cursor)));
                // Encontrar paréntesis de cierre
                int openIdx = idx + "integral".Length;
                if (openIdx >= expr.Length || expr[openIdx] != '(')
                {
                    // No es una llamada válida — renderizar como fragmento normal
                    sb.Append(RenderFragmentAsHtml(expr.Substring(idx, openIdx - idx)));
                    cursor = openIdx;
                    continue;
                }
                int closeIdx = FindMatchingParen(expr, openIdx);
                if (closeIdx < 0)
                {
                    sb.Append(RenderFragmentAsHtml(expr.Substring(idx)));
                    break;
                }
                var intExpr = expr.Substring(idx, closeIdx - idx + 1);
                var intHtml = TryRenderIntegralSpecial(intExpr);
                if (!string.IsNullOrEmpty(intHtml))
                {
                    sb.Append(intHtml);
                    foundAny = true;
                }
                else
                    sb.Append(RenderFragmentAsHtml(intExpr));
                cursor = closeIdx + 1;
            }
            return foundAny ? sb.ToString() : null;
        }

        /// <summary>
        /// Helper: renderizar un fragmento de expresión (sin integrales) via el parser
        /// normal; si falla, fallback a DeqRenderVar token-a-token.
        /// </summary>
        private string RenderFragmentAsHtml(string fragment)
        {
            fragment = fragment.Trim();
            if (string.IsNullOrEmpty(fragment)) return string.Empty;
            // Quitar operadores envolventes triviales ("* detJ" → "<var>·detJ</var>")
            // Pero queremos preservar operadores tipo *, +, etc. en el output.
            // Estrategia: separar operadores leading/trailing y renderizar solo el núcleo.
            int leadOps = 0;
            while (leadOps < fragment.Length && "+-*/·×⋅ ".IndexOf(fragment[leadOps]) >= 0) leadOps++;
            int trailOps = fragment.Length;
            while (trailOps > leadOps && "+-*/·×⋅ ".IndexOf(fragment[trailOps - 1]) >= 0) trailOps--;
            string leading = fragment.Substring(0, leadOps);
            string core = fragment.Substring(leadOps, trailOps - leadOps);
            string trailing = fragment.Substring(trailOps);
            // Reemplazar '*' por '·' para visualización
            string DispOps(string s) => s.Replace("*", "·");
            if (string.IsNullOrEmpty(core))
                return DispOps(leading) + DispOps(trailing);
            string coreHtml;
            try
            {
                _parser.Parse(core, false);
                coreHtml = _parser.ToHtml();
                if (string.IsNullOrWhiteSpace(coreHtml))
                    coreHtml = DeqRenderVar(core);
            }
            catch
            {
                coreHtml = DeqRenderVar(core);
            }
            return DispOps(leading) + coreHtml + DispOps(trailing);
        }

        private static int FindMatchingParen(string s, int openIdx)
        {
            int depth = 0;
            for (int i = openIdx; i < s.Length; i++)
            {
                if (s[i] == '(') depth++;
                else if (s[i] == ')') { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        private static List<string> SplitTopLevelBySemicolon(string s)
        {
            var parts = new List<string>();
            int depth = 0;
            int start = 0;
            for (int i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c == '(' || c == '[') depth++;
                else if (c == ')' || c == ']') depth--;
                else if (c == ';' && depth == 0)
                {
                    parts.Add(s.Substring(start, i - start));
                    start = i + 1;
                }
            }
            if (start < s.Length) parts.Add(s.Substring(start));
            return parts;
        }

        /// <summary>Normalize Unicode math operators to ASCII so the parser accepts them.</summary>
        private static string NormalizeOps(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace('·', '*').Replace('×', '*').Replace('⋅', '*');
        }

        /// <summary>
        /// Render a decorative scalar expression into HTML, recursively handling
        /// fractions (top-level '/'), multiplication (top-level '·'/'*'),
        /// exponents ('^'), parentheses, and variables with subscripts.
        /// Does NOT evaluate — pure decorative output for #deq/#sym/#blk.
        /// Produces the same HTML structure as Calcpad native output:
        ///   fraction: <span class="dvc">num<span class="dvl"></span>den</span>
        ///   mul:      a · b (middle-dot glue)
        ///   exp:      a<sup>n</sup>
        /// </summary>
        [ThreadStatic] private static int _renderDeqScalarDepth;
        private string RenderDeqScalar(string expr)
        {
            if (string.IsNullOrWhiteSpace(expr)) return "";
            if (++_renderDeqScalarDepth > 32)
            {
                _renderDeqScalarDepth--;
                return DeqRenderVar(expr);
            }
            try
            {
                return RenderDeqScalarInner(expr);
            }
            finally { _renderDeqScalarDepth--; }
        }

        /// <summary>Like RenderDeqScalar but skips the outer-paren-strip step
        /// so the caller can render an already-unwrapped expression. Used to
        /// render the content INSIDE preserved parens.</summary>
        private string RenderDeqScalarRaw(string expr)
        {
            if (string.IsNullOrWhiteSpace(expr)) return "";
            if (++_renderDeqScalarDepth > 32)
            {
                _renderDeqScalarDepth--;
                return DeqRenderVar(expr);
            }
            try
            {
                return RenderDeqScalarNoStrip(expr);
            }
            finally { _renderDeqScalarDepth--; }
        }

        private string RenderDeqScalarNoStrip(string expr)
        {
            // Copy of RenderDeqScalarInner but without the outer-paren strip.
            // Top-level add/sub
            if (HasTopLevelAddSub(expr))
            {
                var sb = new System.Text.StringBuilder();
                int depth = 0; int start = 0;
                for (int i = 0; i < expr.Length; i++)
                {
                    var c = expr[i];
                    if (c == '(' || c == '[') depth++;
                    else if (c == ')' || c == ']') depth--;
                    else if ((c == '+' || c == '-') && depth == 0 && i > 0
                             && expr[i - 1] != 'e' && expr[i - 1] != 'E')
                    {
                        var term = expr.Substring(start, i - start).Trim();
                        if (term.Length > 0) sb.Append(RenderDeqScalar(term));
                        sb.Append(c == '+' ? " + " : " &minus; ");
                        start = i + 1;
                    }
                }
                var last = expr.Substring(start).Trim();
                if (last.Length > 0) sb.Append(RenderDeqScalar(last));
                return sb.ToString();
            }
            return RenderDeqScalar(expr);
        }

        private string RenderDeqScalarInner(string expr)
        {
            expr = expr.Trim();
            // Special patterns (partial/total derivatives, primes, integrals, matrix)
            var sp = TryRenderDeqSpecial(expr);
            if (!string.IsNullOrEmpty(sp)) return sp;

            // Strip outer parens in-place, BUT only if doing so won't change
            // the semantic precedence. We keep the parens when the inner
            // expression has top-level '+' or '-' AND is used as a factor
            // in a larger expression (caller can wrap). Here we just strip
            // always, but later we re-add parens if needed at the caller
            // level (see multiplication split below).
            bool hadOuterParens = false;
            while (expr.Length > 1 && expr[0] == '(' && expr[^1] == ')')
            {
                int dp = 0;
                bool ok = true;
                for (int i = 0; i < expr.Length - 1; i++)
                {
                    if (expr[i] == '(') dp++;
                    else if (expr[i] == ')') { dp--; if (dp == 0) { ok = false; break; } }
                }
                if (ok)
                {
                    expr = expr.Substring(1, expr.Length - 2).Trim();
                    hadOuterParens = true;
                }
                else break;
            }
            // If we stripped outer parens AND the inner expression still has
            // top-level '+' or '-', restore the parens before rendering so
            // the grouping is preserved in output (e.g., "12·(1-ν²)").
            if (hadOuterParens && HasTopLevelAddSub(expr))
            {
                return $"&nbsp;(&nbsp;{RenderDeqScalarRaw(expr)}&nbsp;)&nbsp;";
            }

            // 1. Top-level addition/subtraction → render terms and join
            if (HasTopLevelAddSub(expr))
            {
                var sb = new System.Text.StringBuilder();
                int depth = 0; int start = 0;
                for (int i = 0; i < expr.Length; i++)
                {
                    var c = expr[i];
                    if (c == '(' || c == '[') depth++;
                    else if (c == ')' || c == ']') depth--;
                    else if ((c == '+' || c == '-') && depth == 0 && i > 0
                             && expr[i - 1] != 'e' && expr[i - 1] != 'E')
                    {
                        var term = expr.Substring(start, i - start).Trim();
                        if (term.Length > 0) sb.Append(RenderDeqScalar(term));
                        sb.Append(c == '+' ? " + " : " &minus; ");
                        start = i + 1;
                    }
                }
                var last = expr.Substring(start).Trim();
                if (last.Length > 0) sb.Append(RenderDeqScalar(last));
                return sb.ToString();
            }

            // 2. Top-level division (fraction) → <span class="dvc">num dvl den</span>
            var slashIdx = FindTopLevelChar(expr, '/');
            if (slashIdx > 0)
            {
                var num = expr.Substring(0, slashIdx).Trim();
                var den = expr.Substring(slashIdx + 1).Trim();
                return $"<span class=\"dvc\">{RenderDeqScalar(num)}<span class=\"dvl\"></span>{RenderDeqScalar(den)}</span>";
            }

            // 3. Top-level multiplication → a · b · c
            //    Also handles IMPLICIT multiplication like "12(1-v^2)" or
            //    "(a+b)(c+d)" → inserts the '·' at the boundary so each side
            //    is rendered separately and joined with middle-dot.
            if (expr.IndexOfAny(new[] { '·', '*', '×', '⋅' }) >= 0
                || System.Text.RegularExpressions.Regex.IsMatch(expr, @"[\w\)]\s*\(|\)\s*[a-zA-Zα-ωΑ-Ω_]"))
            {
                var parts = new List<string>();
                int depth = 0; int start = 0;
                for (int i = 0; i < expr.Length; i++)
                {
                    var c = expr[i];
                    if (c == '(' || c == '[')
                    {
                        // Implicit multiplication: digit/letter/close-paren followed by '('
                        if (c == '(' && depth == 0 && i > 0)
                        {
                            var prev = expr[i - 1];
                            if (char.IsDigit(prev) || char.IsLetter(prev) || prev == ')' || prev == ']')
                            {
                                var chunk = expr.Substring(start, i - start).Trim();
                                if (chunk.Length > 0) parts.Add(chunk);
                                start = i;
                            }
                        }
                        depth++;
                    }
                    else if (c == ')' || c == ']') depth--;
                    else if (depth == 0 && (c == '·' || c == '*' || c == '×' || c == '⋅'))
                    {
                        parts.Add(expr.Substring(start, i - start).Trim());
                        start = i + 1;
                    }
                    // Implicit multiplication: ')' followed by letter (new factor starts)
                    else if (c != ' ' && depth == 0 && i > 0 && expr[i - 1] == ')'
                             && (char.IsLetter(c) || char.IsDigit(c)))
                    {
                        var chunk = expr.Substring(start, i - start).Trim();
                        if (chunk.Length > 0) parts.Add(chunk);
                        start = i;
                    }
                }
                parts.Add(expr.Substring(start).Trim());
                if (parts.Count > 1)
                    return string.Join(" · ", parts.Where(p => p.Length > 0).Select(RenderDeqScalar));
            }

            // 4. Exponent: base^exponent → base<sup>exp</sup>
            var caretIdx = FindTopLevelChar(expr, '^');
            if (caretIdx > 0)
            {
                var baseExpr = expr.Substring(0, caretIdx).Trim();
                var expExpr = expr.Substring(caretIdx + 1).Trim();
                return $"{RenderDeqScalar(baseExpr)}<sup>{RenderDeqScalar(expExpr)}</sup>";
            }

            // 5. Leaf — try parser, else DeqRenderVar
            try
            {
                _parser.Parse(NormalizeOps(expr), false);
                var html = _parser.ToHtml();
                if (!string.IsNullOrWhiteSpace(html)) return html;
            }
            catch { }
            return DeqRenderVar(expr);
        }

        /// <summary>Find the index of the first char 'c' at top level (depth=0), or -1.</summary>
        private static int FindTopLevelChar(string s, char c)
        {
            int depth = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '(' || s[i] == '[') depth++;
                else if (s[i] == ')' || s[i] == ']') depth--;
                else if (s[i] == c && depth == 0) return i;
            }
            return -1;
        }

        /// <summary>Strip redundant outer parentheses "(expr)" → "expr" when the
        /// opening '(' matches with the closing ')' at the end.</summary>
        private static string StripOuterParens(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Trim();
            while (s.Length > 1 && s[0] == '(' && s[^1] == ')')
            {
                int depth = 0;
                bool matches = true;
                for (int i = 0; i < s.Length - 1; i++)
                {
                    if (s[i] == '(') depth++;
                    else if (s[i] == ')') { depth--; if (depth == 0) { matches = false; break; } }
                }
                if (matches && depth == 1) s = s.Substring(1, s.Length - 2).Trim();
                else break;
            }
            return s;
        }

        /// <summary>
        /// Render a multiplicative expression "A · B · C" where any factor can
        /// be a partial derivative (∂f/∂x), a total derivative (df/dx), a
        /// variable with subscript (u_i), or a scalar. Joins factors with
        /// a middle-dot operator.
        /// </summary>
        private string RenderMultiplicativeFactors(string term)
        {
            // Split by top-level '·', '*', '×', '⋅' respecting parentheses.
            var factors = new List<string>();
            int depth = 0, start = 0;
            for (int i = 0; i < term.Length; i++)
            {
                var c = term[i];
                if (c == '(' || c == '[') depth++;
                else if (c == ')' || c == ']') depth--;
                else if (depth == 0 && (c == '·' || c == '*' || c == '×' || c == '⋅'))
                {
                    factors.Add(term.Substring(start, i - start).Trim());
                    start = i + 1;
                }
            }
            factors.Add(term.Substring(start).Trim());
            if (factors.Count < 2) return null;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < factors.Count; i++)
            {
                if (i > 0) sb.Append(" · ");
                var f = factors[i];
                if (string.IsNullOrEmpty(f)) { sb.Append(f); continue; }
                // Try the special renderer first (derivatives, primes, etc.)
                var sp = TryRenderDeqSpecial(f);
                if (!string.IsNullOrEmpty(sp)) { sb.Append(sp); continue; }
                // Parser fallback (normalize Unicode mul chars first)
                try
                {
                    _parser.Parse(NormalizeOps(f), false);
                    var html = _parser.ToHtml();
                    if (!string.IsNullOrWhiteSpace(html)) { sb.Append(html); continue; }
                }
                catch { }
                // Last resort: format as variable with subscript handling
                sb.Append(DeqRenderVar(f));
            }
            return sb.ToString();
        }

        /// <summary>True if s contains a '+' or '-' at top-level (outside [](), depth=0) past position 0.</summary>
        /// <remarks>
        /// FIX: el loop arrancaba en i=1 (para saltar el signo unario inicial
        /// como "-x + y") pero también saltaba el '(' inicial de "(1 - ξ)/2",
        /// dejando depth=0 cuando se procesaba el '-'. Resultado: reportaba
        /// top-level add/sub falso y RenderDeqScalar entraba en recursión
        /// infinita hasta el depth-guard que caía a DeqRenderVar — el RHS
        /// quedaba como "&lt;var&gt;(1 - ξ)/2&lt;/var&gt;" en texto plano.
        /// Ahora contamos depth desde i=0 y solo NO-evaluamos el carácter
        /// como operador cuando es signo unario (+/-) en posición 0.
        /// </remarks>
        private static bool HasTopLevelAddSub(string s)
        {
            int depth = 0;
            for (int i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c == '(' || c == '[') { depth++; continue; }
                if (c == ')' || c == ']') { depth--; continue; }
                // i==0 con '+' o '-' es signo unario, no operador top-level
                if (i == 0 && (c == '+' || c == '-')) continue;
                if ((c == '+' || c == '-') && depth == 0)
                {
                    // Not an exponent sign like 1e-3
                    if (i > 0 && (s[i - 1] == 'e' || s[i - 1] == 'E')) continue;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Render an expression with top-level +/- separating terms that may
        /// include a partial/total derivative (∂f/∂x or df/dx or d^2v/dx^2).
        /// Splits the expression, renders each term through TryRenderDeqSpecial
        /// (or parser fallback) and re-joins with the original operators.
        /// </summary>
        private string TryRenderMultiTermDerivative(string expr)
        {
            // Split into tokens with their preceding operator (+/-).
            var tokens = new List<(string op, string term)>();
            int depth = 0;
            int start = 0;
            string curOp = "+";
            if (expr.Length > 0 && expr[0] == '-') { curOp = "-"; start = 1; }
            else if (expr.Length > 0 && expr[0] == '+') { curOp = "+"; start = 1; }
            for (int i = start; i < expr.Length; i++)
            {
                var c = expr[i];
                if (c == '(' || c == '[') depth++;
                else if (c == ')' || c == ']') depth--;
                else if ((c == '+' || c == '-') && depth == 0
                         && i > start
                         && !(i > 0 && (expr[i - 1] == 'e' || expr[i - 1] == 'E')))
                {
                    tokens.Add((curOp, expr.Substring(start, i - start).Trim()));
                    curOp = c.ToString();
                    start = i + 1;
                }
            }
            if (start < expr.Length)
                tokens.Add((curOp, expr.Substring(start).Trim()));

            if (tokens.Count == 0) return null;

            var sb = new System.Text.StringBuilder();
            bool anyDeriv = false;
            for (int k = 0; k < tokens.Count; k++)
            {
                var (op, term) = tokens[k];
                // Render operator prefix (except leading '+' which we omit)
                if (k == 0)
                {
                    if (op == "-") sb.Append("&minus;");
                }
                else
                {
                    sb.Append(op == "-" ? " &minus; " : " + ");
                }
                // Render the term — try derivative pattern first, then multiplicative
                // factor rendering, then parser fallback.
                string termHtml = null;
                var spHtml = TryRenderDeqSpecial(term);
                if (!string.IsNullOrEmpty(spHtml))
                {
                    termHtml = spHtml;
                    if (term.Contains('∂') || (term.StartsWith("d") && term.Contains("/d")))
                        anyDeriv = true;
                }
                else if (term.Contains('∂') || term.Contains("d/d")
                         || System.Text.RegularExpressions.Regex.IsMatch(term,
                            @"[d∂](?:\^?\d+)?[a-zA-Zα-ωΑ-Ω_][a-zA-Zα-ωΑ-Ω0-9_]*\s*/\s*[d∂][a-zA-Zα-ωΑ-Ω_]"))
                {
                    // Term has a derivative + multiplicative factors: split by ·/*
                    termHtml = RenderMultiplicativeFactors(term);
                    anyDeriv = true;
                }
                else
                {
                    try
                    {
                        _parser.Parse(NormalizeOps(term), false);
                        termHtml = _parser.ToHtml();
                    }
                    catch { }
                    if (string.IsNullOrWhiteSpace(termHtml))
                        termHtml = DeqRenderVar(term);
                }
                sb.Append(termHtml);
            }
            return anyDeriv ? sb.ToString() : null;
        }

        /// <summary>Find the index of the first top-level '=' (outside [](), depth=0), or -1.</summary>
        private static int FindTopLevelEquals(string s)
        {
            int depth = 0;
            for (int i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c == '(' || c == '[') depth++;
                else if (c == ')' || c == ']') depth--;
                else if (c == '=' && depth == 0) return i;
            }
            return -1;
        }

        /// <summary>Split a string by a single top-level delimiter (outside [](), depth=0).</summary>
        private static List<string> SplitTopLevelByChar(string s, char delim)
        {
            var parts = new List<string>();
            int depth = 0;
            int start = 0;
            for (int i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c == '(' || c == '[') depth++;
                else if (c == ')' || c == ']') depth--;
                else if (c == delim && depth == 0)
                {
                    parts.Add(s.Substring(start, i - start));
                    start = i + 1;
                }
            }
            if (start <= s.Length) parts.Add(s.Substring(start));
            return parts;
        }

        /// <summary>
        /// Render a matrix literal of the form "[row1 | row2 | row3]" where each
        /// row has cells separated by ',' or ';'. Each cell is recursively
        /// rendered through the special-renderer (so partial derivatives and
        /// primes inside the matrix still work), then through the parser
        /// (fallback), then through DeqRenderVar (last resort).
        /// Wraps the resulting grid in large square brackets.
        /// </summary>
        private string TryRenderMatrixLiteral(string expr)
        {
            if (string.IsNullOrEmpty(expr) || expr.Length < 3) return null;
            if (expr[0] != '[' || expr[^1] != ']') return null;
            var inner = expr.Substring(1, expr.Length - 2).Trim();
            if (string.IsNullOrEmpty(inner)) return null;
            var rows = SplitTopLevelByChar(inner, '|');
            // Accept 1-row matrices (no '|') ONLY if they contain pdiff()/
            // diff()/integrate() — we want to render those operators as
            // math notation. Plain 1-row vectors [a; b; c] are still handled
            // by the normal parser.
            if (rows.Count < 2 &&
                !inner.Contains("pdiff(") &&
                !inner.Contains("diff(") &&
                !inner.Contains("integrate("))
                return null;

            // Split each row by ',' or ';' (whichever yields more cells)
            var cells = new List<List<string>>();
            int maxCols = 0;
            foreach (var row in rows)
            {
                var byComma = SplitTopLevelByChar(row, ',');
                var bySemi  = SplitTopLevelByChar(row, ';');
                var chosen = byComma.Count >= bySemi.Count ? byComma : bySemi;
                var trimmed = chosen.Select(c => c.Trim()).ToList();
                cells.Add(trimmed);
                if (trimmed.Count > maxCols) maxCols = trimmed.Count;
            }

            // Render each cell as HTML
            string renderCell(string cellExpr)
            {
                if (string.IsNullOrWhiteSpace(cellExpr)) return "&nbsp;";
                // Try special render first (partials, primes, integrals)
                var sp = TryRenderDeqSpecial(cellExpr.Trim());
                if (!string.IsNullOrEmpty(sp)) return sp;
                // Fall back to parser
                try
                {
                    _parser.Parse(cellExpr.Trim(), false);
                    var html = _parser.ToHtml();
                    if (!string.IsNullOrWhiteSpace(html)) return html;
                }
                catch { /* fall through */ }
                return DeqRenderVar(cellExpr.Trim());
            }

            // Build HTML using the SAME <span class="matrix"> pattern that
            // native Calcpad uses for computed matrices. The existing
            // stylesheet already renders proper tall square brackets via
            // the empty first/last <span class="td"></span> cells combined
            // with CSS pseudo-elements — so our decorative matrices look
            // IDENTICAL to native ones, align perfectly inline with other
            // expressions, and don't force line breaks in the paragraph.
            var sb = new System.Text.StringBuilder();
            sb.Append("<span class=\"matrix matwrap\">");
            foreach (var row in cells)
            {
                sb.Append("<span class=\"tr\"><span class=\"td\"></span>");
                for (int c = 0; c < maxCols; c++)
                {
                    var cell = c < row.Count ? row[c] : "";
                    sb.Append("<span class=\"td\">");
                    sb.Append(renderCell(cell));
                    sb.Append("</span>");
                }
                sb.Append("<span class=\"td\"></span></span>");
            }
            sb.Append("</span>");
            return sb.ToString();
        }

        /// <summary>Render a variable name with subscript support for #deq</summary>
        private static string DeqRenderVar(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var i = name.IndexOf('_');
            if (i > 0 && i + 1 < name.Length)
            {
                var main = name[..i];
                var sub = name[(i + 1)..];
                // Handle {} braces in subscript: v_{max} → v<sub>max</sub>
                if (sub.StartsWith("{") && sub.EndsWith("}"))
                    sub = sub[1..^1];
                return $"<var>{main}</var><sub>{sub}</sub>";
            }
            return $"<var>{name}</var>";
        }

        /// <summary>Escape d or ∂ for HTML rendering in derivatives</summary>
        private static string EscapeDeqChar(char c) => c switch
        {
            '∂' or '\u2202' => "∂",
            _ => c.ToString()
        };

        /// <summary>
        /// Detecta si la cadena es una llamada a función "literal" tipo
        /// <c>f(x; y)</c>, <c>N_1(ξ; η)</c>, <c>v_x(t)</c>. Renderiza como
        /// función display con argumentos, sin tratar de evaluar.
        /// Retorna null si no matchea el patrón.
        /// </summary>
        /// <remarks>
        /// El patrón es: identificador (opcionalmente con _subscript)
        /// seguido de '(' + lista de args separados por ; o , + ')',
        /// y el string completo termina en ')'. No matchea expresiones
        /// como <c>(1+ξ)(1-η)</c> ni <c>N_1(ξ)·x_1 + ...</c> porque ahí
        /// el primer carácter no es una letra y/o hay tokens después
        /// del paréntesis cierre.
        /// </remarks>
        private static string TryRenderFunctionCallSignature(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            // Regex: id (con sub) ( contenido sin paréntesis anidados )
            // OJO: esto solo cubre el caso simple — sin paréntesis anidados
            // dentro de los argumentos. Casos como f(g(x)) no matchearían.
            var m = System.Text.RegularExpressions.Regex.Match(
                s.Trim(),
                @"^([a-zA-Zα-ωΑ-Ω][a-zA-Zα-ωΑ-Ω0-9_]*)\s*\(\s*([^()]+?)\s*\)$");
            if (!m.Success) return null;

            var fnName = m.Groups[1].Value;
            var argsRaw = m.Groups[2].Value;

            // Args separados por ; o ,
            var argList = argsRaw.Split(new[] { ';', ',' },
                StringSplitOptions.RemoveEmptyEntries);

            var fnHtml = DeqRenderVar(fnName);
            // Cada argumento se renderiza por separado con DeqRenderVar
            // (soporta letras griegas, subscripts).
            var argsHtml = string.Join(", ",
                argList.Select(a => DeqRenderVar(a.Trim())));

            // Notar: el separador visual de argumentos en notación
            // matemática estándar es la coma (no el punto y coma).
            // Calcpad usa ';' por sintaxis interna pero al renderizar
            // queda más natural mostrar ',' (como N_1(ξ, η) en libros).
            return $"{fnHtml}({argsHtml})";
        }

        // ─── #svg W H / #end svg — Inline SVG drawing block ──────────────────
        // Lines starting with . are SVG primitives; other lines evaluate normally
        // (variables, #for, #if, math) so flow control works inside #svg blocks.
        //
        // Syntax: #svg 600 400
        //   .rect 0 0 100 50 green 0.2
        //   .circle 50 50 20 blue
        //   .line 0 0 100 100 red 2
        //   .text 50 80 Hello World
        //   .arrow 10 10 90 10 black
        //   .arc cx cy r startAngle endAngle color strokeWidth
        //   .ellipse cx cy rx ry color opacity
        //   .polyline x1 y1 x2 y2 x3 y3 ... color strokeWidth
        //   .polygon x1 y1 x2 y2 x3 y3 ... color opacity
        //   .dashed x1 y1 x2 y2 color strokeWidth dashLength
        //   #for i = 1 : 5
        //     .circle i*80 100 10 red
        //   #loop
        // #end svg

        private bool _insideSvgBlock;
        private int _svgWidth;
        private int _svgHeight;
        private System.Text.StringBuilder _svgBuffer;
        private bool _svgSavedVisible;
        internal int _svgSbPositionBeforeLine = -1;

        private void ParseKeywordSvg(ReadOnlySpan<char> s)
        {
            // Parse: #svg W H  or  #svg 600 400
            var text = s.ToString().Trim();
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            _svgWidth = parts.Length > 1 ? (int)EvalSvgExpr(parts[1]) : 600;
            _svgHeight = parts.Length > 2 ? (int)EvalSvgExpr(parts[2]) : 400;
            _insideSvgBlock = true;
            _svgBuffer = new System.Text.StringBuilder(2048);
            _svgSavedVisible = _isVisible;
            _svgSbPositionBeforeLine = -1;
        }

        private void ParseKeywordEndSvg()
        {
            if (!_insideSvgBlock || _svgBuffer == null)
            {
                AppendError("#end svg", "No matching #svg", _currentLine);
                return;
            }
            _insideSvgBlock = false;
            _svgSbPositionBeforeLine = -1;

            if (_svgSavedVisible)
            {
                var svg = $"<svg viewBox=\"0 0 {_svgWidth} {_svgHeight}\" " +
                    $"width=\"{_svgWidth}\" height=\"{_svgHeight}\" " +
                    $"xmlns=\"http://www.w3.org/2000/svg\" " +
                    $"style=\"font-family:'Segoe UI',Arial,sans-serif;font-size:12px\">" +
                    $"{_svgBuffer}</svg>";
                _sb.Append($"<div{HtmlId}>{svg}</div>\n");
            }
            _svgBuffer = null;
        }

        /// <summary>Process a SVG primitive line like ".rect 0 0 100 50 green 0.2"</summary>
        private void ProcessSvgPrimitive(string line)
        {
            if (_svgBuffer == null || !_svgSavedVisible) return;

            // Split: .command arg1 arg2 arg3 ...
            // But text after last numeric arg is treated as text content
            var trimmed = line.TrimStart();
            if (trimmed.Length < 2 || trimmed[0] != '.') return;

            var spaceIdx = trimmed.IndexOf(' ');
            if (spaceIdx < 0) return;

            var cmd = trimmed[1..spaceIdx].ToLowerInvariant();
            var argsStr = trimmed[(spaceIdx + 1)..].Trim();

            switch (cmd)
            {
                case "line":
                    SvgLine(argsStr);
                    break;
                case "dashed":
                    SvgDashed(argsStr);
                    break;
                case "rect":
                    SvgRect(argsStr);
                    break;
                case "circle":
                    SvgCircle(argsStr);
                    break;
                case "ellipse":
                    SvgEllipse(argsStr);
                    break;
                case "text":
                    SvgText(argsStr);
                    break;
                case "arrow":
                    SvgArrow(argsStr);
                    break;
                case "arc":
                    SvgArc(argsStr);
                    break;
                case "polyline":
                    SvgPolyline(argsStr);
                    break;
                case "polygon":
                    SvgPolygon(argsStr);
                    break;
                case "style":
                    break;
                // ── CAD commands from CadCli ──
                case "dim": case "cota":
                    SvgDim(argsStr); break;
                case "hdim": case "cotah":
                    SvgHDim(argsStr); break;
                case "vdim": case "cotav":
                    SvgVDim(argsStr); break;
                case "darrow": case "flechadoble":
                    SvgDArrow(argsStr); break;
                case "beam": case "viga":
                    SvgBeam(argsStr); break;
                case "axes": case "ejes":
                    SvgAxes(argsStr); break;
                case "cnode": case "cn": case "cid":
                    SvgCNode(argsStr); break;
                case "tnode": case "tn": case "tid":
                    SvgTNode(argsStr); break;
                case "support": case "apoyo":
                    SvgSupport(argsStr); break;
                case "moment": case "giro":
                    SvgMoment(argsStr); break;
                case "hatch": case "rayado":
                    SvgHatch(argsStr); break;
                case "fillrect":
                    SvgFillRect(argsStr); break;
                // ── Compound/preset figures ──
                case "angle": case "angulo":
                    SvgAngle(argsStr); break;
                case "radian":
                    SvgRadian(argsStr); break;
                case "spring": case "resorte":
                    SvgSpring(argsStr); break;
                case "grid": case "cuadricula":
                    SvgGrid(argsStr); break;
                case "curvedarrow": case "flechacurva":
                    SvgCurvedArrow(argsStr); break;
                case "color":
                    _svgCurrentColor = SvgSplitArgs(argsStr)[0]; break;
                case "lw": case "linewidth":
                    _svgCurrentLw = SvgNum(SvgSplitArgs(argsStr)[0]); break;
                case "fontsize": case "fs":
                    _svgCurrentFontSize = SvgNum(SvgSplitArgs(argsStr)[0]); break;
            }
        }

        // SVG state
        private string _svgCurrentColor = "black";
        private string _svgCurrentLw = "1.5";
        private string _svgCurrentFontSize = "12";

        // Parse args, evaluating Calcpad expressions for numeric values
        private string[] SvgSplitArgs(string s)
        {
            // Split por espacios pero RESPETANDO comillas dobles, así el
            // user puede pasar  .text 100 50 "varias palabras juntas" 14 ...
            // como un solo token. Antes split('') las rompía en 3 tokens
            // y los textos quedaban como "0" porque "varias se evaluaba
            // como expresión sin matchear y devolvía 0.
            var parts = new System.Collections.Generic.List<string>();
            var cur = new System.Text.StringBuilder();
            bool inQuotes = false;
            foreach (var c in s)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    cur.Append(c);     // mantener las comillas (SvgText las usa de marker)
                }
                else if (c == ' ' && !inQuotes)
                {
                    if (cur.Length > 0)
                    {
                        parts.Add(cur.ToString());
                        cur.Clear();
                    }
                }
                else
                    cur.Append(c);
            }
            if (cur.Length > 0)
                parts.Add(cur.ToString());
            return parts.ToArray();
        }

        private double EvalSvgExpr(string expr)
        {
            try
            {
                _parser.Parse(expr.Trim());
                _parser.Calculate(false, -1);
                var rv = _parser.ResultValue;
                return rv is IScalarValue sv ? sv.Re : 0;
            }
            catch { return 0; }
        }

        private string SvgNum(string expr)
        {
            var val = EvalSvgExpr(expr);
            return val.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        // .line x1 y1 x2 y2 [color] [strokeWidth]
        private void SvgLine(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 4) return;
            var color = p.Length > 4 ? p[4] : "black";
            var sw = p.Length > 5 ? SvgNum(p[5]) : "1.5";
            _svgBuffer.Append($"<line x1=\"{SvgNum(p[0])}\" y1=\"{SvgNum(p[1])}\" " +
                $"x2=\"{SvgNum(p[2])}\" y2=\"{SvgNum(p[3])}\" " +
                $"stroke=\"{color}\" stroke-width=\"{sw}\"/>");
        }

        // .dashed x1 y1 x2 y2 [color] [strokeWidth] [dashLen]
        private void SvgDashed(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 4) return;
            var color = p.Length > 4 ? p[4] : "gray";
            var sw = p.Length > 5 ? SvgNum(p[5]) : "1";
            var dash = p.Length > 6 ? SvgNum(p[6]) : "5";
            _svgBuffer.Append($"<line x1=\"{SvgNum(p[0])}\" y1=\"{SvgNum(p[1])}\" " +
                $"x2=\"{SvgNum(p[2])}\" y2=\"{SvgNum(p[3])}\" " +
                $"stroke=\"{color}\" stroke-width=\"{sw}\" stroke-dasharray=\"{dash}\"/>");
        }

        // .rect x y w h [color] [opacity] [strokeColor] [strokeWidth]
        private void SvgRect(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 4) return;
            var fill = p.Length > 4 ? p[4] : "none";
            var opacity = p.Length > 5 ? SvgNum(p[5]) : "1";
            var stroke = p.Length > 6 ? p[6] : "black";
            var sw = p.Length > 7 ? SvgNum(p[7]) : "1";
            _svgBuffer.Append($"<rect x=\"{SvgNum(p[0])}\" y=\"{SvgNum(p[1])}\" " +
                $"width=\"{SvgNum(p[2])}\" height=\"{SvgNum(p[3])}\" " +
                $"fill=\"{fill}\" fill-opacity=\"{opacity}\" stroke=\"{stroke}\" stroke-width=\"{sw}\"/>");
        }

        // .circle cx cy r [color] [opacity] [strokeColor] [strokeWidth]
        private void SvgCircle(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 3) return;
            var fill = p.Length > 3 ? p[3] : "black";
            var opacity = p.Length > 4 ? SvgNum(p[4]) : "1";
            var stroke = p.Length > 5 ? p[5] : "none";
            var sw = p.Length > 6 ? SvgNum(p[6]) : "1";
            _svgBuffer.Append($"<circle cx=\"{SvgNum(p[0])}\" cy=\"{SvgNum(p[1])}\" r=\"{SvgNum(p[2])}\" " +
                $"fill=\"{fill}\" fill-opacity=\"{opacity}\" stroke=\"{stroke}\" stroke-width=\"{sw}\"/>");
        }

        // .ellipse cx cy rx ry [color] [opacity]
        private void SvgEllipse(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 4) return;
            var fill = p.Length > 4 ? p[4] : "black";
            var opacity = p.Length > 5 ? SvgNum(p[5]) : "1";
            _svgBuffer.Append($"<ellipse cx=\"{SvgNum(p[0])}\" cy=\"{SvgNum(p[1])}\" " +
                $"rx=\"{SvgNum(p[2])}\" ry=\"{SvgNum(p[3])}\" " +
                $"fill=\"{fill}\" fill-opacity=\"{opacity}\"/>");
        }

        // .text x y content [fontSize] [color] [anchor] [fontWeight]
        // Content uses _ for spaces. If content starts with a letter, it's literal text.
        // If it's a pure number or arithmetic expression, it evaluates.
        // Use "quotes" for text that might conflict with variable names.
        private void SvgText(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 3) return;
            var x = SvgNum(p[0]);
            var y = SvgNum(p[1]);

            string textContent;
            string fontSize = "12";
            string color = "black";
            string anchor = "middle";
            string weight = "normal";

            var rawText = p[2];
            // If text is in "quotes", use literal (remove quotes)
            if (rawText.StartsWith("\"") && rawText.EndsWith("\""))
                textContent = rawText[1..^1].Replace('_', ' ');
            else
            {
                // Try to evaluate as expression (handles variables like i, e, j, x+1, etc.)
                try
                {
                    var val = EvalSvgExpr(rawText);
                    textContent = Math.Round(val, 6).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    // Clean up: 3.000000 → 3
                    if (textContent.Contains('.'))
                        textContent = textContent.TrimEnd('0').TrimEnd('.');
                }
                catch
                {
                    // If evaluation fails, treat as literal text
                    textContent = rawText.Replace('_', ' ');
                }
            }

            if (p.Length > 3) fontSize = SvgNum(p[3]);
            if (p.Length > 4) color = p[4];
            if (p.Length > 5) anchor = p[5];
            if (p.Length > 6) weight = p[6];

            _svgBuffer.Append($"<text x=\"{x}\" y=\"{y}\" text-anchor=\"{anchor}\" " +
                $"fill=\"{color}\" font-size=\"{fontSize}\" font-weight=\"{weight}\">" +
                $"{System.Web.HttpUtility.HtmlEncode(textContent)}</text>");
        }

        // .arrow x1 y1 x2 y2 [color] [strokeWidth]
        private void SvgArrow(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 4) return;
            var color = p.Length > 4 ? p[4] : "black";
            var sw = p.Length > 5 ? SvgNum(p[5]) : "1.5";

            double x1 = EvalSvgExpr(p[0]), y1 = EvalSvgExpr(p[1]);
            double x2 = EvalSvgExpr(p[2]), y2 = EvalSvgExpr(p[3]);
            double dx = x2 - x1, dy = y2 - y1;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.001) return;
            double ux = dx / len, uy = dy / len;
            double headLen = Math.Min(12, len * 0.3);
            // Arrow head: two lines from tip, rotated ±30 degrees
            double cos30 = 0.866, sin30 = 0.5;
            double ax1 = x2 - headLen * (ux * cos30 + uy * sin30);
            double ay1 = y2 - headLen * (-ux * sin30 + uy * cos30);
            double ax2 = x2 - headLen * (ux * cos30 - uy * sin30);
            double ay2 = y2 - headLen * (ux * sin30 + uy * cos30);

            var inv = System.Globalization.CultureInfo.InvariantCulture;
            _svgBuffer.Append($"<line x1=\"{x1.ToString(inv)}\" y1=\"{y1.ToString(inv)}\" " +
                $"x2=\"{x2.ToString(inv)}\" y2=\"{y2.ToString(inv)}\" stroke=\"{color}\" stroke-width=\"{sw}\"/>");
            _svgBuffer.Append($"<polygon points=\"{x2.ToString(inv)},{y2.ToString(inv)} " +
                $"{ax1.ToString(inv)},{ay1.ToString(inv)} {ax2.ToString(inv)},{ay2.ToString(inv)}\" " +
                $"fill=\"{color}\"/>");
        }

        // .arc cx cy r startAngle endAngle [color] [strokeWidth]
        private void SvgArc(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 5) return;
            double cx = EvalSvgExpr(p[0]), cy = EvalSvgExpr(p[1]), r = EvalSvgExpr(p[2]);
            double startDeg = EvalSvgExpr(p[3]), endDeg = EvalSvgExpr(p[4]);
            var color = p.Length > 5 ? p[5] : "black";
            var sw = p.Length > 6 ? SvgNum(p[6]) : "1.5";

            double startRad = startDeg * Math.PI / 180;
            double endRad = endDeg * Math.PI / 180;
            double x1 = cx + r * Math.Cos(startRad), y1 = cy + r * Math.Sin(startRad);
            double x2 = cx + r * Math.Cos(endRad), y2 = cy + r * Math.Sin(endRad);
            int largeArc = Math.Abs(endDeg - startDeg) > 180 ? 1 : 0;

            var inv = System.Globalization.CultureInfo.InvariantCulture;
            _svgBuffer.Append($"<path d=\"M {x1.ToString(inv)} {y1.ToString(inv)} " +
                $"A {r.ToString(inv)} {r.ToString(inv)} 0 {largeArc} 1 " +
                $"{x2.ToString(inv)} {y2.ToString(inv)}\" " +
                $"fill=\"none\" stroke=\"{color}\" stroke-width=\"{sw}\"/>");
        }

        // .polyline x1 y1 x2 y2 ... [color] [strokeWidth]
        private void SvgPolyline(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 4) return;
            // Find where coordinates end and style begins
            var points = new System.Text.StringBuilder();
            int i;
            for (i = 0; i + 1 < p.Length; i += 2)
            {
                if (!double.TryParse(p[i], out _) && EvalSvgExpr(p[i]) == 0) break;
                if (points.Length > 0) points.Append(' ');
                points.Append($"{SvgNum(p[i])},{SvgNum(p[i + 1])}");
            }
            var color = i < p.Length ? p[i] : "black";
            var sw = i + 1 < p.Length ? SvgNum(p[i + 1]) : "1.5";
            _svgBuffer.Append($"<polyline points=\"{points}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{sw}\"/>");
        }

        // .polygon x1 y1 x2 y2 ... [color] [opacity]
        private void SvgPolygon(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 6) return;
            var points = new System.Text.StringBuilder();
            int i;
            for (i = 0; i + 1 < p.Length; i += 2)
            {
                if (!double.TryParse(p[i], out _) && EvalSvgExpr(p[i]) == 0) break;
                if (points.Length > 0) points.Append(' ');
                points.Append($"{SvgNum(p[i])},{SvgNum(p[i + 1])}");
            }
            var color = i < p.Length ? p[i] : "blue";
            var opacity = i + 1 < p.Length ? SvgNum(p[i + 1]) : "0.3";
            _svgBuffer.Append($"<polygon points=\"{points}\" fill=\"{color}\" fill-opacity=\"{opacity}\" stroke=\"{color}\" stroke-width=\"1\"/>");
        }

        // ── CAD command implementations ──────────────────────────────────────

        // .dim x1 y1 x2 y2 offset [text] — diagonal dimension with arrows
        private void SvgDim(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 5) return;
            double x1 = EvalSvgExpr(p[0]), y1 = EvalSvgExpr(p[1]);
            double x2 = EvalSvgExpr(p[2]), y2 = EvalSvgExpr(p[3]);
            double off = EvalSvgExpr(p[4]);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            double dx = x2 - x1, dy = y2 - y1;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.01) return;
            double ux = dx / len, uy = dy / len;
            double nx = -uy * off, ny = ux * off;
            double ax = x1 + nx, ay = y1 + ny, bx = x2 + nx, by = y2 + ny;
            string dimText = p.Length > 5 ? p[5].Replace('_', ' ') : Math.Round(len, 2).ToString(inv);
            // Extension lines
            _svgBuffer.Append($"<line x1=\"{x1.ToString(inv)}\" y1=\"{y1.ToString(inv)}\" x2=\"{ax.ToString(inv)}\" y2=\"{ay.ToString(inv)}\" stroke=\"gray\" stroke-width=\"0.5\"/>");
            _svgBuffer.Append($"<line x1=\"{x2.ToString(inv)}\" y1=\"{y2.ToString(inv)}\" x2=\"{bx.ToString(inv)}\" y2=\"{by.ToString(inv)}\" stroke=\"gray\" stroke-width=\"0.5\"/>");
            // Dimension line with arrows
            SvgDimLine(ax, ay, bx, by, dimText);
        }

        // .hdim x1 y1 x2 y2 offset [text] — horizontal dimension
        private void SvgHDim(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 5) return;
            double x1 = EvalSvgExpr(p[0]), y1 = EvalSvgExpr(p[1]);
            double x2 = EvalSvgExpr(p[2]), y2 = EvalSvgExpr(p[3]);
            double off = EvalSvgExpr(p[4]);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            double ay = y1 - off, by = y2 - off;
            double hLen = Math.Abs(x2 - x1);
            string dimText = p.Length > 5 ? p[5].Replace('_', ' ') : Math.Round(hLen, 2).ToString(inv);
            _svgBuffer.Append($"<line x1=\"{x1.ToString(inv)}\" y1=\"{y1.ToString(inv)}\" x2=\"{x1.ToString(inv)}\" y2=\"{ay.ToString(inv)}\" stroke=\"gray\" stroke-width=\"0.5\"/>");
            _svgBuffer.Append($"<line x1=\"{x2.ToString(inv)}\" y1=\"{y2.ToString(inv)}\" x2=\"{x2.ToString(inv)}\" y2=\"{by.ToString(inv)}\" stroke=\"gray\" stroke-width=\"0.5\"/>");
            SvgDimLine(x1, ay, x2, by, dimText);
        }

        // .vdim x1 y1 x2 y2 offset [text] — vertical dimension
        private void SvgVDim(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 5) return;
            double x1 = EvalSvgExpr(p[0]), y1 = EvalSvgExpr(p[1]);
            double x2 = EvalSvgExpr(p[2]), y2 = EvalSvgExpr(p[3]);
            double off = EvalSvgExpr(p[4]);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            double ax = x1 + off, bx = x2 + off;
            double vLen = Math.Abs(y2 - y1);
            string dimText = p.Length > 5 ? p[5].Replace('_', ' ') : Math.Round(vLen, 2).ToString(inv);
            _svgBuffer.Append($"<line x1=\"{x1.ToString(inv)}\" y1=\"{y1.ToString(inv)}\" x2=\"{ax.ToString(inv)}\" y2=\"{y1.ToString(inv)}\" stroke=\"gray\" stroke-width=\"0.5\"/>");
            _svgBuffer.Append($"<line x1=\"{x2.ToString(inv)}\" y1=\"{y2.ToString(inv)}\" x2=\"{bx.ToString(inv)}\" y2=\"{y2.ToString(inv)}\" stroke=\"gray\" stroke-width=\"0.5\"/>");
            SvgDimLine(ax, y1, bx, y2, dimText);
        }

        // Helper: draw dimension line with arrowheads + centered text
        private void SvgDimLine(double x1, double y1, double x2, double y2, string text)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            double dx = x2 - x1, dy = y2 - y1;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.01) return;
            double ux = dx / len, uy = dy / len;
            double hl = Math.Min(8, len * 0.2);
            double cos30 = 0.866, sin30 = 0.5;
            // Arrow at start (pointing toward x1,y1)
            double a1x = x1 + hl * (ux * cos30 + uy * sin30), a1y = y1 + hl * (-ux * sin30 + uy * cos30);
            double a2x = x1 + hl * (ux * cos30 - uy * sin30), a2y = y1 + hl * (ux * sin30 + uy * cos30);
            // Arrow at end (pointing toward x2,y2)
            double b1x = x2 - hl * (ux * cos30 + uy * sin30), b1y = y2 - hl * (-ux * sin30 + uy * cos30);
            double b2x = x2 - hl * (ux * cos30 - uy * sin30), b2y = y2 - hl * (ux * sin30 + uy * cos30);
            // Line
            _svgBuffer.Append($"<line x1=\"{x1.ToString(inv)}\" y1=\"{y1.ToString(inv)}\" x2=\"{x2.ToString(inv)}\" y2=\"{y2.ToString(inv)}\" stroke=\"{_svgCurrentColor}\" stroke-width=\"0.8\"/>");
            // Arrowheads
            _svgBuffer.Append($"<polygon points=\"{x1.ToString(inv)},{y1.ToString(inv)} {a1x.ToString(inv)},{a1y.ToString(inv)} {a2x.ToString(inv)},{a2y.ToString(inv)}\" fill=\"{_svgCurrentColor}\"/>");
            _svgBuffer.Append($"<polygon points=\"{x2.ToString(inv)},{y2.ToString(inv)} {b1x.ToString(inv)},{b1y.ToString(inv)} {b2x.ToString(inv)},{b2y.ToString(inv)}\" fill=\"{_svgCurrentColor}\"/>");
            // Text
            double mx = (x1 + x2) / 2, my = (y1 + y2) / 2 - 4;
            _svgBuffer.Append($"<text x=\"{mx.ToString(inv)}\" y=\"{my.ToString(inv)}\" text-anchor=\"middle\" fill=\"{_svgCurrentColor}\" font-size=\"11\">{System.Web.HttpUtility.HtmlEncode(text)}</text>");
        }

        // .darrow x1 y1 x2 y2 [color] — double-headed arrow
        private void SvgDArrow(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 4) return;
            var color = p.Length > 4 ? p[4] : _svgCurrentColor;
            double x1 = EvalSvgExpr(p[0]), y1 = EvalSvgExpr(p[1]);
            double x2 = EvalSvgExpr(p[2]), y2 = EvalSvgExpr(p[3]);
            var savedColor = _svgCurrentColor;
            _svgCurrentColor = color;
            SvgDimLine(x1, y1, x2, y2, "");
            _svgCurrentColor = savedColor;
        }

        // .beam x1 y1 x2 y2 [width] [color] — beam with hatching
        private void SvgBeam(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 4) return;
            double x1 = EvalSvgExpr(p[0]), y1 = EvalSvgExpr(p[1]);
            double x2 = EvalSvgExpr(p[2]), y2 = EvalSvgExpr(p[3]);
            double bw = p.Length > 4 ? EvalSvgExpr(p[4]) : 8;
            var color = p.Length > 5 ? p[5] : _svgCurrentColor;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            double dx = x2 - x1, dy = y2 - y1;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.01) return;
            double nx = -dy / len * bw / 2, ny = dx / len * bw / 2;
            // Rectangle outline
            _svgBuffer.Append($"<polygon points=\"{(x1+nx).ToString(inv)},{(y1+ny).ToString(inv)} {(x2+nx).ToString(inv)},{(y2+ny).ToString(inv)} {(x2-nx).ToString(inv)},{(y2-ny).ToString(inv)} {(x1-nx).ToString(inv)},{(y1-ny).ToString(inv)}\" fill=\"white\" stroke=\"{color}\" stroke-width=\"1.5\"/>");
        }

        // .axes x y [size] — coordinate axes
        private void SvgAxes(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 2) return;
            double x = EvalSvgExpr(p[0]), y = EvalSvgExpr(p[1]);
            double sz = p.Length > 2 ? EvalSvgExpr(p[2]) : 40;
            SvgArrow($"{x} {y} {x + sz} {y} {_svgCurrentColor} 1.5");
            SvgArrow($"{x} {y} {x} {y - sz} {_svgCurrentColor} 1.5");
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            _svgBuffer.Append($"<text x=\"{(x + sz + 5).ToString(inv)}\" y=\"{(y + 4).ToString(inv)}\" fill=\"{_svgCurrentColor}\" font-size=\"12\">x</text>");
            _svgBuffer.Append($"<text x=\"{(x - 5).ToString(inv)}\" y=\"{(y - sz - 3).ToString(inv)}\" text-anchor=\"end\" fill=\"{_svgCurrentColor}\" font-size=\"12\">y</text>");
        }

        // .cnode cx cy label [radius] [color] — circle with label inside
        private void SvgCNode(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 3) return;
            double cx = EvalSvgExpr(p[0]), cy = EvalSvgExpr(p[1]);
            string label = p[2].Replace('_', ' ');
            double r = p.Length > 3 ? EvalSvgExpr(p[3]) : 12;
            var color = p.Length > 4 ? p[4] : _svgCurrentColor;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            _svgBuffer.Append($"<circle cx=\"{cx.ToString(inv)}\" cy=\"{cy.ToString(inv)}\" r=\"{r.ToString(inv)}\" fill=\"white\" stroke=\"{color}\" stroke-width=\"1.5\"/>");
            _svgBuffer.Append($"<text x=\"{cx.ToString(inv)}\" y=\"{(cy + 4).ToString(inv)}\" text-anchor=\"middle\" fill=\"{color}\" font-size=\"11\" font-weight=\"bold\">{System.Web.HttpUtility.HtmlEncode(label)}</text>");
        }

        // .tnode cx cy label [size] [color] — triangle with label
        private void SvgTNode(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 3) return;
            double cx = EvalSvgExpr(p[0]), cy = EvalSvgExpr(p[1]);
            string label = p[2].Replace('_', ' ');
            double sz = p.Length > 3 ? EvalSvgExpr(p[3]) : 12;
            var color = p.Length > 4 ? p[4] : _svgCurrentColor;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            double h = sz * 0.866;
            _svgBuffer.Append($"<polygon points=\"{cx.ToString(inv)},{(cy - h * 2 / 3).ToString(inv)} {(cx - sz / 2).ToString(inv)},{(cy + h / 3).ToString(inv)} {(cx + sz / 2).ToString(inv)},{(cy + h / 3).ToString(inv)}\" fill=\"white\" stroke=\"{color}\" stroke-width=\"1.5\"/>");
            _svgBuffer.Append($"<text x=\"{cx.ToString(inv)}\" y=\"{(cy + 2).ToString(inv)}\" text-anchor=\"middle\" fill=\"{color}\" font-size=\"9\" font-weight=\"bold\">{System.Web.HttpUtility.HtmlEncode(label)}</text>");
        }

        // .support x y type — pin/roller/fixed support
        private void SvgSupport(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 3) return;
            double x = EvalSvgExpr(p[0]), y = EvalSvgExpr(p[1]);
            var type = p[2].ToLowerInvariant();
            var color = p.Length > 3 ? p[3] : _svgCurrentColor;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            double sz = 12;
            switch (type)
            {
                case "pin":
                    _svgBuffer.Append($"<polygon points=\"{x.ToString(inv)},{y.ToString(inv)} {(x - sz).ToString(inv)},{(y + sz * 1.2).ToString(inv)} {(x + sz).ToString(inv)},{(y + sz * 1.2).ToString(inv)}\" fill=\"white\" stroke=\"{color}\" stroke-width=\"1.5\"/>");
                    _svgBuffer.Append($"<line x1=\"{(x - sz * 1.3).ToString(inv)}\" y1=\"{(y + sz * 1.2).ToString(inv)}\" x2=\"{(x + sz * 1.3).ToString(inv)}\" y2=\"{(y + sz * 1.2).ToString(inv)}\" stroke=\"{color}\" stroke-width=\"1.5\"/>");
                    // Hatch lines below
                    for (int i = 0; i < 5; i++)
                    {
                        double hx = x - sz * 1.3 + i * sz * 2.6 / 5;
                        _svgBuffer.Append($"<line x1=\"{hx.ToString(inv)}\" y1=\"{(y + sz * 1.2).ToString(inv)}\" x2=\"{(hx - 4).ToString(inv)}\" y2=\"{(y + sz * 1.6).ToString(inv)}\" stroke=\"{color}\" stroke-width=\"0.8\"/>");
                    }
                    break;
                case "roller":
                    double cr = 5;
                    _svgBuffer.Append($"<polygon points=\"{x.ToString(inv)},{y.ToString(inv)} {(x - sz).ToString(inv)},{(y + sz).ToString(inv)} {(x + sz).ToString(inv)},{(y + sz).ToString(inv)}\" fill=\"white\" stroke=\"{color}\" stroke-width=\"1.5\"/>");
                    _svgBuffer.Append($"<circle cx=\"{(x - sz / 2).ToString(inv)}\" cy=\"{(y + sz + cr).ToString(inv)}\" r=\"{cr.ToString(inv)}\" fill=\"white\" stroke=\"{color}\" stroke-width=\"1\"/>");
                    _svgBuffer.Append($"<circle cx=\"{(x + sz / 2).ToString(inv)}\" cy=\"{(y + sz + cr).ToString(inv)}\" r=\"{cr.ToString(inv)}\" fill=\"white\" stroke=\"{color}\" stroke-width=\"1\"/>");
                    _svgBuffer.Append($"<line x1=\"{(x - sz * 1.3).ToString(inv)}\" y1=\"{(y + sz + cr * 2).ToString(inv)}\" x2=\"{(x + sz * 1.3).ToString(inv)}\" y2=\"{(y + sz + cr * 2).ToString(inv)}\" stroke=\"{color}\" stroke-width=\"1.5\"/>");
                    break;
                case "fixed":
                    _svgBuffer.Append($"<line x1=\"{x.ToString(inv)}\" y1=\"{(y - sz).ToString(inv)}\" x2=\"{x.ToString(inv)}\" y2=\"{(y + sz).ToString(inv)}\" stroke=\"{color}\" stroke-width=\"2.5\"/>");
                    for (int i = 0; i < 5; i++)
                    {
                        double hy = y - sz + i * sz * 2 / 5;
                        _svgBuffer.Append($"<line x1=\"{x.ToString(inv)}\" y1=\"{hy.ToString(inv)}\" x2=\"{(x - 8).ToString(inv)}\" y2=\"{(hy + 5).ToString(inv)}\" stroke=\"{color}\" stroke-width=\"0.8\"/>");
                    }
                    break;
            }
        }

        // .moment cx cy [r] [direction] [color] — curved arrow (moment)
        private void SvgMoment(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 2) return;
            double cx = EvalSvgExpr(p[0]), cy = EvalSvgExpr(p[1]);
            double r = p.Length > 2 ? EvalSvgExpr(p[2]) : 15;
            var color = p.Length > 3 ? p[3] : _svgCurrentColor;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            // Draw 270° arc from 45° to 315°
            double s1 = 45 * Math.PI / 180, s2 = 315 * Math.PI / 180;
            double sx = cx + r * Math.Cos(s1), sy = cy + r * Math.Sin(s1);
            double ex = cx + r * Math.Cos(s2), ey = cy + r * Math.Sin(s2);
            _svgBuffer.Append($"<path d=\"M {sx.ToString(inv)} {sy.ToString(inv)} A {r.ToString(inv)} {r.ToString(inv)} 0 1 1 {ex.ToString(inv)} {ey.ToString(inv)}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"1.5\"/>");
            // Arrowhead at end
            double ux = Math.Sin(s2), uy = -Math.Cos(s2);
            double hl = 6;
            _svgBuffer.Append($"<polygon points=\"{ex.ToString(inv)},{ey.ToString(inv)} {(ex - hl * ux - hl * 0.4 * uy).ToString(inv)},{(ey - hl * uy + hl * 0.4 * ux).ToString(inv)} {(ex - hl * ux + hl * 0.4 * uy).ToString(inv)},{(ey - hl * uy - hl * 0.4 * ux).ToString(inv)}\" fill=\"{color}\"/>");
        }

        // .hatch x y w h [spacing] [color] — diagonal hatch lines
        private void SvgHatch(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 4) return;
            double x = EvalSvgExpr(p[0]), y = EvalSvgExpr(p[1]);
            double w = EvalSvgExpr(p[2]), h = EvalSvgExpr(p[3]);
            double sp = p.Length > 4 ? EvalSvgExpr(p[4]) : 6;
            var color = p.Length > 5 ? p[5] : "gray";
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            // Clip
            var clipId = $"hatch_{_svgBuffer.Length}";
            _svgBuffer.Append($"<defs><clipPath id=\"{clipId}\"><rect x=\"{x.ToString(inv)}\" y=\"{y.ToString(inv)}\" width=\"{w.ToString(inv)}\" height=\"{h.ToString(inv)}\"/></clipPath></defs>");
            _svgBuffer.Append($"<g clip-path=\"url(#{clipId})\">");
            double maxD = w + h;
            for (double d = sp; d < maxD; d += sp)
            {
                double lx1 = x + d, ly1 = y;
                double lx2 = x, ly2 = y + d;
                _svgBuffer.Append($"<line x1=\"{lx1.ToString(inv)}\" y1=\"{ly1.ToString(inv)}\" x2=\"{lx2.ToString(inv)}\" y2=\"{ly2.ToString(inv)}\" stroke=\"{color}\" stroke-width=\"0.5\"/>");
            }
            _svgBuffer.Append("</g>");
        }

        // .fillrect x y w h color [opacity] — filled rectangle (no border)
        private void SvgFillRect(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 5) return;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var opacity = p.Length > 5 ? SvgNum(p[5]) : "1";
            _svgBuffer.Append($"<rect x=\"{SvgNum(p[0])}\" y=\"{SvgNum(p[1])}\" width=\"{SvgNum(p[2])}\" height=\"{SvgNum(p[3])}\" fill=\"{p[4]}\" fill-opacity=\"{opacity}\" stroke=\"none\"/>");
        }

        // ── Compound preset figures ─────────────────────────────────────

        // .angle cx cy r startDeg endDeg [label] [color] — draw angle arc with label
        private void SvgAngle(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 5) return;
            double cx = EvalSvgExpr(p[0]), cy = EvalSvgExpr(p[1]);
            double r = EvalSvgExpr(p[2]);
            double startDeg = EvalSvgExpr(p[3]), endDeg = EvalSvgExpr(p[4]);
            string label = p.Length > 5 ? p[5].Replace('_', ' ') : "";
            var color = p.Length > 6 ? p[6] : "blue";
            var inv = System.Globalization.CultureInfo.InvariantCulture;

            // Draw the two radii
            double s1 = startDeg * Math.PI / 180, s2 = endDeg * Math.PI / 180;
            double rx1 = cx + (r + 15) * Math.Cos(s1), ry1 = cy - (r + 15) * Math.Sin(s1);
            double rx2 = cx + (r + 15) * Math.Cos(s2), ry2 = cy - (r + 15) * Math.Sin(s2);
            _svgBuffer.Append($"<line x1=\"{cx.ToString(inv)}\" y1=\"{cy.ToString(inv)}\" x2=\"{rx1.ToString(inv)}\" y2=\"{ry1.ToString(inv)}\" stroke=\"gray\" stroke-width=\"0.8\"/>");
            _svgBuffer.Append($"<line x1=\"{cx.ToString(inv)}\" y1=\"{cy.ToString(inv)}\" x2=\"{rx2.ToString(inv)}\" y2=\"{ry2.ToString(inv)}\" stroke=\"gray\" stroke-width=\"0.8\"/>");

            // Draw the arc
            double ax1 = cx + r * Math.Cos(s1), ay1 = cy - r * Math.Sin(s1);
            double ax2 = cx + r * Math.Cos(s2), ay2 = cy - r * Math.Sin(s2);
            double sweep = endDeg - startDeg;
            int largeArc = Math.Abs(sweep) > 180 ? 1 : 0;
            int sweepDir = sweep > 0 ? 0 : 1; // SVG: 0=counterclockwise in screen coords (= math positive)
            _svgBuffer.Append($"<path d=\"M {ax1.ToString(inv)} {ay1.ToString(inv)} A {r.ToString(inv)} {r.ToString(inv)} 0 {largeArc} {sweepDir} {ax2.ToString(inv)} {ay2.ToString(inv)}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"2\"/>");

            // Label at midpoint of arc
            if (!string.IsNullOrEmpty(label))
            {
                double midDeg = (startDeg + endDeg) / 2;
                double midRad = midDeg * Math.PI / 180;
                double lx = cx + (r + 8) * Math.Cos(midRad), ly = cy - (r + 8) * Math.Sin(midRad);
                _svgBuffer.Append($"<text x=\"{lx.ToString(inv)}\" y=\"{(ly + 4).ToString(inv)}\" text-anchor=\"middle\" fill=\"{color}\" font-size=\"11\" font-weight=\"bold\">{System.Web.HttpUtility.HtmlEncode(label)}</text>");
            }
        }

        // .radian cx cy R [color] — complete radian diagram (circle + radius + arc + label)
        private void SvgRadian(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 3) return;
            double cx = EvalSvgExpr(p[0]), cy = EvalSvgExpr(p[1]);
            double R = EvalSvgExpr(p[2]);
            var color = p.Length > 3 ? p[3] : "black";
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            double rad573 = 57.2957795; // 1 radian in degrees

            // Full circle (light)
            _svgBuffer.Append($"<circle cx=\"{cx.ToString(inv)}\" cy=\"{cy.ToString(inv)}\" r=\"{R.ToString(inv)}\" fill=\"none\" stroke=\"#aaaaaa\" stroke-width=\"1\"/>");
            // Center dot
            _svgBuffer.Append($"<circle cx=\"{cx.ToString(inv)}\" cy=\"{cy.ToString(inv)}\" r=\"3\" fill=\"{color}\" stroke=\"none\"/>");

            // Horizontal radius
            _svgBuffer.Append($"<line x1=\"{cx.ToString(inv)}\" y1=\"{cy.ToString(inv)}\" x2=\"{(cx + R).ToString(inv)}\" y2=\"{cy.ToString(inv)}\" stroke=\"{color}\" stroke-width=\"1.5\"/>");
            // Label R on horizontal
            _svgBuffer.Append($"<text x=\"{(cx + R / 2).ToString(inv)}\" y=\"{(cy + 15).ToString(inv)}\" text-anchor=\"middle\" fill=\"{color}\" font-size=\"12\" font-weight=\"bold\">R</text>");

            // Second radius at 1 radian (57.3°) upward
            double endX = cx + R * Math.Cos(rad573 * Math.PI / 180);
            double endY = cy - R * Math.Sin(rad573 * Math.PI / 180);
            _svgBuffer.Append($"<line x1=\"{cx.ToString(inv)}\" y1=\"{cy.ToString(inv)}\" x2=\"{endX.ToString(inv)}\" y2=\"{endY.ToString(inv)}\" stroke=\"{color}\" stroke-width=\"1.5\"/>");
            // Label R on diagonal
            double rmx = (cx + endX) / 2 - 10, rmy = (cy + endY) / 2;
            _svgBuffer.Append($"<text x=\"{rmx.ToString(inv)}\" y=\"{rmy.ToString(inv)}\" text-anchor=\"middle\" fill=\"{color}\" font-size=\"12\" font-weight=\"bold\">R</text>");

            // RED arc = the arc of length R (1 radian)
            double ax1 = cx + R, ay1 = cy; // start at 0°
            // SVG arc: from (ax1,ay1) to (endX,endY), radius R, counterclockwise (sweep=0)
            _svgBuffer.Append($"<path d=\"M {ax1.ToString(inv)} {ay1.ToString(inv)} A {R.ToString(inv)} {R.ToString(inv)} 0 0 0 {endX.ToString(inv)} {endY.ToString(inv)}\" fill=\"none\" stroke=\"red\" stroke-width=\"3.5\"/>");
            // Label "arco = R" next to red arc
            double arcMidDeg = rad573 / 2;
            double arcMidRad = arcMidDeg * Math.PI / 180;
            double alx = cx + (R + 20) * Math.Cos(arcMidRad), aly = cy - (R + 20) * Math.Sin(arcMidRad);
            _svgBuffer.Append($"<text x=\"{alx.ToString(inv)}\" y=\"{aly.ToString(inv)}\" text-anchor=\"start\" fill=\"red\" font-size=\"12\" font-weight=\"bold\">arco = R</text>");

            // BLUE angle arc (small, near center)
            double sr = R * 0.25;
            double sax = cx + sr, say = cy;
            double sex = cx + sr * Math.Cos(rad573 * Math.PI / 180);
            double sey = cy - sr * Math.Sin(rad573 * Math.PI / 180);
            _svgBuffer.Append($"<path d=\"M {sax.ToString(inv)} {say.ToString(inv)} A {sr.ToString(inv)} {sr.ToString(inv)} 0 0 0 {sex.ToString(inv)} {sey.ToString(inv)}\" fill=\"none\" stroke=\"blue\" stroke-width=\"2\"/>");
            // Label "1 rad"
            double slx = cx + (sr + 8) * Math.Cos(arcMidRad), sly = cy - (sr + 8) * Math.Sin(arcMidRad);
            _svgBuffer.Append($"<text x=\"{slx.ToString(inv)}\" y=\"{(sly + 4).ToString(inv)}\" text-anchor=\"start\" fill=\"blue\" font-size=\"11\" font-weight=\"bold\">1 rad = 57.3\u00b0</text>");
        }

        // .spring x1 y1 x2 y2 [nCoils] [color] — zigzag spring
        private void SvgSpring(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 4) return;
            double x1 = EvalSvgExpr(p[0]), y1 = EvalSvgExpr(p[1]);
            double x2 = EvalSvgExpr(p[2]), y2 = EvalSvgExpr(p[3]);
            int nCoils = p.Length > 4 ? (int)EvalSvgExpr(p[4]) : 6;
            var color = p.Length > 5 ? p[5] : _svgCurrentColor;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            double dx = x2 - x1, dy = y2 - y1;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1) return;
            double ux = dx / len, uy = dy / len;
            double nx = -uy * 8, ny = ux * 8; // perpendicular offset
            var pts = new System.Text.StringBuilder();
            pts.Append($"{x1.ToString(inv)},{y1.ToString(inv)} ");
            double leadIn = len * 0.1;
            // Lead-in
            double lx = x1 + ux * leadIn, ly = y1 + uy * leadIn;
            pts.Append($"{lx.ToString(inv)},{ly.ToString(inv)} ");
            // Coils
            double coilLen = (len - 2 * leadIn) / nCoils;
            for (int i = 0; i < nCoils; i++)
            {
                double t = leadIn + (i + 0.25) * coilLen;
                double px = x1 + ux * t + nx * (i % 2 == 0 ? 1 : -1);
                double py = y1 + uy * t + ny * (i % 2 == 0 ? 1 : -1);
                pts.Append($"{px.ToString(inv)},{py.ToString(inv)} ");
                t = leadIn + (i + 0.75) * coilLen;
                px = x1 + ux * t + nx * (i % 2 == 0 ? -1 : 1);
                py = y1 + uy * t + ny * (i % 2 == 0 ? -1 : 1);
                pts.Append($"{px.ToString(inv)},{py.ToString(inv)} ");
            }
            // Lead-out
            lx = x2 - ux * leadIn; ly = y2 - uy * leadIn;
            pts.Append($"{lx.ToString(inv)},{ly.ToString(inv)} ");
            pts.Append($"{x2.ToString(inv)},{y2.ToString(inv)}");
            _svgBuffer.Append($"<polyline points=\"{pts}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"1.5\"/>");
        }

        // .grid x y w h [spacing] [color] — coordinate grid
        private void SvgGrid(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 4) return;
            double x = EvalSvgExpr(p[0]), y = EvalSvgExpr(p[1]);
            double w = EvalSvgExpr(p[2]), h = EvalSvgExpr(p[3]);
            double sp = p.Length > 4 ? EvalSvgExpr(p[4]) : 20;
            var color = p.Length > 5 ? p[5] : "#e0e0e0";
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            for (double gx = x; gx <= x + w; gx += sp)
                _svgBuffer.Append($"<line x1=\"{gx.ToString(inv)}\" y1=\"{y.ToString(inv)}\" x2=\"{gx.ToString(inv)}\" y2=\"{(y + h).ToString(inv)}\" stroke=\"{color}\" stroke-width=\"0.5\"/>");
            for (double gy = y; gy <= y + h; gy += sp)
                _svgBuffer.Append($"<line x1=\"{x.ToString(inv)}\" y1=\"{gy.ToString(inv)}\" x2=\"{(x + w).ToString(inv)}\" y2=\"{gy.ToString(inv)}\" stroke=\"{color}\" stroke-width=\"0.5\"/>");
        }

        // .curvedarrow cx cy r startDeg endDeg [color] — arc with arrowhead at end
        private void SvgCurvedArrow(string args)
        {
            var p = SvgSplitArgs(args);
            if (p.Length < 5) return;
            double cx = EvalSvgExpr(p[0]), cy = EvalSvgExpr(p[1]);
            double r = EvalSvgExpr(p[2]);
            double startDeg = EvalSvgExpr(p[3]), endDeg = EvalSvgExpr(p[4]);
            var color = p.Length > 5 ? p[5] : _svgCurrentColor;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            double s1 = startDeg * Math.PI / 180, s2 = endDeg * Math.PI / 180;
            // SVG Y is flipped, so negate sin for Y coords
            double ax1 = cx + r * Math.Cos(s1), ay1 = cy - r * Math.Sin(s1);
            double ax2 = cx + r * Math.Cos(s2), ay2 = cy - r * Math.Sin(s2);
            double sweep = endDeg - startDeg;
            int largeArc = Math.Abs(sweep) > 180 ? 1 : 0;
            int sweepDir = sweep > 0 ? 0 : 1;
            _svgBuffer.Append($"<path d=\"M {ax1.ToString(inv)} {ay1.ToString(inv)} A {r.ToString(inv)} {r.ToString(inv)} 0 {largeArc} {sweepDir} {ax2.ToString(inv)} {ay2.ToString(inv)}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"1.5\"/>");
            // Arrowhead at end — tangent direction
            double tangentAngle = s2 + (sweep > 0 ? Math.PI / 2 : -Math.PI / 2);
            double hl = 7;
            double tx = Math.Cos(tangentAngle), ty = -Math.Sin(tangentAngle);
            double nx2 = -ty, ny2 = tx;
            _svgBuffer.Append($"<polygon points=\"{ax2.ToString(inv)},{ay2.ToString(inv)} {(ax2 - hl * tx + hl * 0.35 * nx2).ToString(inv)},{(ay2 - hl * ty + hl * 0.35 * ny2).ToString(inv)} {(ax2 - hl * tx - hl * 0.35 * nx2).ToString(inv)},{(ay2 - hl * ty - hl * 0.35 * ny2).ToString(inv)}\" fill=\"{color}\"/>");
        }

        // #sym diff(x^2 + 3*x; x)
        // #sym integrate(sin(x); x; 0; pi)
        // #sym solve(x^2 - 4; x)
        // #sym simplify((x^2-1)/(x-1))
        // Uses AngouriMath for symbolic computation, renders via Calcpad HtmlWriter
        // Block mode: #sym alone on a line starts block, #end sym ends it
        private bool _insideSymBlock;

        private void ParseKeywordSym(ReadOnlySpan<char> s)
        {
            var spaceIdx = s.IndexOf(' ');
            var command = spaceIdx > 0 ? s[(spaceIdx + 1)..].ToString().Trim() : "";

            // If #sym alone (no expression) → enter block mode
            if (string.IsNullOrEmpty(command))
            {
                _insideSymBlock = true;
                return;
            }

            // Decorative patterns rendered by the #deq renderer (partial
            // derivatives, matrix literals, prime notation, integrals).
            // We try these FIRST so that #sym can display math that the
            // SymbolicProcessor backend can't parse (e.g., the ∂ Unicode char).
            if (_isVisible)
            {
                // Matrix literal on its own: "[...|...]"
                if (command.StartsWith("[") && command.EndsWith("]") && command.Contains('|'))
                {
                    var matHtml = TryRenderMatrixLiteral(command);
                    if (!string.IsNullOrEmpty(matHtml))
                    {
                        _sb.Append($"<p{HtmlId}{HtmlLineClass}><span class=\"eq\"{EqStyleForMatrix(matHtml)}>{matHtml}</span></p>\n");
                        return;
                    }
                }
                // Assignment "Name = [...|...]"
                var eqIdx2 = FindTopLevelEquals(command);
                if (eqIdx2 > 0)
                {
                    var rhs = command.Substring(eqIdx2 + 1).Trim();
                    if (rhs.StartsWith("[") && rhs.EndsWith("]") && rhs.Contains('|'))
                    {
                        var matHtml = TryRenderMatrixLiteral(rhs);
                        if (!string.IsNullOrEmpty(matHtml))
                        {
                            var lhs = command.Substring(0, eqIdx2).Trim();
                            var lhsHtml = DeqRenderVar(lhs);
                            var content = $"{lhsHtml} = {matHtml}";
                            _sb.Append($"<p{HtmlId}{HtmlLineClass}><span class=\"eq\"{EqStyleForMatrix(content)}>{content}</span></p>\n");
                            return;
                        }
                    }
                }
                // Decorative standalone patterns: ∂f/∂x, ∫(...), integral(...), v'(x)
                // Or assignments "LHS = <pattern>" where RHS has ∂/∫/prime
                if (command.Contains('∂') || command.Contains('∫')
                    || command.StartsWith("integral(", StringComparison.OrdinalIgnoreCase))
                {
                    // Try render the whole command via the #deq special renderer
                    var specialHtml = TryRenderDeqSpecial(command);
                    if (!string.IsNullOrEmpty(specialHtml))
                    {
                        _sb.Append($"<p{HtmlId}{HtmlLineClass}><span class=\"eq\"{EqStyleForMatrix(specialHtml)}>{specialHtml}</span></p>\n");
                        return;
                    }
                    // If it's an assignment, split and render each side
                    if (eqIdx2 > 0)
                    {
                        var lhs = command.Substring(0, eqIdx2).Trim();
                        var rhs = command.Substring(eqIdx2 + 1).Trim();
                        var lhsHtml = DeqRenderVar(lhs);
                        var rhsHtml = TryRenderDeqSpecial(rhs) ?? DeqRenderVar(rhs);
                        var content = $"{lhsHtml} = {rhsHtml}";
                        _sb.Append($"<p{HtmlId}{HtmlLineClass}><span class=\"eq\"{EqStyleForMatrix(content)}>{content}</span></p>\n");
                        return;
                    }
                }
            }

            // Normalize Unicode operators before dispatching to AngouriMath:
            // `·` (U+00B7 middle dot), `×` (U+00D7), `⋅` (U+22C5 dot op) → `*`.
            // Calcpad Symbolic users habitually type `·` for multiplication but
            // AngouriMath's parser only understands ASCII `*`.
            var normalized = NormalizeOps(command);
            var result = SymbolicProcessor.Process(normalized);
            if (!_isVisible) return;

            if (result.IsError)
            {
                _sb.Append($"<p{HtmlId}{HtmlLineClass}><span class=\"err\">{System.Web.HttpUtility.HtmlEncode(result.Error)}</span></p>\n");
                return;
            }

            var sb2 = RenderSymResultBody(result);

            if (sb2.Length > 0)
                _sb.Append($"<p{HtmlId}{HtmlLineClass}><span class=\"eq\">{sb2}</span></p>\n");
        }

        // Reusable rendering of a SymResult's Parts into the inner HTML that
        // would go inside <span class="eq">…</span>. Shared by ParseKeywordSym
        // and ParseKeywordColumns so symbolic operations render identically
        // whether they're line-level (#sym …) or cell-level (#blk cells).
        private System.Text.StringBuilder RenderSymResultBody(SymbolicProcessor.SymResult result)
        {
            var savedIsVal = _isVal;
            var savedIsCalc = _parser.IsCalculation;
            _isVal = -1; // #noc mode
            _parser.IsCalculation = false;
            var hw = new HtmlWriter(Settings.Math, _parser.Phasor);

            var sb2 = new System.Text.StringBuilder();
            for (int i = 0; i < result.Parts.Length; i++)
            {
                var part = result.Parts[i]?.Trim();
                if (string.IsNullOrEmpty(part)) continue;
                if (i > 0) sb2.Append(" = ");

                if (part.StartsWith(SymbolicProcessor.TAG_NARY))
                {
                    // N-ary: symbol|sub|sup|bodyExpr
                    var data = part[SymbolicProcessor.TAG_NARY.Length..];
                    var segs = data.Split('|');
                    if (segs.Length >= 4)
                    {
                        var symbol = segs[0]; // ∫, ∑, ∏
                        var sub = segs[1];     // lower bound (or empty)
                        var sup = segs[2];     // upper bound (or "0" for none)
                        var body = segs[3];    // expression
                        // Render sub/sup through parser
                        var subHtml = string.IsNullOrEmpty(sub) ? "" : SymRenderExpr(sub);
                        var supHtml = sup == "0" ? "" : SymRenderExpr(sup);
                        var bodyHtml = SymRenderExpr(body);
                        sb2.Append(hw.FormatNary(symbol, subHtml, supHtml, bodyHtml));
                    }
                }
                else if (part.StartsWith(SymbolicProcessor.TAG_DERIV))
                {
                    // Derivative: num|den|body — fraction + body
                    var data = part[SymbolicProcessor.TAG_DERIV.Length..];
                    var segs = data.Split('|');
                    if (segs.Length >= 3)
                    {
                        var num = SymRenderExpr(segs[0]);
                        var den = SymRenderExpr(segs[1]);
                        var body = SymRenderExpr(segs[2]);
                        sb2.Append(hw.FormatDivision(num, den, 0));
                        sb2.Append("\u2009"); // thin space
                        sb2.Append(hw.AddBrackets(body));
                    }
                }
                else if (part.StartsWith(SymbolicProcessor.TAG_FRAC))
                {
                    // Fraction: numerator|denominator
                    var data = part[SymbolicProcessor.TAG_FRAC.Length..];
                    var pipe = data.IndexOf('|');
                    if (pipe > 0)
                    {
                        var num = SymRenderExpr(data[..pipe]);
                        var den = SymRenderExpr(data[(pipe + 1)..]);
                        sb2.Append(hw.FormatDivision(num, den, 0));
                    }
                }
                else if (part.StartsWith(SymbolicProcessor.TAG_NABLA))
                {
                    // Nabla operator: type|body
                    // ∇ rendered as inline symbol (not .nary — too large for nabla)
                    const string nabla = "<span style=\"font-size:120%;color:#C080F0;font-family:Georgia Pro Light,serif\">\u2207</span>";
                    var data = part[SymbolicProcessor.TAG_NABLA.Length..];
                    var pipe = data.IndexOf('|');
                    var type = pipe > 0 ? data[..pipe] : data;
                    var body = pipe > 0 ? data[(pipe + 1)..] : "";

                    switch (type)
                    {
                        case "grad":
                            sb2.Append(nabla);
                            sb2.Append(hw.AddBrackets(SymRenderExpr(body)));
                            break;
                        case "div":
                            sb2.Append(nabla);
                            sb2.Append(" \u00B7 <b>" + SymRenderExpr(body) + "</b>");
                            break;
                        case "curl":
                            sb2.Append(nabla);
                            sb2.Append(" \u00D7 <b>" + SymRenderExpr(body) + "</b>");
                            break;
                        case "lap":
                            sb2.Append(nabla + "\u00B2");
                            sb2.Append(hw.AddBrackets(SymRenderExpr(body)));
                            break;
                    }
                }
                else if (part.StartsWith(SymbolicProcessor.TAG_TAYLOR))
                {
                    // Taylor: body|n — renders as "Taylor(rendered_body)" with subscript n=N
                    var data = part[SymbolicProcessor.TAG_TAYLOR.Length..];
                    var pipe = data.IndexOf('|');
                    if (pipe > 0)
                    {
                        var body = SymRenderExpr(data[..pipe]);
                        var nVal = data[(pipe + 1)..];
                        sb2.Append("<b>Taylor</b>");
                        sb2.Append(hw.AddBrackets(body));
                        sb2.Append($"<sub><var>n</var> = {nVal}</sub>");
                    }
                }
                else if (part.StartsWith(SymbolicProcessor.TAG_SOLVE))
                {
                    // Solve: var|val1|val2|... — renders as "var = { val1; val2 }"
                    var data = part[SymbolicProcessor.TAG_SOLVE.Length..];
                    var segs = data.Split('|');
                    if (segs.Length >= 2)
                    {
                        sb2.Append(SymRenderExpr(segs[0]));
                        sb2.Append(" = { ");
                        for (int j = 1; j < segs.Length; j++)
                        {
                            if (j > 1) sb2.Append(" ;\u2009 ");
                            sb2.Append(SymRenderExpr(segs[j]));
                        }
                        sb2.Append(" }");
                    }
                }
                else if (part.StartsWith(SymbolicProcessor.TAG_HTML))
                {
                    // HTML with optional {CALCPAD:expr} markers resolved through parser
                    var html = part[SymbolicProcessor.TAG_HTML.Length..];
                    sb2.Append(ResolveCalcpadMarkers(html));
                }
                else
                {
                    // Regular Calcpad expression
                    sb2.Append(SymRenderExpr(part));
                }
            }

            _isVal = savedIsVal;
            _parser.IsCalculation = savedIsCalc;
            return sb2;
        }

        // Render a Calcpad expression to HTML via MathParser
        private string SymRenderExpr(string expr)
        {
            // Special: expressions with ∂ (partial derivative symbol) can't be parsed
            // Render them as HTML directly: ∂ → italic, ^n → superscript
            if (expr.Contains('\u2202'))
                return RenderPartialSymbol(expr);

            // Clean Maxima / AngouriMath artifacts that confuse Calcpad's parser:
            //   %e            → Maxima's literal Euler number constant
            //   log(e)        → AngouriMath leaves this unsimplified when `e`
            //                   is treated as a free variable; mathematically
            //                   it equals 1 (natural log of Euler's number).
            //   log(e)·X      → simplifies to X (since log(e) = 1).
            // Without these substitutions, integrate(x^2·e^x; x) returns
            // "((log(e)^2·x^2 − 2·log(e)·x + 2)·%e^(log(e)·x))/log(e)^3"
            // which renders as plain text because Calcpad doesn't know %e.
            expr = expr.Replace("%e", "e");
            // Collapse log(e) → 1 (e here is Euler's constant). Use a simple
            // string replace; downstream the parser can simplify 1·X → X.
            if (expr.Contains("log(e)"))
            {
                expr = System.Text.RegularExpressions.Regex.Replace(expr,
                    @"\blog\(e\)", "1");
            }

            // NOTE: previous versions converted "s1" → "s_1" automatically so
            // a trailing digit rendered as subscript. The user prefers the
            // explicit notation (`s_1`, `s_2`) and "s1" must stay as "s1"
            // (Calcpad already accepts s1, s2 as valid identifiers).
            // The auto-conversion is intentionally DISABLED here.

            // Pre-define variable names found in the expression so MathParser
            // doesn't interpret them as units (e.g. "u" as atomic mass unit).
            // Only stub variables that aren't already bound — otherwise we
            // would clobber real numeric values assigned earlier (e.g. L = 60
            // defined outside a #blk cell and referenced inside a #sym op).
            var varNames = ExtractVariableNames(expr);
            foreach (var vn in varNames)
            {
                if (_parser.HasVariable(vn)) continue;
                try { _parser.SetVariable(vn, 0); } catch { }
            }

            try
            {
                _parser.Parse(expr, false);
                var html = _parser.ToHtml();
                if (!string.IsNullOrWhiteSpace(html))
                    return html;
            }
            catch { /* fallback */ }
            var w = new HtmlWriter(Settings.Math, _parser.Phasor);
            return w.FormatVariable(expr, string.Empty, false);
        }

        /// <summary>
        /// Convert variable names ending in digits to subscript notation.
        /// E.g. "u1" → "u_1", "u2" → "u_2", "Le" stays "Le" (no trailing digit).
        /// Only converts if the name is a letter followed by digits at the end.
        /// </summary>
        private static string ConvertDigitSuffixToSubscript(string expr)
        {
            var sb = new System.Text.StringBuilder();
            int i = 0;
            while (i < expr.Length)
            {
                // Check for variable name: letter(s) followed by digit(s)
                if (char.IsLetter(expr[i]))
                {
                    int start = i;
                    // Read all letters
                    while (i < expr.Length && char.IsLetter(expr[i])) i++;
                    int digitStart = i;
                    // Read all digits
                    while (i < expr.Length && char.IsDigit(expr[i])) i++;

                    var name = expr[start..i];
                    if (digitStart < i && digitStart > start && !name.Contains('_'))
                    {
                        // Has trailing digits and no existing underscore
                        sb.Append(expr[start..digitStart]);
                        sb.Append('_');
                        sb.Append(expr[digitStart..i]);
                    }
                    else
                    {
                        sb.Append(name);
                    }
                }
                else
                {
                    sb.Append(expr[i]);
                    i++;
                }
            }
            return sb.ToString();
        }

        // Render ∂, ∂x, ∂^2, ∂x^2, etc. as proper HTML
        private static readonly HashSet<string> _mathFunctions = new(StringComparer.OrdinalIgnoreCase)
        {
            "sin","cos","tan","log","ln","exp","abs","sqrt","cbrt",
            "asin","acos","atan","sec","csc","cot","sinh","cosh","tanh",
            "max","min","sign","mod","floor","ceiling","round","trunc"
        };

        private static List<string> ExtractVariableNames(string expr)
        {
            var names = new List<string>();
            int i = 0;
            while (i < expr.Length)
            {
                if (char.IsLetter(expr[i]) || expr[i] == '_')
                {
                    int start = i;
                    while (i < expr.Length && (char.IsLetterOrDigit(expr[i]) || expr[i] == '_')) i++;
                    var name = expr[start..i];
                    if (!_mathFunctions.Contains(name) && !names.Contains(name))
                        names.Add(name);
                }
                else
                    i++;
            }
            return names;
        }

        private static string RenderPartialSymbol(string expr)
        {
            var sb = new System.Text.StringBuilder();
            int i = 0;
            while (i < expr.Length)
            {
                if (expr[i] == '\u2202') // ∂
                {
                    sb.Append("<i>\u2202</i>");
                    i++;
                }
                else if (expr[i] == '^' && i + 1 < expr.Length)
                {
                    // Superscript
                    i++;
                    var sup = new System.Text.StringBuilder();
                    while (i < expr.Length && (char.IsDigit(expr[i]) || char.IsLetter(expr[i])))
                    {
                        sup.Append(expr[i]);
                        i++;
                    }
                    sb.Append($"<sup>{sup}</sup>");
                }
                else if (char.IsLetter(expr[i]))
                {
                    // Variable name
                    var vName = new System.Text.StringBuilder();
                    while (i < expr.Length && (char.IsLetterOrDigit(expr[i]) || expr[i] == '_'))
                    {
                        vName.Append(expr[i]);
                        i++;
                    }
                    sb.Append($"<var>{vName}</var>");
                }
                else
                {
                    sb.Append(expr[i]);
                    i++;
                }
            }
            return sb.ToString();
        }

        // Resolve {CALCPAD:expression} markers in HTML strings
        private string ResolveCalcpadMarkers(string html)
        {
            const string prefix = "{CALCPAD:";
            var sb = new System.Text.StringBuilder(html.Length + 64);
            var pos = 0;
            while (pos < html.Length)
            {
                var ms = html.IndexOf(prefix, pos, StringComparison.Ordinal);
                if (ms < 0) { sb.Append(html, pos, html.Length - pos); break; }
                sb.Append(html, pos, ms - pos);
                var es = ms + prefix.Length;
                // Find matching } respecting nested {}
                int depth = 1, ee = es;
                while (ee < html.Length && depth > 0)
                {
                    if (html[ee] == '{') depth++;
                    else if (html[ee] == '}') depth--;
                    if (depth > 0) ee++;
                }
                var expr = html[es..ee];
                sb.Append(SymRenderExpr(expr));
                pos = ee + 1;
            }
            return sb.ToString();
        }

        // ─── #python / #end python — Execute Python code block ─────

        private bool _insidePythonBlock;
        private int _pythonBlockStartLine;
        private List<string> _pythonBlockLines;

        private void ParseKeywordPython(ReadOnlySpan<char> s)
        {
            var spaceIdx = s.IndexOf(' ');
            var rest = spaceIdx > 0 ? s[(spaceIdx + 1)..].ToString().Trim() : "";

            // #python alone → start block mode
            if (string.IsNullOrEmpty(rest))
            {
                _insidePythonBlock = true;
                _pythonBlockStartLine = _currentLine;
                _pythonBlockLines = new List<string>();
                return;
            }
            // #python one-liner → execute single line
            ExecutePythonCode(rest);
        }

        private void ParseKeywordEndPython()
        {
            if (!_insidePythonBlock || _pythonBlockLines == null) return;
            _insidePythonBlock = false;
            var code = string.Join("\n", _pythonBlockLines);
            _pythonBlockLines = null;
            ExecutePythonCode(code);
        }

        private string HtmlIdForLine(int line) =>
            Debug && (_loops.Count == 0 || _loops.Peek().Iteration == 1) ?
            $" id=\"line-{line + 1}\" class=\"line\"" : "";

        private void ExecutePythonCode(string code)
        {
            if (!_calculate || !_isVisible) return;
            try
            {
                PipProgressChanged?.Invoke("Running Python...");
                var tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "calcpad_python_" + Guid.NewGuid().ToString("N")[..8] + ".py");
                // Write BOM-free UTF-8
                System.IO.File.WriteAllText(tempFile, code, new System.Text.UTF8Encoding(false));

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{tempFile}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };
                psi.Environment["PYTHONIOENCODING"] = "utf-8";

                using var proc = System.Diagnostics.Process.Start(psi);
                var stdout = proc.StandardOutput.ReadToEnd();
                var stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit(30000);

                // Clean up temp file
                try { System.IO.File.Delete(tempFile); } catch { }

                // Render output — first line gets clickable data-line, rest just display
                if (!string.IsNullOrWhiteSpace(stdout))
                {
                    var lines = stdout.Split('\n');
                    var isFirst = true;
                    foreach (var line in lines)
                    {
                        var trimmed = line.TrimEnd('\r');
                        if (!string.IsNullOrEmpty(trimmed))
                        {
                            // Check if output looks like CALCPAD:var=value (variable export)
                            if (trimmed.StartsWith("CALCPAD:"))
                            {
                                var eqIdx = trimmed.IndexOf('=', 8);
                                if (eqIdx > 8)
                                {
                                    var varName = trimmed[8..eqIdx].Trim();
                                    var varValue = trimmed[(eqIdx + 1)..].Trim();
                                    if (double.TryParse(varValue, System.Globalization.NumberStyles.Any,
                                        System.Globalization.CultureInfo.InvariantCulture, out var numVal))
                                    {
                                        _parser.SetVariable(varName, new RealValue(numVal));
                                    }
                                }
                                continue;
                            }
                            // First output line gets data-line of #python keyword for click navigation
                            var lineId = isFirst ? HtmlIdForLine(_pythonBlockStartLine) : "";
                            isFirst = false;
                            _sb.Append($"<p{lineId}><span class=\"eq\">{RenderPythonLine(trimmed)}</span></p>\n");
                        }
                    }
                }
                if (!string.IsNullOrWhiteSpace(stderr))
                    _sb.Append($"<p{HtmlId}{HtmlLineClass}><span class=\"err\">{System.Web.HttpUtility.HtmlEncode(stderr.Trim())}</span></p>\n");
                PipProgressChanged?.Invoke(null);
            }
            catch (Exception ex)
            {
                PipProgressChanged?.Invoke(null);
                _sb.Append($"<p{HtmlId}{HtmlLineClass}><span class=\"err\">Python error: {System.Web.HttpUtility.HtmlEncode(ex.Message)}</span></p>\n");
            }
        }

        /// <summary>
        /// Render a line of Python output as CalcpadCE HTML.
        /// Converts Python math notation to CalcpadCE template notation.
        /// Functions like diff(), integrate(), solve() become d/dx, ∫, etc.
        /// </summary>
        private string RenderPythonLine(string line)
        {
            // Convert Python notation → Calcpad (** → ^ only, * handled in sub-functions)
            var s = line.Replace("**", "^");

            // Split on first " = " for "label = result" format
            var eqIdx = s.IndexOf(" = ");
            if (eqIdx > 0)
            {
                var lhs = s[..eqIdx].Trim();
                var rhs = s[(eqIdx + 3)..].Trim();
                // Try to render LHS as CalcpadCE math (pass through parser)
                var lhsHtml = RenderPythonExprAsCalcpad(lhs);
                var rhsHtml = RenderPythonExprAsCalcpad(rhs);
                return $"{lhsHtml} = {rhsHtml}";
            }

            return RenderPythonExprAsCalcpad(s);
        }

        /// <summary>
        /// Convert Python expression to CalcpadCE notation, then render via parser.
        /// diff(x^3, x) → d(x^3)/dx rendered as fraction
        /// integrate(sin(x), x) → ∫ sin(x) dx
        /// solve(x^2-4, x) → x²-4 = 0
        /// </summary>
        private string RenderPythonExprAsCalcpad(string expr)
        {
            // Normalize: · back to * for parser, but keep ^
            var calcpadExpr = expr.Replace("\u00B7", "*");

            // Transform Python symbolic functions to CalcpadCE notation

            // diff(expr) or diff(expr, var) → fraction d/dx (body)
            var diffMatch = System.Text.RegularExpressions.Regex.Match(calcpadExpr,
                @"^diff\((.+?)(?:,\s*(\w+))?\)$");
            if (diffMatch.Success)
            {
                var body = diffMatch.Groups[1].Value;
                var v = diffMatch.Groups[2].Success ? diffMatch.Groups[2].Value : "x";
                var hw = new HtmlWriter(Settings.Math, _parser.Phasor);
                var num = SymRenderExpr("d");
                var den = SymRenderExpr("d" + v);
                var bodyHtml = SymRenderExpr(body);
                return hw.FormatDivision(num, den, 0) + "\u2009" + hw.AddBrackets(bodyHtml);
            }

            // integrate(expr) or integrate(expr, var) → ∫ body dx
            var intMatch = System.Text.RegularExpressions.Regex.Match(calcpadExpr,
                @"^integrate\((.+?)(?:,\s*(\w+))?\)$");
            if (intMatch.Success)
            {
                var body = intMatch.Groups[1].Value;
                var v = intMatch.Groups[2].Success ? intMatch.Groups[2].Value : "x";
                var hw = new HtmlWriter(Settings.Math, _parser.Phasor);
                var bodyHtml = SymRenderExpr(body);
                return hw.FormatNary("\u222B", "", "", bodyHtml + "\u2009<var>d" + v + "</var>");
            }

            // solve(expr) or solve(expr, var) → body = 0
            var solveMatch = System.Text.RegularExpressions.Regex.Match(calcpadExpr,
                @"^solve\((.+?)(?:,\s*\w+)?\)$");
            if (solveMatch.Success)
            {
                calcpadExpr = solveMatch.Groups[1].Value;
                // Will be rendered below, then " = 0" is NOT added here
                // because it's on the LHS already
            }

            // Taylor expr → Taylor(expr)
            if (calcpadExpr.StartsWith("Taylor "))
            {
                var body = calcpadExpr[7..];
                var hw = new HtmlWriter(Settings.Math, _parser.Phasor);
                return "<b>Taylor</b>" + hw.AddBrackets(SymRenderExpr(body));
            }

            // pi → π
            calcpadExpr = System.Text.RegularExpressions.Regex.Replace(calcpadExpr, @"\bpi\b", "\u03C0");

            // Python lists → CalcpadCE vectors/matrices
            // [[a,b],[c,d]] → [a; b | c; d]
            if (calcpadExpr.Contains("[["))
            {
                calcpadExpr = System.Text.RegularExpressions.Regex.Replace(calcpadExpr, @"\]\s*,\s*\[", " | ");
                calcpadExpr = calcpadExpr.Replace("[[", "[").Replace("]]", "]");
                // Convert remaining commas to semicolons (within rows)
                calcpadExpr = System.Text.RegularExpressions.Regex.Replace(calcpadExpr, @",\s*", "; ");
            }
            // [a, b, c] → [a; b; c] (single list → vector)
            if (calcpadExpr.StartsWith("[") && calcpadExpr.EndsWith("]") && !calcpadExpr.Contains("|"))
            {
                calcpadExpr = System.Text.RegularExpressions.Regex.Replace(calcpadExpr, @",\s*", "; ");
            }

            // Scientific notation: 1.23e+04 → 12300 (evaluate)
            calcpadExpr = System.Text.RegularExpressions.Regex.Replace(calcpadExpr,
                @"(-?\d+\.?\d*)[eE]([+-]?\d+)", m =>
                {
                    if (double.TryParse(m.Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var val))
                        return val.ToString("G10", System.Globalization.CultureInfo.InvariantCulture);
                    return m.Value;
                });

            // Try to render through CalcpadCE parser
            try
            {
                var savedIsVal = _isVal;
                _isVal = -1;
                _parser.IsCalculation = false;
                _parser.Parse(calcpadExpr, false);
                var html = _parser.ToHtml();
                _isVal = savedIsVal;
                _parser.IsCalculation = _isVal != -1;
                if (!string.IsNullOrWhiteSpace(html))
                    return html;
            }
            catch { /* fallback below */ }

            // Fallback: basic formatting
            return FormatPythonExprBasic(expr);
        }

        private static string FormatPythonExprBasic(string expr)
        {
            var html = expr;
            // Superscripts
            html = System.Text.RegularExpressions.Regex.Replace(html, @"\^(\d+)", "<sup>$1</sup>");
            html = System.Text.RegularExpressions.Regex.Replace(html, @"\^\(([^)]+)\)", "<sup>$1</sup>");
            // Functions → bold
            html = System.Text.RegularExpressions.Regex.Replace(html, @"\b(sin|cos|tan|exp|log|sqrt|abs)\b", "<b>$1</b>");
            // Variables → italic
            html = System.Text.RegularExpressions.Regex.Replace(html, @"(?<![<\w/])([a-z])(?![a-zA-Z>])", "<var>$1</var>");
            html = html.Replace("pi", "\u03C0");
            return html;
        }

        // ─── $Viz multiline block accumulation ($Draw{..}, $Chart{..}, etc.) ─────

        private bool _insideVizBlock;
        private string _vizBlockHeader; // e.g. "$Draw{"
        private List<string> _vizBlockLines;

        // ─── #maxima / #end maxima — Execute Maxima CAS block ─────

        private bool _insideMaximaBlock;
        private int _maximaBlockStartLine;
        private List<string> _maximaBlockLines;

        private void ParseKeywordMaxima(ReadOnlySpan<char> s)
        {
            var spaceIdx = s.IndexOf(' ');
            var rest = spaceIdx > 0 ? s[(spaceIdx + 1)..].ToString().Trim() : "";

            if (string.IsNullOrEmpty(rest))
            {
                _insideMaximaBlock = true;
                _maximaBlockStartLine = _currentLine;
                _maximaBlockLines = new List<string>();
                return;
            }
            ExecuteMaximaCode(rest + ";");
        }

        private void ParseKeywordEndMaxima()
        {
            if (!_insideMaximaBlock || _maximaBlockLines == null) return;
            _insideMaximaBlock = false;
            var code = string.Join("\n", _maximaBlockLines);
            _maximaBlockLines = null;
            ExecuteMaximaCode(code);
        }

        private void ExecuteMaximaCode(string code)
        {
            if (!_calculate || !_isVisible) return;
            try
            {
                // Auto-discover any C:\maxima-*\bin\maxima.bat instead of
                // hard-coding 5.48.1. Falls back to PATH.
                string maximaCmd = null;
                var preferred = "C:/maxima-5.48.1/bin/maxima.bat";
                if (System.IO.File.Exists(preferred))
                    maximaCmd = preferred;
                else
                {
                    try
                    {
                        foreach (var dir in System.IO.Directory.EnumerateDirectories("C:/", "maxima-*"))
                        {
                            var bat = System.IO.Path.Combine(dir, "bin", "maxima.bat");
                            if (System.IO.File.Exists(bat)) { maximaCmd = bat; break; }
                        }
                    }
                    catch { }
                }
                maximaCmd ??= "maxima"; // PATH fallback

                var tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "calcpad_maxima_" + Guid.NewGuid().ToString("N")[..8] + ".mac");
                System.IO.File.WriteAllText(tempFile, "display2d:false$\n" + code, new System.Text.UTF8Encoding(false));

                // Maxima interprets backslashes in the batch path as escape
                // chars — force forward slashes.
                var batchPath = tempFile.Replace('\\', '/');
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = maximaCmd,
                    Arguments = $"--very-quiet --batch \"{batchPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };

                using var proc = System.Diagnostics.Process.Start(psi);
                var stdout = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(30000);

                try { System.IO.File.Delete(tempFile); } catch { }

                if (!string.IsNullOrWhiteSpace(stdout))
                {
                    var lines = stdout.Split('\n');
                    var isFirst = true;
                    foreach (var line in lines)
                    {
                        var trimmed = line.TrimEnd('\r').Trim();
                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("batch") ||
                            trimmed.StartsWith("read and") || trimmed.StartsWith("display2d"))
                            continue;
                        var lineId = isFirst ? HtmlIdForLine(_maximaBlockStartLine) : "";
                        isFirst = false;
                        _sb.Append($"<p{lineId}><span class=\"eq\">{System.Web.HttpUtility.HtmlEncode(trimmed)}</span></p>\n");
                    }
                }
            }
            catch (Exception ex)
            {
                _sb.Append($"<p{HtmlId}{HtmlLineClass}><span class=\"err\">Maxima error: {System.Web.HttpUtility.HtmlEncode(ex.Message)}</span></p>\n");
            }
        }

        // ─── #pip — Install Python packages ─────────────────────────

        /// <summary>Event raised when pip starts installing packages, for UI progress feedback.</summary>
        public static event Action<string> PipProgressChanged;

        private void ParseKeywordPip(ReadOnlySpan<char> s)
        {
            var spaceIdx = s.IndexOf(' ');
            if (spaceIdx < 0) return;
            var args = s[(spaceIdx + 1)..].ToString().Trim(); // "install numpy sympy"

            if (!_calculate) return;

            try
            {
                // Notify UI: installing packages
                PipProgressChanged?.Invoke($"pip {args}...");

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "pip",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = System.Diagnostics.Process.Start(psi);
                var stdout = proc.StandardOutput.ReadToEnd();
                var stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit(120000); // 2 min timeout for install

                // Restore UI status
                PipProgressChanged?.Invoke(null);

                if (_isVisible)
                {
                    var lines = stdout.Split('\n');
                    var hasOutput = false;
                    foreach (var line in lines)
                    {
                        var trimmed = line.TrimEnd('\r').Trim();
                        if (trimmed.StartsWith("Successfully") || trimmed.StartsWith("Installing") ||
                            trimmed.StartsWith("Collecting") || trimmed.StartsWith("Downloading"))
                        {
                            _sb.Append($"<p{HtmlId}{HtmlLineClass}><code>{System.Web.HttpUtility.HtmlEncode(trimmed)}</code></p>\n");
                            hasOutput = true;
                        }
                    }
                    // If nothing interesting in stdout, check if already satisfied
                    if (!hasOutput)
                    {
                        foreach (var line in lines)
                        {
                            var trimmed = line.TrimEnd('\r').Trim();
                            if (trimmed.StartsWith("Requirement already satisfied"))
                            {
                                _sb.Append($"<p{HtmlId}{HtmlLineClass}><code style=\"color:#888\">✓ {System.Web.HttpUtility.HtmlEncode(args)} (already installed)</code></p>\n");
                                break;
                            }
                        }
                    }
                    // Show errors if any
                    if (!string.IsNullOrWhiteSpace(stderr))
                    {
                        var errLines = stderr.Split('\n');
                        foreach (var line in errLines)
                        {
                            var trimmed = line.TrimEnd('\r').Trim();
                            if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("[notice]"))
                                _sb.Append($"<p{HtmlId}{HtmlLineClass}><span class=\"err\">pip: {System.Web.HttpUtility.HtmlEncode(trimmed)}</span></p>\n");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PipProgressChanged?.Invoke(null);
                if (_isVisible)
                    _sb.Append($"<p{HtmlId}{HtmlLineClass}><span class=\"err\">pip error: {System.Web.HttpUtility.HtmlEncode(ex.Message)}</span></p>\n");
            }
        }

        // ─── Inline versions (no <p> wrapper, for use inside text lines) ───

        /// <summary>Inline #sym — renders without &lt;p&gt; wrapper</summary>
        private void ParseInlineSym(string command)
        {
            if (string.IsNullOrEmpty(command)) return;
            // Normalize Unicode operators (·, ×, ⋅) → ASCII * for AngouriMath.
            var result = SymbolicProcessor.Process(NormalizeOps(command));
            if (result.IsError) { _sb.Append($"<span class=\"err\">{System.Web.HttpUtility.HtmlEncode(result.Error)}</span>"); return; }

            var savedIsVal = _isVal;
            _isVal = -1;
            _parser.IsCalculation = false;
            var hw = new HtmlWriter(Settings.Math, _parser.Phasor);
            var sb2 = new System.Text.StringBuilder();

            for (int i = 0; i < result.Parts.Length; i++)
            {
                var part = result.Parts[i]?.Trim();
                if (string.IsNullOrEmpty(part)) continue;
                if (i > 0) sb2.Append(" = ");

                if (part.StartsWith(SymbolicProcessor.TAG_NARY))
                {
                    var data = part[SymbolicProcessor.TAG_NARY.Length..];
                    var segs = data.Split('|');
                    if (segs.Length >= 4)
                    {
                        var subHtml = string.IsNullOrEmpty(segs[1]) ? "" : SymRenderExpr(segs[1]);
                        var supHtml = segs[2] == "0" ? "" : SymRenderExpr(segs[2]);
                        sb2.Append(hw.FormatNary(segs[0], subHtml, supHtml, SymRenderExpr(segs[3])));
                    }
                }
                else if (part.StartsWith(SymbolicProcessor.TAG_DERIV))
                {
                    var data = part[SymbolicProcessor.TAG_DERIV.Length..];
                    var segs = data.Split('|');
                    if (segs.Length >= 3)
                    {
                        sb2.Append(hw.FormatDivision(SymRenderExpr(segs[0]), SymRenderExpr(segs[1]), 0));
                        sb2.Append("\u2009");
                        sb2.Append(hw.AddBrackets(SymRenderExpr(segs[2])));
                    }
                }
                else if (part.StartsWith(SymbolicProcessor.TAG_FRAC))
                {
                    var data = part[SymbolicProcessor.TAG_FRAC.Length..];
                    var pipe = data.IndexOf('|');
                    if (pipe > 0)
                        sb2.Append(hw.FormatDivision(SymRenderExpr(data[..pipe]), SymRenderExpr(data[(pipe + 1)..]), 0));
                }
                else if (part.StartsWith(SymbolicProcessor.TAG_NABLA))
                {
                    const string nabla = "<span style=\"font-size:120%;color:#C080F0;font-family:Georgia Pro Light,serif\">\u2207</span>";
                    var data = part[SymbolicProcessor.TAG_NABLA.Length..];
                    var pipe = data.IndexOf('|');
                    var type = pipe > 0 ? data[..pipe] : data;
                    var body = pipe > 0 ? data[(pipe + 1)..] : "";
                    switch (type)
                    {
                        case "grad": sb2.Append(nabla + hw.AddBrackets(SymRenderExpr(body))); break;
                        case "div": sb2.Append(nabla + " \u00B7 <b>" + SymRenderExpr(body) + "</b>"); break;
                        case "curl": sb2.Append(nabla + " \u00D7 <b>" + SymRenderExpr(body) + "</b>"); break;
                        case "lap": sb2.Append(nabla + "\u00B2" + hw.AddBrackets(SymRenderExpr(body))); break;
                    }
                }
                else if (part.StartsWith(SymbolicProcessor.TAG_HTML))
                    sb2.Append(ResolveCalcpadMarkers(part[SymbolicProcessor.TAG_HTML.Length..]));
                else if (part.StartsWith(SymbolicProcessor.TAG_TAYLOR))
                {
                    var data = part[SymbolicProcessor.TAG_TAYLOR.Length..];
                    var pipe = data.IndexOf('|');
                    if (pipe > 0)
                    {
                        sb2.Append("<b>Taylor</b>" + hw.AddBrackets(SymRenderExpr(data[..pipe])));
                        sb2.Append($"<sub><var>n</var> = {data[(pipe + 1)..]}</sub>");
                    }
                }
                else if (part.StartsWith(SymbolicProcessor.TAG_SOLVE))
                {
                    var data = part[SymbolicProcessor.TAG_SOLVE.Length..];
                    var segs = data.Split('|');
                    if (segs.Length >= 2)
                    {
                        sb2.Append(SymRenderExpr(segs[0]) + " = { ");
                        for (int j = 1; j < segs.Length; j++)
                        {
                            if (j > 1) sb2.Append(" ;\u2009 ");
                            sb2.Append(SymRenderExpr(segs[j]));
                        }
                        sb2.Append(" }");
                    }
                }
                else
                    sb2.Append(SymRenderExpr(part));
            }

            _isVal = savedIsVal;
            _parser.IsCalculation = _isVal != -1;
            if (sb2.Length > 0)
                _sb.Append($"<span class=\"eq\">{sb2}</span>");
        }

        /// <summary>Inline #deq — renders without &lt;p&gt; wrapper</summary>
        /// <summary>
        /// Pre-define all variable names in an expression so MathParser
        /// doesn't interpret single letters like E, A, L, u as units.
        /// </summary>
        private void PreDefineVariables(string expr)
        {
            // Only stub variables that aren't already bound. Otherwise we'd
            // wipe out real values assigned elsewhere — e.g. a preceding
            // L = 60 would be reset to 0 the first time a #deq referenced L.
            var names = ExtractVariableNames(expr);
            foreach (var vn in names)
            {
                if (_parser.HasVariable(vn)) continue;
                try { _parser.SetVariable(vn, 0); } catch { }
            }
        }

        private void ParseInlineDeq(string expr)
        {
            if (string.IsNullOrEmpty(expr)) return;
            PreDefineVariables(expr);
            var parts = SplitByEqualsOutsideBrackets(expr);
            var savedIsVal = _isVal;
            _isVal = -1;
            _parser.IsCalculation = false;
            var sb2 = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i].Trim();
                if (string.IsNullOrEmpty(part)) continue;
                // Intentar renderizado especial primero (derivadas parciales, leibniz, primes)
                // para que las derivadas tipo -∂^2w/∂x^2 se rendericen como fraccion incluso
                // cuando aparecen en inline dentro de texto narrativo.
                var special = TryRenderDeqSpecial(part);
                if (!string.IsNullOrEmpty(special))
                {
                    if (i > 0) sb2.Append(" = ");
                    sb2.Append(special);
                    continue;
                }
                try
                {
                    _parser.Parse(part, false);
                    var html = _parser.ToHtml();
                    if (string.IsNullOrWhiteSpace(html))
                        html = new HtmlWriter(Settings.Math, _parser.Phasor).FormatVariable(part, string.Empty, false);
                    if (i > 0) sb2.Append(" = ");
                    sb2.Append(html);
                }
                catch
                {
                    if (i > 0) sb2.Append(" = ");
                    sb2.Append(new HtmlWriter(Settings.Math, _parser.Phasor).FormatVariable(part, string.Empty, false));
                }
            }
            _isVal = savedIsVal;
            _parser.IsCalculation = _isVal != -1;
            if (sb2.Length > 0)
            {
                // Apply the matrix-aware style so that when the rendered HTML
                // contains a <span class="matrix"> the equals sign is
                // vertically centered with the matrix (inline-flex, align-items:center).
                var sbStr = sb2.ToString();
                var eqStyle = EqStyleForMatrix(sbStr);
                _sb.Append($"<span class=\"eq\"{eqStyle}>{sbStr}</span>");
            }
        }

        /// <summary>
        /// Decide whether an inline math fragment (between apostrophes in a
        /// text line) should be rendered as display-only — mirroring the
        /// permissiveness of block-level #deq — instead of being evaluated
        /// through the normal parser (which rejects identities, Leibniz
        /// derivatives, and literal directive references).
        /// </summary>
        /// <remarks>
        /// Patterns routed to display-only:
        /// <list type="bullet">
        /// <item>Literal directive or function reference: starts with
        /// <c>#</c> or <c>$</c> (e.g. <c>#blk</c>, <c>$Plot</c>). These
        /// cannot be evaluated inline but the user wants to show them
        /// as code while narrating.</item>
        /// <item>Matrix literal: <c>[row1 | row2 | ...]</c>.</item>
        /// <item>Leibniz derivative: <c>d^n f / d^m x</c>, <c>∂f/∂x</c>,
        /// <c>d^2w/dxdy</c> — the normal inline parser tokenises these
        /// wrong (treats <c>dxdy</c> as units).</item>
        /// <item>Identity whose LHS is not a simple assignable target:
        /// <c>(a+b)^2 = a^2 + 2·a·b + b^2</c>. The normal parser insists
        /// the LHS of <c>=</c> be a variable or function name.</item>
        /// </list>
        /// </remarks>
        internal static bool ShouldRenderInlineAsDisplay(string expr)
        {
            if (string.IsNullOrWhiteSpace(expr)) return false;
            var trimmed = expr.Trim();
            // 1. Literal directive or function reference
            if (trimmed.Length > 0 && (trimmed[0] == '#' || trimmed[0] == '$'))
                return true;
            // 2. Matrix literal [..|..|..] — whole expression is a matrix
            if (trimmed.Length > 2 && trimmed[0] == '[' && trimmed[^1] == ']' &&
                trimmed.IndexOf('|') > 0)
                return true;
            // 3. Leibniz derivative pattern: d^n f / d^m x (or with ∂)
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed,
                @"[d∂]\^?\d*[a-zA-Zα-ωΑ-Ω_]+\s*/\s*[d∂]"))
                return true;
            // 3b. Integral call — Calcpad core does not have `integral` as a
            // builtin, but #deq/TryRenderIntegralSpecial renders it as ∫.
            // Route any inline expression containing `integral(` to display.
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed,
                @"(?i)\bintegral\s*\("))
                return true;
            // 4. Equality rules
            var eqIdx = FindTopLevelEquals(trimmed);
            if (eqIdx > 0)
            {
                var lhs = trimmed[..eqIdx].Trim();
                // 4a. Identity: LHS is not a simple variable/function
                if (!IsSimpleAssignmentTarget(lhs))
                    return true;
                // 4b. Assignment whose RHS is a matrix literal containing
                // pdiff() — these need our custom ∂ display path because the
                // normal parser produces ugly FD expressions. For diff/
                // integrate the parser handles them via $slope/$area natively,
                // and for plain matrices the parser must run normally.
                var rhs = trimmed[(eqIdx + 1)..].Trim();
                if (rhs.Length > 2 && rhs[0] == '[' && rhs[^1] == ']' &&
                    rhs.IndexOf('|') > 0 && rhs.Contains("pdiff("))
                    return true;
                // 4c. Assignment whose RHS contains pdiff() — render the
                // partial derivatives as ∂ notation (display) and let
                // RenderInlineAsDisplay's side-effect register the function
                // with FD expansion for numerical evaluation.
                // Note: diff() and integrate() use the normal Parse path —
                // they get rewritten to native $slope{...}/$area{...} solvers
                // by ExpandPdiff before parsing, so Calcpad's native rendering
                // takes care of the math notation and evaluation.
                if (rhs.Contains("pdiff("))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// A "simple assignment target" is a variable or function call header
        /// (no operators in the LHS). Accepts:
        /// <c>name</c>, <c>name_sub</c>, <c>name(args)</c>.
        /// Rejects: <c>(a+b)^2</c>, <c>a+b</c>, <c>a*b</c>, empty.
        /// </summary>
        private static bool IsSimpleAssignmentTarget(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            return System.Text.RegularExpressions.Regex.IsMatch(s.Trim(),
                @"^[a-zA-Zα-ωΑ-Ω_][a-zA-Zα-ωΑ-Ω0-9_]*(?:\([^)]*\))?$");
        }

        /// <summary>
        /// Render an inline math fragment as display-only (no evaluation).
        /// Delegates to <see cref="ParseInlineDeq"/> for derivatives,
        /// matrices and identities. Emits a <c>&lt;code&gt;</c> span for
        /// literal <c>#xxx</c> / <c>$xxx</c> directive references so they
        /// stand out from surrounding prose.
        /// </summary>
        internal void RenderInlineAsDisplay(string expr)
        {
            var trimmed = (expr ?? string.Empty).Trim();
            if (trimmed.Length == 0) return;
            // Literal directive or function reference → <code>
            if (trimmed[0] == '#' || trimmed[0] == '$')
            {
                _sb.Append($"<code>{System.Web.HttpUtility.HtmlEncode(trimmed)}</code>");
                return;
            }
            // Otherwise route through the same path as #deq inline, which
            // handles TryRenderDeqSpecial (matrix/Leibniz/partial/primes)
            // and falls back to _parser.Parse(part, false).ToHtml() with
            // IsCalculation=false so identities render without evaluating.
            ParseInlineDeq(trimmed);

            // SIDE-EFFECT: If this is an assignment containing symbolic
            // operators (pdiff/diff/integrate) or native solvers ($area/$slope
            // etc.) — or just a matrix literal that might be a function body —
            // also register the function/variable with the parser so that
            // subsequent calls/references evaluate. Display already shows
            // clean math notation; this only adds evaluation.
            bool hasSymOp = trimmed.Contains("pdiff(") || trimmed.Contains("diff(") ||
                            trimmed.Contains("integrate(") ||
                            trimmed.Contains("$area") || trimmed.Contains("$slope") ||
                            trimmed.Contains("$root") || trimmed.Contains("$sum") ||
                            trimmed.Contains("$product");
            bool hasMatrixRhs = false;
            if (trimmed.IndexOf('=') > 0)
            {
                int eqIdx = FindTopLevelEquals(trimmed);
                if (eqIdx > 0)
                {
                    var rhsCheck = trimmed[(eqIdx + 1)..].Trim();
                    hasMatrixRhs = rhsCheck.Length > 2 && rhsCheck[0] == '['
                                   && rhsCheck[^1] == ']';
                }
            }
            if ((hasSymOp || hasMatrixRhs) && trimmed.IndexOf('=') > 0)
            {
                int eqIdx = FindTopLevelEquals(trimmed);
                if (eqIdx > 0)
                {
                    var lhs = trimmed[..eqIdx].Trim();
                    if (IsSimpleAssignmentTarget(lhs))
                    {
                        try
                        {
                            var expanded = ExpandPdiff(trimmed);
                            var savedIsCalc = _parser.IsCalculation;
                            _parser.IsCalculation = true;
                            _parser.Parse(expanded);
                            _parser.IsCalculation = savedIsCalc;
                        }
                        catch { /* swallow: display already rendered */ }
                    }
                }
            }
        }

        private static string _lastDeqSeparator = " = ";
        private static List<string> SplitByEqualsOutsideBrackets(string s)
        {
            var parts = new List<string>();
            var depth = 0; // [] depth
            var pDepth = 0; // () depth
            var start = 0;
            for (int i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c == '[') depth++;
                else if (c == ']') depth--;
                else if (c == '(') pDepth++;
                else if (c == ')') pDepth--;
                else if ((c == '=' || c == '≈' || c == '≡' || c == '≠') && depth == 0 && pDepth == 0)
                {
                    // Skip == (comparison)
                    if (c == '=' && i + 1 < s.Length && s[i + 1] == '=') { i++; continue; }
                    // Skip <=, >=, !=
                    if (c == '=' && i > 0 && (s[i - 1] == '<' || s[i - 1] == '>' || s[i - 1] == '!')) continue;
                    parts.Add(s[start..i]);
                    // Store the separator so we can render ≈ instead of =
                    _lastDeqSeparator = c == '=' ? " = " : $" {c} ";
                    start = i + 1;
                    // Handle multi-byte UTF chars
                    if (c != '=') while (start < s.Length && s[start] == ' ') start++;
                }
            }
            parts.Add(s[start..]);
            return parts;
        }

        /// <summary>
        /// Expand pdiff(expr; var) calls in an expression using AngouriMath symbolic differentiation.
        /// This allows using partial derivatives inline in math expressions.
        /// Example: pdiff((1-ξ)*(1-η)/4; ξ) → -(1-η)/4
        /// </summary>
        /// <summary>
        /// Expand diff(expr; var) or diff(expr; var; n) calls.
        /// Uses Calcpad's native $slope{expr @ var = var} for n=1.
        /// For n=2+, uses central FD on the n-th order.
        /// </summary>
        private static string ExpandDiff(string expression)
        {
            if (!expression.Contains("diff(")) return expression;
            var result = new System.Text.StringBuilder(expression.Length);
            int pos = 0;
            while (pos < expression.Length)
            {
                int diffStart = -1;
                // Find "diff(" not preceded by 'p' (avoid pdiff)
                int idx = pos;
                while (idx < expression.Length)
                {
                    int hit = expression.IndexOf("diff(", idx, StringComparison.Ordinal);
                    if (hit < 0) break;
                    // Reject if this is "pdiff(" — back-char is 'p'
                    if (hit > 0 && expression[hit - 1] == 'p') { idx = hit + 5; continue; }
                    // Reject if part of an identifier (preceded by alnum/underscore)
                    if (hit > 0 && (char.IsLetterOrDigit(expression[hit - 1]) || expression[hit - 1] == '_'))
                    { idx = hit + 5; continue; }
                    diffStart = hit;
                    break;
                }
                if (diffStart < 0)
                {
                    result.Append(expression, pos, expression.Length - pos);
                    break;
                }
                result.Append(expression, pos, diffStart - pos);
                int openParen = diffStart + 4;
                int depth = 1;
                int i = openParen + 1;
                while (i < expression.Length && depth > 0)
                {
                    if (expression[i] == '(') depth++;
                    else if (expression[i] == ')') depth--;
                    i++;
                }
                if (depth != 0)
                {
                    result.Append(expression, diffStart, i - diffStart);
                    pos = i;
                    continue;
                }
                var argsStr = expression[(openParen + 1)..(i - 1)];
                var args = SplitArgs(argsStr);
                if (args.Length >= 2)
                {
                    var exprArg = args[0].Trim();
                    var varArg = args[1].Trim();
                    int n = args.Length >= 3 && int.TryParse(args[2].Trim(), out var nn) ? nn : 1;

                    bool isUserFunc = false;
                    int fp = exprArg.IndexOf('(');
                    if (fp > 0 && exprArg.IndexOf(';', fp) > 0)
                        isUserFunc = true;

                    // Translate to Calcpad's native $slope{...} with renaming
                    // to avoid x=x shadowing. For n>=2, use FD as Calcpad's
                    // $slope only does first order.
                    if (n == 1)
                    {
                        string dummy = "__t__";
                        string exprRenamed = SubstWordBoundary(exprArg, varArg, dummy);
                        result.Append($"$slope{{{exprRenamed} @ {dummy} = {varArg}}}");
                    }
                    else
                    {
                        var numExpr = NumericalDiff(exprArg, varArg, n);
                        if (numExpr != null)
                            result.Append($"({numExpr})");
                        else
                            result.Append(expression, diffStart, i - diffStart);
                    }
                }
                else
                    result.Append(expression, diffStart, i - diffStart);
                pos = i;
            }
            return result.ToString();
        }

        /// <summary>
        /// Numerical derivative using central finite differences for any expression.
        /// 1st order: (f(x+h) - f(x-h))/(2h)
        /// 2nd order: (f(x+h) - 2f(x) + f(x-h))/h²
        /// Higher orders: not supported here, returns null.
        /// </summary>
        private static string? NumericalDiff(string expr, string variable, int order)
        {
            const string h = "0.0001";
            // Simple identifier substitution: replace standalone `variable` with (variable+h), (variable-h)
            // We rebuild expression by treating expr as opaque text but substituting only
            // standalone occurrences of `variable`.
            string subst(string e, string repl)
            {
                var sb = new System.Text.StringBuilder();
                int i = 0;
                while (i < e.Length)
                {
                    // Find next occurrence of variable
                    int idx = e.IndexOf(variable, i, StringComparison.Ordinal);
                    if (idx < 0) { sb.Append(e, i, e.Length - i); break; }
                    // Check word boundaries
                    bool leftOk = idx == 0 || !(char.IsLetterOrDigit(e[idx - 1]) || e[idx - 1] == '_');
                    int endIdx = idx + variable.Length;
                    bool rightOk = endIdx >= e.Length || !(char.IsLetterOrDigit(e[endIdx]) || e[endIdx] == '_');
                    if (leftOk && rightOk)
                    {
                        sb.Append(e, i, idx - i);
                        sb.Append(repl);
                        i = endIdx;
                    }
                    else
                    {
                        sb.Append(e, i, endIdx - i);
                        i = endIdx;
                    }
                }
                return sb.ToString();
            }
            if (order == 1)
            {
                var fp = subst(expr, $"({variable} + {h})");
                var fm = subst(expr, $"({variable} - {h})");
                return $"({fp} - {fm})/(2*{h})";
            }
            if (order == 2)
            {
                var fp = subst(expr, $"({variable} + {h})");
                var f0 = expr;
                var fm = subst(expr, $"({variable} - {h})");
                return $"({fp} - 2*({f0}) + {fm})/({h}^2)";
            }
            return null;
        }

        /// <summary>
        /// Expand integrate(expr; var; a; b) calls to numerical Simpson's rule
        /// or AngouriMath symbolic integration. For user functions, uses
        /// composite Simpson's rule with N=20 subintervals.
        /// </summary>
        private static string ExpandIntegrate(string expression)
        {
            if (!expression.Contains("integrate(")) return expression;
            var result = new System.Text.StringBuilder(expression.Length);
            int pos = 0;
            while (pos < expression.Length)
            {
                int idx = expression.IndexOf("integrate(", pos, StringComparison.Ordinal);
                if (idx < 0) { result.Append(expression, pos, expression.Length - pos); break; }
                // Reject if part of a longer identifier
                if (idx > 0 && (char.IsLetterOrDigit(expression[idx - 1]) || expression[idx - 1] == '_'))
                {
                    result.Append(expression, pos, idx + 10 - pos);
                    pos = idx + 10;
                    continue;
                }
                result.Append(expression, pos, idx - pos);
                int openParen = idx + 9;
                int depth = 1;
                int i = openParen + 1;
                while (i < expression.Length && depth > 0)
                {
                    if (expression[i] == '(') depth++;
                    else if (expression[i] == ')') depth--;
                    i++;
                }
                if (depth != 0)
                {
                    result.Append(expression, idx, i - idx);
                    pos = i;
                    continue;
                }
                var argsStr = expression[(openParen + 1)..(i - 1)];
                var args = SplitArgs(argsStr);
                if (args.Length >= 4)
                {
                    var exprArg = args[0].Trim();
                    var varArg = args[1].Trim();
                    var aArg = args[2].Trim();
                    var bArg = args[3].Trim();

                    // Use Calcpad's native $area{expr @ var = a : b} — works
                    // for both pure expressions and user-function calls, and
                    // evaluates numerically with Calcpad's adaptive integrator.
                    result.Append($"$area{{{exprArg} @ {varArg} = {aArg} : {bArg}}}");
                }
                else
                    result.Append(expression, idx, i - idx);
                pos = i;
            }
            return result.ToString();
        }

        /// <summary>
        /// Numerical definite integration using composite Simpson's rule with N=20.
        /// Returns Calcpad expression text.
        /// </summary>
        private static string? NumericalIntegrate(string expr, string variable, string a, string b)
        {
            const int N = 20; // even, composite Simpson
            string subst(string e, string repl)
            {
                var sb = new System.Text.StringBuilder();
                int i = 0;
                while (i < e.Length)
                {
                    int idx2 = e.IndexOf(variable, i, StringComparison.Ordinal);
                    if (idx2 < 0) { sb.Append(e, i, e.Length - i); break; }
                    bool leftOk = idx2 == 0 || !(char.IsLetterOrDigit(e[idx2 - 1]) || e[idx2 - 1] == '_');
                    int endIdx = idx2 + variable.Length;
                    bool rightOk = endIdx >= e.Length || !(char.IsLetterOrDigit(e[endIdx]) || e[endIdx] == '_');
                    if (leftOk && rightOk)
                    {
                        sb.Append(e, i, idx2 - i);
                        sb.Append(repl);
                        i = endIdx;
                    }
                    else
                    {
                        sb.Append(e, i, endIdx - i);
                        i = endIdx;
                    }
                }
                return sb.ToString();
            }
            // h = (b - a)/N
            string hExpr = $"(({b}) - ({a}))/{N}";
            // Composite Simpson: h/3 * (f0 + fN + 4*Σodd + 2*Σeven)
            var sb2 = new System.Text.StringBuilder();
            sb2.Append("(");
            sb2.Append($"({hExpr})/3 * (");
            // f at a (i=0)
            sb2.Append("(").Append(subst(expr, $"({a})")).Append(") + ");
            // f at b (i=N)
            sb2.Append("(").Append(subst(expr, $"({b})")).Append(")");
            // Sum odd indices (4*) and even (2*)
            sb2.Append(" + 4*(");
            for (int k = 1; k < N; k += 2)
            {
                if (k > 1) sb2.Append(" + ");
                var xk = $"(({a}) + {k}*{hExpr})";
                sb2.Append("(").Append(subst(expr, xk)).Append(")");
            }
            sb2.Append(") + 2*(");
            for (int k = 2; k < N; k += 2)
            {
                if (k > 2) sb2.Append(" + ");
                var xk = $"(({a}) + {k}*{hExpr})";
                sb2.Append("(").Append(subst(expr, xk)).Append(")");
            }
            sb2.Append(")))");
            return sb2.ToString();
        }

        private static string ExpandPdiff(string expression)
        {
            // Translate symbolic operators to Calcpad's native solvers so the
            // parser renders them with proper math notation (∫, d/dx) and
            // evaluates them numerically:
            //   integrate(expr; var; a; b) → $area{expr @ var = a : b}
            //   diff(expr; var)            → $slope{expr_renamed @ __t__ = var}
            //   diff(expr; var; n)         → central FD for n>=2
            //   pdiff(expr; var)           → $slope{expr_renamed @ __t__ = var}
            // Renaming the bound variable to a unique placeholder avoids
            // shadowing when expr already references `var` (e.g. f(x; y)).
            // Run translations iteratively to handle nested calls like
            // integrate(integrate(g(x;y); y; 0; 1); x; 0; 1).
            for (int iter = 0; iter < 8; iter++)
            {
                var prev = expression;
                expression = ExpandDiff(expression);
                expression = ExpandIntegrate(expression);
                if (expression == prev) break;
            }
            if (!expression.Contains("pdiff(")) return expression;

            var result = new System.Text.StringBuilder(expression.Length);
            int pos = 0;
            while (pos < expression.Length)
            {
                int pdiffStart = expression.IndexOf("pdiff(", pos, StringComparison.Ordinal);
                if (pdiffStart < 0) { result.Append(expression, pos, expression.Length - pos); break; }
                result.Append(expression, pos, pdiffStart - pos);
                int openParen = pdiffStart + 5;
                int depth = 1;
                int i = openParen + 1;
                while (i < expression.Length && depth > 0)
                {
                    if (expression[i] == '(') depth++;
                    else if (expression[i] == ')') depth--;
                    i++;
                }
                if (depth != 0)
                {
                    result.Append(expression, pdiffStart, i - pdiffStart);
                    pos = i;
                    continue;
                }
                var argsStr = expression[(openParen + 1)..(i - 1)];
                var args = SplitArgs(argsStr);
                if (args.Length >= 2)
                {
                    var exprArg = args[0].Trim();
                    var varArg = args[1].Trim();
                    // Rewrite to native $slope, renaming `varArg` → `__t__` in expr
                    string dummy = "__t__";
                    string exprRenamed = SubstWordBoundary(exprArg, varArg, dummy);
                    result.Append($"$slope{{{exprRenamed} @ {dummy} = {varArg}}}");
                }
                else
                    result.Append(expression, pdiffStart, i - pdiffStart);
                pos = i;
            }
            return result.ToString();
        }

        /// <summary>Replace standalone occurrences of `name` with `repl` in `expr`.</summary>
        private static string SubstWordBoundary(string expr, string name, string repl)
        {
            var sb = new System.Text.StringBuilder();
            int i = 0;
            while (i < expr.Length)
            {
                int idx = expr.IndexOf(name, i, StringComparison.Ordinal);
                if (idx < 0) { sb.Append(expr, i, expr.Length - i); break; }
                bool leftOk = idx == 0 || !(char.IsLetterOrDigit(expr[idx - 1]) || expr[idx - 1] == '_');
                int endIdx = idx + name.Length;
                bool rightOk = endIdx >= expr.Length || !(char.IsLetterOrDigit(expr[endIdx]) || expr[endIdx] == '_');
                if (leftOk && rightOk)
                {
                    sb.Append(expr, i, idx - i);
                    sb.Append(repl);
                    i = endIdx;
                }
                else
                {
                    sb.Append(expr, i, endIdx - i);
                    i = endIdx;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Generate numerical partial derivative using central finite differences.
        /// pdiff(f(x; y); x) → (f(x+1e-7; y) - f(x-1e-7; y))/(2*1e-7)
        /// </summary>
        private static string? NumericalPdiff(string expr, string variable)
        {
            const string h = "0.0000001";

            // Check if expr is a function call like f(a; b; c)
            int parenOpen = expr.IndexOf('(');
            if (parenOpen < 0) return null;

            string funcName = expr[..parenOpen].Trim();
            int parenClose = expr.LastIndexOf(')');
            if (parenClose <= parenOpen) return null;

            string argsInner = expr[(parenOpen + 1)..parenClose];
            var funcArgs = SplitArgs(argsInner);

            // Find which argument matches the variable
            int varIdx = -1;
            for (int j = 0; j < funcArgs.Length; j++)
            {
                if (funcArgs[j].Trim() == variable)
                {
                    varIdx = j;
                    break;
                }
            }

            if (varIdx < 0) return null;

            // Build f(... x+h ...) and f(... x-h ...)
            var argsPlus = new string[funcArgs.Length];
            var argsMinus = new string[funcArgs.Length];
            for (int j = 0; j < funcArgs.Length; j++)
            {
                var a = funcArgs[j].Trim();
                if (j == varIdx)
                {
                    argsPlus[j] = $"{a} + {h}";
                    argsMinus[j] = $"{a} - {h}";
                }
                else
                {
                    argsPlus[j] = a;
                    argsMinus[j] = a;
                }
            }

            var fPlus = $"{funcName}({string.Join("; ", argsPlus)})";
            var fMinus = $"{funcName}({string.Join("; ", argsMinus)})";
            return $"({fPlus} - {fMinus})/(2*{h})";
        }

        /// <summary>Split arguments by semicolon, respecting nested parentheses and brackets</summary>
        private static string[] SplitArgs(string s)
        {
            var args = new List<string>();
            int depth = 0;
            int start = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '(' || c == '[') depth++;
                else if (c == ')' || c == ']') depth--;
                else if (c == ';' && depth == 0)
                {
                    args.Add(s[start..i]);
                    start = i + 1;
                }
            }
            args.Add(s[start..]);
            return args.ToArray();
        }
    }
}