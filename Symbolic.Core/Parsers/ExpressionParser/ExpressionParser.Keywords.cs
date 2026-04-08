
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
            SkipLine,
            Svg,
            End_Svg
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
                case Keyword.Svg:
                    ParseKeywordSvg(s);
                    return KeywordResult.Continue;
                case Keyword.End_Svg:
                    ParseKeywordEndSvg();
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
            // Saltar "#deq "
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

                if (i > 0) sb2.Append(_lastDeqSeparator);

                // Try special rendering for derivatives and primes
                var specialHtml = TryRenderDeqSpecial(part);
                if (specialHtml != null)
                {
                    sb2.Append(specialHtml);
                    continue;
                }

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
                    sb2.Append(html);
                }
                catch
                {
                    // Si el parser falla, renderizar como texto con formato de variable
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

        /// <summary>
        /// Try to render special #deq patterns that the MathParser can't handle:
        /// - Leibniz derivatives: d^2v/dx^2, dv/dx, d^4v/dx^4, ∂^2u/∂x^2
        /// - Prime notation: v'(x), v''(x), f''''(x)
        /// - Partial derivatives: ∂f/∂x
        /// Returns null if the part is not a special pattern.
        /// </summary>
        private string TryRenderDeqSpecial(string part)
        {
            // --- Pattern 1: Leibniz derivative fractions ---
            // d^nf/dx^n, d^2v/dx^2, df/dx, ∂f/∂x, ∂^2u/∂x^2, ∂^2u/∂x∂y
            var leibnizMatch = System.Text.RegularExpressions.Regex.Match(part,
                @"^([d∂](?:\^(\d+))?)(\w+)\s*/\s*([d∂])(\w)(?:\^(\d+))?(?:([d∂])(\w)(?:\^(\d+))?)?$");
            if (leibnizMatch.Success)
            {
                var dSym = leibnizMatch.Groups[1].Value;   // d or ∂ or d^2 or ∂^2
                var order = leibnizMatch.Groups[2].Value;    // order number (empty for 1st)
                var func = leibnizMatch.Groups[3].Value;     // function name (v, u, f...)
                var dSym2 = leibnizMatch.Groups[4].Value;    // d or ∂ in denominator
                var var1 = leibnizMatch.Groups[5].Value;     // variable (x, y...)
                var order2 = leibnizMatch.Groups[6].Value;   // order in denominator
                var dSym3 = leibnizMatch.Groups[7].Value;    // second ∂ in denominator (mixed)
                var var2 = leibnizMatch.Groups[8].Value;     // second variable
                var order3 = leibnizMatch.Groups[9].Value;   // second order

                // Build numerator: d²v or ∂²u
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

                return $"<span class=\"dvc\">{numSb}<span class=\"dvl\"></span>{denSb}</span>";
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
            return s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
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
                    _sb.Append($"<p{HtmlId}><span class=\"err\">{System.Web.HttpUtility.HtmlEncode(stderr.Trim())}</span></p>\n");
                PipProgressChanged?.Invoke(null);
            }
            catch (Exception ex)
            {
                PipProgressChanged?.Invoke(null);
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
                _sb.Append($"<p{HtmlId}><span class=\"err\">Maxima error: {System.Web.HttpUtility.HtmlEncode(ex.Message)}</span></p>\n");
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
                            _sb.Append($"<p{HtmlId}><code>{System.Web.HttpUtility.HtmlEncode(trimmed)}</code></p>\n");
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
                                _sb.Append($"<p{HtmlId}><code style=\"color:#888\">✓ {System.Web.HttpUtility.HtmlEncode(args)} (already installed)</code></p>\n");
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
                                _sb.Append($"<p{HtmlId}><span class=\"err\">pip: {System.Web.HttpUtility.HtmlEncode(trimmed)}</span></p>\n");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PipProgressChanged?.Invoke(null);
                if (_isVisible)
                    _sb.Append($"<p{HtmlId}><span class=\"err\">pip error: {System.Web.HttpUtility.HtmlEncode(ex.Message)}</span></p>\n");
            }
        }

        // ─── Inline versions (no <p> wrapper, for use inside text lines) ───

        /// <summary>Inline #sym — renders without &lt;p&gt; wrapper</summary>
        private void ParseInlineSym(string command)
        {
            if (string.IsNullOrEmpty(command)) return;
            var result = SymbolicProcessor.Process(command);
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
        private void ParseInlineDeq(string expr)
        {
            if (string.IsNullOrEmpty(expr)) return;
            var parts = SplitByEqualsOutsideBrackets(expr);
            var savedIsVal = _isVal;
            _isVal = -1;
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
                _sb.Append($"<span class=\"eq\">{sb2}</span>");
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
    }
}