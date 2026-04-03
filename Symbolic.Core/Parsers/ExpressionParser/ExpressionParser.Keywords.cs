
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
            Sym,
            End_Sym,
            Python,
            End_Python,
            Maxima,
            End_Maxima,
            Pip,
            SkipLine
        }
        private enum KeywordResult  
        {
            None,
            Continue,
            Break
        }

        private Keyword _previousKeyword = Keyword.None;
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
                    ParseKeywordDeq(s);
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
                _sb.Append($"<p{HtmlId} class=\"cond\">#pause</p>");

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
                    _sb.Append($"<p{HtmlId} class=\"cond\">#repeat</p><div class=\"indent\">");
                else
                {
                    try
                    {
                        _parser.Parse(expression);
                        _sb.Append($"<p{HtmlId}><span class=\"cond\">#repeat</span> <span class=\"eq\">{_parser.ToHtml()}</span></p><div class=\"indent\">");
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
                            _sb.Append($"<p{HtmlId}><span class=\"cond\">#for</span> <span class=\"eq\">{varHtml} = {startHtml} : {endHtml}</span></p><div class=\"indent\">");
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
                    _sb.Append($"<p{HtmlId}><span class=\"cond\">#while</span> ");
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
                _sb.Append($"</div><p{HtmlId} class=\"cond\">#loop</p>");
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
                _sb.Append($"<p{HtmlId} class=\"cond\">#break</p>");

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
                _sb.Append($"<p{HtmlId} class=\"cond\">#continue</p>");
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
                _sb.Append($"<p><span{HtmlId} class=\"cond\">#read</span> {s[5..]}</p>");
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
                _sb.Append($"<p><span{HtmlId} class=\"cond\">#write</span> {s[6..]}</p>");
        }

        private void ReportDataExchageResult(ReadWriteOptions options, string command)
        {
            var url = $"file:///{options.FullPath.Replace('\\', '/')}";
            _sb.Append($"<p{HtmlId}>")
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
        private void ParseKeywordDeq(ReadOnlySpan<char> s)
        {
            _sb.Append($"<!-- FORMEQ CALLED: len={s.Length} -->");
            // Saltar "#formeq "
            var spaceIdx = s.IndexOf(' ');
            if (spaceIdx < 0) return;
            var expr = s[(spaceIdx + 1)..].ToString();

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

                try
                {
                    _parser.Parse(part, false);
                    var html = _parser.ToHtml();
                    if (string.IsNullOrWhiteSpace(html))
                    {
                        // Fallback: renderizar como variable formateada
                        var w = new HtmlWriter(Settings.Math, _parser.Phasor);
                        html = w.FormatVariable(part, string.Empty, false);
                    }
                    if (i > 0) sb2.Append(" = ");
                    sb2.Append(html);
                }
                catch
                {
                    // Si el parser falla, renderizar como texto con formato de variable
                    if (i > 0) sb2.Append(" = ");
                    var w = new HtmlWriter(Settings.Math, _parser.Phasor);
                    sb2.Append(w.FormatVariable(part, string.Empty, false));
                }
            }

            _isVal = savedIsVal;
            _parser.IsCalculation = _isVal != -1;

            if (sb2.Length > 0)
                _sb.Append($"<p{HtmlId}><span class=\"eq\">{sb2}</span></p>\n");
            else
                _sb.Append($"<p{HtmlId}><span class=\"err\">#formeq: no output for '{expr}'</span></p>\n");
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

            var result = SymbolicProcessor.Process(command);
            if (!_isVisible) return;

            if (result.IsError)
            {
                _sb.Append($"<p{HtmlId}><span class=\"err\">{System.Web.HttpUtility.HtmlEncode(result.Error)}</span></p>\n");
                return;
            }

            var savedIsVal = _isVal;
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
            _parser.IsCalculation = _isVal != -1;

            if (sb2.Length > 0)
                _sb.Append($"<p{HtmlId}><span class=\"eq\">{sb2}</span></p>\n");
        }

        // Render a Calcpad expression to HTML via MathParser
        private string SymRenderExpr(string expr)
        {
            // Special: expressions with ∂ (partial derivative symbol) can't be parsed
            // Render them as HTML directly: ∂ → italic, ^n → superscript
            if (expr.Contains('\u2202'))
                return RenderPartialSymbol(expr);

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

        // Render ∂, ∂x, ∂^2, ∂x^2, etc. as proper HTML
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
        private List<string> _pythonBlockLines;

        private void ParseKeywordPython(ReadOnlySpan<char> s)
        {
            var spaceIdx = s.IndexOf(' ');
            var rest = spaceIdx > 0 ? s[(spaceIdx + 1)..].ToString().Trim() : "";

            // #python alone → start block mode
            if (string.IsNullOrEmpty(rest))
            {
                _insidePythonBlock = true;
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

        private void ExecutePythonCode(string code)
        {
            if (!_calculate || !_isVisible) return;
            try
            {
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

                using var proc = System.Diagnostics.Process.Start(psi);
                var stdout = proc.StandardOutput.ReadToEnd();
                var stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit(30000);

                // Clean up temp file
                try { System.IO.File.Delete(tempFile); } catch { }

                // Render output
                if (!string.IsNullOrWhiteSpace(stdout))
                {
                    var lines = stdout.Split('\n');
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
                            // Render with CalcpadCE template: try as math expression, fallback to text
                            _sb.Append($"<p{HtmlId}><span class=\"eq\">{RenderPythonLine(trimmed)}</span></p>\n");
                        }
                    }
                }
                if (!string.IsNullOrWhiteSpace(stderr))
                    _sb.Append($"<p{HtmlId}><span class=\"err\">{System.Web.HttpUtility.HtmlEncode(stderr.Trim())}</span></p>\n");
            }
            catch (Exception ex)
            {
                _sb.Append($"<p{HtmlId}><span class=\"err\">Python error: {System.Web.HttpUtility.HtmlEncode(ex.Message)}</span></p>\n");
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

        // ─── #maxima / #end maxima — Execute Maxima CAS block ─────

        private bool _insideMaximaBlock;
        private List<string> _maximaBlockLines;

        private void ParseKeywordMaxima(ReadOnlySpan<char> s)
        {
            var spaceIdx = s.IndexOf(' ');
            var rest = spaceIdx > 0 ? s[(spaceIdx + 1)..].ToString().Trim() : "";

            if (string.IsNullOrEmpty(rest))
            {
                _insideMaximaBlock = true;
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
                // Find Maxima
                var maximaCmd = "C:/maxima-5.48.1/bin/maxima.bat";
                if (!System.IO.File.Exists(maximaCmd))
                    maximaCmd = "maxima"; // fallback to PATH

                var tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "calcpad_maxima_" + Guid.NewGuid().ToString("N")[..8] + ".mac");
                System.IO.File.WriteAllText(tempFile, "display2d:false$\n" + code, new System.Text.UTF8Encoding(false));

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = maximaCmd,
                    Arguments = $"--very-quiet --batch \"{tempFile}\"",
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
                    foreach (var line in lines)
                    {
                        var trimmed = line.TrimEnd('\r').Trim();
                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("batch") ||
                            trimmed.StartsWith("read and") || trimmed.StartsWith("display2d"))
                            continue;
                        _sb.Append($"<p{HtmlId}><span class=\"eq\">{System.Web.HttpUtility.HtmlEncode(trimmed)}</span></p>\n");
                    }
                }
            }
            catch (Exception ex)
            {
                _sb.Append($"<p{HtmlId}><span class=\"err\">Maxima error: {System.Web.HttpUtility.HtmlEncode(ex.Message)}</span></p>\n");
            }
        }

        // ─── #pip — Install Python packages ─────────────────────────

        private void ParseKeywordPip(ReadOnlySpan<char> s)
        {
            var spaceIdx = s.IndexOf(' ');
            if (spaceIdx < 0) return;
            var args = s[(spaceIdx + 1)..].ToString().Trim(); // "install numpy sympy"

            if (!_calculate) return;

            try
            {
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
                proc.WaitForExit(120000); // 2 min timeout for install

                if (_isVisible)
                {
                    // Show only relevant lines (skip "Requirement already satisfied")
                    var lines = stdout.Split('\n');
                    foreach (var line in lines)
                    {
                        var trimmed = line.TrimEnd('\r').Trim();
                        if (trimmed.StartsWith("Successfully") || trimmed.StartsWith("Installing") ||
                            trimmed.StartsWith("Collecting"))
                            _sb.Append($"<p{HtmlId}><code>{System.Web.HttpUtility.HtmlEncode(trimmed)}</code></p>\n");
                    }
                }
            }
            catch (Exception ex)
            {
                if (_isVisible)
                    _sb.Append($"<p{HtmlId}><span class=\"err\">pip error: {System.Web.HttpUtility.HtmlEncode(ex.Message)}</span></p>\n");
            }
        }

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
                else if (c == '=' && depth == 0 && pDepth == 0)
                {
                    // Skip == (comparison)
                    if (i + 1 < s.Length && s[i + 1] == '=') { i++; continue; }
                    // Skip <=, >=, !=
                    if (i > 0 && (s[i - 1] == '<' || s[i - 1] == '>' || s[i - 1] == '!')) continue;
                    parts.Add(s[start..i]);
                    start = i + 1;
                }
            }
            parts.Add(s[start..]);
            return parts;
        }
    }
}