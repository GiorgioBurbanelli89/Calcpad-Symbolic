using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Calcpad.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Calcpad.Wpf.MathEditor
{
    /// <summary>
    /// MathCanvas: Canvas WPF nativo con edicion directa + parser real de Calcpad.
    /// - El teclado va directo al Canvas (PreviewKeyDown/PreviewTextInput en UserControl)
    /// - Cada linea se parsea con ExpressionParser para evaluacion
    /// - El HTML resultado se convierte a MathElements para renderizado visual
    /// </summary>
    public partial class MathCanvasControl : UserControl
    {
        // ─── Data ───
        private List<string> _rawLines = new() { "" };
        private int _currentLineIndex = 0;
        private int _cursorPosition = 0;

        // ─── Parser ───
        private ExpressionParser _parser;

        // ─── Visual settings ───
        private double _fontSize = 14.0;  // Same as Code panel (RichTextBox FontSize=14)
        private double _zoom = 1.0;
        private const double LeftMargin = 35.0;  // Just enough for line numbers
        private const double TopMargin = 2.0;  // Minimal top margin
        private const double LineSpacing = 8.0;
        private const double MinLineHeight = 24.0;

        // ─── Cursor blink ───
        private DispatcherTimer _cursorTimer;
        private bool _cursorVisible = true;

        // ─── Debounce: delay parser evaluation while typing ───
        private DispatcherTimer _parseTimer;
        private string _cachedHtmlResult = null;
        private bool _htmlDirty = true;

        // ─── Per-line HTML cache: only re-parse when lines change ───
        private List<string> _cachedLineHtmls = new();
        private List<string> _lastParsedLines = new();

        // ─── Embedded WebView2 for graphs ───
        private List<WebView2> _graphWebViews = new();
        private string _vizScriptPath = ""; // Path to calcpad-viz UMD bundle

        // ─── Selection ───
        private bool _isDragging = false;
        private int _selStartLine = -1, _selStartPos = -1;
        private int _selEndLine = -1, _selEndPos = -1;
        private bool HasSelection => _selStartLine >= 0 && _selEndLine >= 0 &&
            (_selStartLine != _selEndLine || _selStartPos != _selEndPos);

        // ─── Layout cache ───
        private List<double> _lineHeights = new();
        private List<double> _lineYPositions = new();

        // ─── DPI ───
        private double _dpi = 1.0;

        public event Action<string> TextChanged;

        public MathCanvasControl()
        {
            InitializeComponent();

            // Keyboard events on the UserControl itself (NOT Canvas)
            // This ensures keyboard works regardless of Canvas focus state
            PreviewKeyDown += OnKeyDown;
            PreviewTextInput += OnTextInput;

            // Mouse events on Canvas
            EditorCanvas.MouseLeftButtonDown += EditorCanvas_MouseLeftButtonDown;
            EditorCanvas.MouseLeftButtonUp += EditorCanvas_MouseLeftButtonUp;
            EditorCanvas.MouseMove += EditorCanvas_MouseMove;

            // Cursor blink timer
            _cursorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(530) };
            _cursorTimer.Tick += (s, e) =>
            {
                _cursorVisible = !_cursorVisible;
                RenderAll();
            };

            // Debounce timer: only parse after user stops typing for 600ms
            _parseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _parseTimer.Tick += (s, e) =>
            {
                _parseTimer.Stop();
                _htmlDirty = true;
                RenderAll();
            };

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Get DPI once
            try
            {
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                    _dpi = source.CompositionTarget.TransformToDevice.M11;
            }
            catch { _dpi = 1.0; }

            // Find viz script
            var docPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "doc");
            var vizPath = System.IO.Path.Combine(docPath, "calcpad-viz.umd.cjs");
            if (File.Exists(vizPath))
                _vizScriptPath = vizPath;

            _cursorTimer.Start();
            Keyboard.Focus(this);
            RenderAll();
        }

        // ─── Focus: capture all mouse clicks to take focus ───

        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Take keyboard focus when clicking anywhere in the control
            Keyboard.Focus(this);
        }

        // ─── Public API ───

        public void SetParser(ExpressionParser parser) => _parser = parser;

        public void LoadFromText(string code)
        {
            if (code == null) code = "";
            _rawLines = new List<string>(code.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None));
            if (_rawLines.Count == 0) _rawLines.Add("");
            _currentLineIndex = Math.Min(_currentLineIndex, _rawLines.Count - 1);
            _cursorPosition = 0;
            ClearSelection();
            _htmlDirty = true;
            _cachedHtmlResult = null;
            RenderAll();
        }

        public string GetText() => string.Join("\r\n", _rawLines);

        // ─── Keyboard Input ───

        private void OnTextInput(object sender, TextCompositionEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(e.Text)) return;
                char c = e.Text[0];
                if (c < 32) return; // control chars

                DeleteSelectionIfAny();
                var line = _rawLines[_currentLineIndex];
                _rawLines[_currentLineIndex] = line.Insert(_cursorPosition, e.Text);
                _cursorPosition += e.Text.Length;

                _cursorVisible = true;
                // Restart debounce: don't re-parse yet (current line shows as tokens)
                _parseTimer.Stop();
                _parseTimer.Start();
                // Keep cached HTML for other lines — only current line shows tokens
                RenderAll();
                NotifyTextChanged();
                e.Handled = true;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Input error: {ex.Message}";
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                bool handled = true;

                switch (e.Key)
                {
                    case Key.Enter:
                        DeleteSelectionIfAny();
                        InsertNewLine();
                        break;
                    case Key.Back:
                        if (HasSelection) DeleteSelectionIfAny();
                        else DeleteBackward();
                        break;
                    case Key.Delete:
                        if (HasSelection) DeleteSelectionIfAny();
                        else DeleteForward();
                        break;
                    case Key.Left:
                        if (!shift) ClearSelection();
                        MoveCursorLeft(shift);
                        break;
                    case Key.Right:
                        if (!shift) ClearSelection();
                        MoveCursorRight(shift);
                        break;
                    case Key.Up:
                        if (!shift) ClearSelection();
                        MoveCursorUp(shift);
                        break;
                    case Key.Down:
                        if (!shift) ClearSelection();
                        MoveCursorDown(shift);
                        break;
                    case Key.Home:
                        if (!shift) ClearSelection();
                        _cursorPosition = 0;
                        if (shift) UpdateSelection();
                        break;
                    case Key.End:
                        if (!shift) ClearSelection();
                        _cursorPosition = _rawLines[_currentLineIndex].Length;
                        if (shift) UpdateSelection();
                        break;
                    case Key.A:
                        if (ctrl) SelectAll(); else handled = false;
                        break;
                    case Key.C:
                        if (ctrl) CopySelection(); else handled = false;
                        break;
                    case Key.V:
                        if (ctrl) PasteClipboard(); else handled = false;
                        break;
                    case Key.X:
                        if (ctrl) { CopySelection(); DeleteSelectionIfAny(); } else handled = false;
                        break;
                    case Key.Tab:
                        DeleteSelectionIfAny();
                        _rawLines[_currentLineIndex] = _rawLines[_currentLineIndex].Insert(_cursorPosition, "    ");
                        _cursorPosition += 4;
                        break;
                    default:
                        handled = false;
                        break;
                }

                if (handled)
                {
                    _cursorVisible = true;
                    e.Handled = true;

                    // On Enter: force immediate parse (line is complete)
                    if (e.Key == Key.Enter)
                    {
                        _parseTimer.Stop();
                        _htmlDirty = true;
                        _cachedHtmlResult = null;
                    }

                    RenderAll();
                    EnsureCursorVisible();

                    // Notify for data-modifying keys
                    if (e.Key == Key.Enter || e.Key == Key.Back || e.Key == Key.Delete ||
                        e.Key == Key.Tab || (ctrl && (e.Key == Key.V || e.Key == Key.X)))
                        NotifyTextChanged();
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Key error: {ex.Message}";
            }
        }

        // ─── Line Operations ───

        private void InsertNewLine()
        {
            var line = _rawLines[_currentLineIndex];
            _rawLines[_currentLineIndex] = line.Substring(0, _cursorPosition);
            _rawLines.Insert(_currentLineIndex + 1, line.Substring(_cursorPosition));
            _currentLineIndex++;
            _cursorPosition = 0;
        }

        private void DeleteBackward()
        {
            if (_cursorPosition > 0)
            {
                _rawLines[_currentLineIndex] = _rawLines[_currentLineIndex].Remove(_cursorPosition - 1, 1);
                _cursorPosition--;
            }
            else if (_currentLineIndex > 0)
            {
                _cursorPosition = _rawLines[_currentLineIndex - 1].Length;
                _rawLines[_currentLineIndex - 1] += _rawLines[_currentLineIndex];
                _rawLines.RemoveAt(_currentLineIndex);
                _currentLineIndex--;
            }
        }

        private void DeleteForward()
        {
            var line = _rawLines[_currentLineIndex];
            if (_cursorPosition < line.Length)
                _rawLines[_currentLineIndex] = line.Remove(_cursorPosition, 1);
            else if (_currentLineIndex < _rawLines.Count - 1)
            {
                _rawLines[_currentLineIndex] += _rawLines[_currentLineIndex + 1];
                _rawLines.RemoveAt(_currentLineIndex + 1);
            }
        }

        // ─── Cursor Movement ───

        private void MoveCursorLeft(bool shift)
        {
            if (shift && _selStartLine < 0) StartSelection();
            if (_cursorPosition > 0) _cursorPosition--;
            else if (_currentLineIndex > 0) { _currentLineIndex--; _cursorPosition = _rawLines[_currentLineIndex].Length; }
            if (shift) UpdateSelection();
        }

        private void MoveCursorRight(bool shift)
        {
            if (shift && _selStartLine < 0) StartSelection();
            if (_cursorPosition < _rawLines[_currentLineIndex].Length) _cursorPosition++;
            else if (_currentLineIndex < _rawLines.Count - 1) { _currentLineIndex++; _cursorPosition = 0; }
            if (shift) UpdateSelection();
        }

        private void MoveCursorUp(bool shift)
        {
            if (shift && _selStartLine < 0) StartSelection();
            if (_currentLineIndex > 0)
            {
                _currentLineIndex--;
                _cursorPosition = Math.Min(_cursorPosition, _rawLines[_currentLineIndex].Length);
            }
            if (shift) UpdateSelection();
        }

        private void MoveCursorDown(bool shift)
        {
            if (shift && _selStartLine < 0) StartSelection();
            if (_currentLineIndex < _rawLines.Count - 1)
            {
                _currentLineIndex++;
                _cursorPosition = Math.Min(_cursorPosition, _rawLines[_currentLineIndex].Length);
            }
            if (shift) UpdateSelection();
        }

        // ─── Selection ───
        private void StartSelection() { _selStartLine = _currentLineIndex; _selStartPos = _cursorPosition; _selEndLine = _currentLineIndex; _selEndPos = _cursorPosition; }
        private void UpdateSelection() { _selEndLine = _currentLineIndex; _selEndPos = _cursorPosition; }
        private void ClearSelection() { _selStartLine = _selStartPos = _selEndLine = _selEndPos = -1; }

        private void SelectAll()
        {
            _selStartLine = 0; _selStartPos = 0;
            _selEndLine = _rawLines.Count - 1; _selEndPos = _rawLines.Last().Length;
            _currentLineIndex = _selEndLine; _cursorPosition = _selEndPos;
        }

        private void CopySelection()
        {
            if (!HasSelection) return;
            var (sl, sp, el, ep) = Norm();
            var sb = new StringBuilder();
            for (int i = sl; i <= el; i++)
            {
                int s = (i == sl) ? sp : 0;
                int e = (i == el) ? ep : _rawLines[i].Length;
                sb.Append(_rawLines[i].Substring(s, e - s));
                if (i < el) sb.AppendLine();
            }
            try { Clipboard.SetText(sb.ToString()); } catch { }
        }

        private void PasteClipboard()
        {
            try
            {
                if (!Clipboard.ContainsText()) return;
                DeleteSelectionIfAny();
                var pasteLines = Clipboard.GetText().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                if (pasteLines.Length == 1)
                {
                    _rawLines[_currentLineIndex] = _rawLines[_currentLineIndex].Insert(_cursorPosition, pasteLines[0]);
                    _cursorPosition += pasteLines[0].Length;
                }
                else
                {
                    var line = _rawLines[_currentLineIndex];
                    var after = line.Substring(_cursorPosition);
                    _rawLines[_currentLineIndex] = line.Substring(0, _cursorPosition) + pasteLines[0];
                    for (int i = 1; i < pasteLines.Length; i++)
                        _rawLines.Insert(_currentLineIndex + i, pasteLines[i]);
                    _rawLines[_currentLineIndex + pasteLines.Length - 1] += after;
                    _currentLineIndex += pasteLines.Length - 1;
                    _cursorPosition = pasteLines.Last().Length;
                }
            }
            catch { }
        }

        private void DeleteSelectionIfAny()
        {
            if (!HasSelection) return;
            var (sl, sp, el, ep) = Norm();
            if (sl == el)
                _rawLines[sl] = _rawLines[sl].Remove(sp, ep - sp);
            else
            {
                _rawLines[sl] = _rawLines[sl].Substring(0, sp) + _rawLines[el].Substring(ep);
                _rawLines.RemoveRange(sl + 1, el - sl);
            }
            _currentLineIndex = sl; _cursorPosition = sp;
            ClearSelection();
        }

        private (int sl, int sp, int el, int ep) Norm()
        {
            int sl = _selStartLine, sp = _selStartPos, el = _selEndLine, ep = _selEndPos;
            if (sl > el || (sl == el && sp > ep)) { (sl, el) = (el, sl); (sp, ep) = (ep, sp); }
            return (sl, sp, el, ep);
        }

        // ─── Mouse ───

        private void EditorCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Keyboard.Focus(this);
            EditorCanvas.CaptureMouse();
            var pos = e.GetPosition(EditorCanvas);
            var (line, charPos) = HitTestPosition(pos.X, pos.Y);
            _currentLineIndex = line;
            _cursorPosition = charPos;

            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            if (shift)
            {
                if (_selStartLine < 0) StartSelection();
                UpdateSelection();
            }
            else
            {
                ClearSelection();
                _isDragging = true;
                _selStartLine = _selEndLine = line;
                _selStartPos = _selEndPos = charPos;
            }
            _cursorVisible = true;
            RenderAll();
            e.Handled = true;
        }

        private void EditorCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            EditorCanvas.ReleaseMouseCapture();
            if (_selStartLine == _selEndLine && _selStartPos == _selEndPos) ClearSelection();
        }

        private void EditorCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;
            var pos = e.GetPosition(EditorCanvas);
            var (line, charPos) = HitTestPosition(pos.X, pos.Y);
            _currentLineIndex = line; _cursorPosition = charPos;
            _selEndLine = line; _selEndPos = charPos;
            RenderAll();
        }

        private (int line, int charPos) HitTestPosition(double x, double y)
        {
            int line = 0;
            for (int i = 0; i < _lineYPositions.Count; i++)
            {
                double nextY = (i + 1 < _lineYPositions.Count) ? _lineYPositions[i + 1] : double.MaxValue;
                if (y >= _lineYPositions[i] && y < nextY) { line = i; break; }
                if (i == _lineYPositions.Count - 1) line = i;
            }
            line = Math.Clamp(line, 0, _rawLines.Count - 1);

            var text = _rawLines[line];
            if (string.IsNullOrEmpty(text)) return (line, 0);
            double textX = x - LeftMargin;
            if (textX <= 0) return (line, 0);

            for (int i = 1; i <= text.Length; i++)
            {
                var w = MeasureTextWidth(text.Substring(0, i), _fontSize);
                if (w >= textX)
                {
                    var prevW = i > 1 ? MeasureTextWidth(text.Substring(0, i - 1), _fontSize) : 0;
                    return (line, (textX - prevW < w - textX) ? i - 1 : i);
                }
            }
            return (line, text.Length);
        }

        // ─── Zoom ───

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _zoom = Math.Min(3.0, _zoom + 0.1);
            ApplyZoom();
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _zoom = Math.Max(0.5, _zoom - 0.1);
            ApplyZoom();
        }

        private void ApplyZoom()
        {
            var st = EditorCanvas.LayoutTransform as ScaleTransform;
            if (st != null) { st.ScaleX = _zoom; st.ScaleY = _zoom; }
            else EditorCanvas.LayoutTransform = new ScaleTransform(_zoom, _zoom);
            ZoomLabel.Text = $"{(int)(_zoom * 100)}%";
            Keyboard.Focus(this);
            RenderAll();
        }

        // ═══════════════════════════════════════════════════════════
        // ─── RENDERING ───
        // ═══════════════════════════════════════════════════════════

        private void RenderAll()
        {
            try
            {
                // Remove old graph WebViews
                foreach (var wv in _graphWebViews)
                {
                    EditorCanvas.Children.Remove(wv);
                    wv.Dispose();
                }
                _graphWebViews.Clear();

                EditorCanvas.Children.Clear();
                _lineHeights.Clear();
                _lineYPositions.Clear();

                // ── Parse with Calcpad parser ──
                // Strategy:
                // 1. Lines ABOVE current line: use cached HTML (already calculated)
                // 2. Current editing line: show raw tokens (no parser, avoids errors)
                // 3. When Enter is pressed (_htmlDirty=true): re-parse everything
                string htmlResult = _cachedHtmlResult;
                bool parserOk = htmlResult != null;

                if (_htmlDirty && _parser != null && _rawLines.Any(l => !string.IsNullOrWhiteSpace(l)))
                {
                    try
                    {
                        _parser.Parse(string.Join("\r\n", _rawLines));
                        htmlResult = _parser.HtmlResult;
                        _cachedHtmlResult = htmlResult;
                        parserOk = true;
                        _htmlDirty = false;

                        // Cache per-line HTML
                        _cachedLineHtmls = SplitHtmlToLines(htmlResult);
                        _lastParsedLines = new List<string>(_rawLines);

                        // Debug: write HTML log to Desktop
                        try
                        {
                            var logSb = new StringBuilder();
                            logSb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><style>pre{background:#f5f5f5;padding:5px;font-size:11px;} .line{border-bottom:1px solid #ddd;padding:3px;}</style></head><body>");
                            logSb.AppendLine($"<h3>MathCanvas Log — {_rawLines.Count} lines, {_cachedLineHtmls.Count} html parts</h3>");
                            logSb.AppendLine("<table border='1' cellpadding='4' style='font-size:12px;border-collapse:collapse'>");
                            logSb.AppendLine("<tr><th>#</th><th>Raw Line</th><th>HTML (encoded)</th><th>Rendered</th></tr>");
                            for (int li = 0; li < _rawLines.Count; li++)
                            {
                                var rawL = System.Net.WebUtility.HtmlEncode(_rawLines[li]);
                                var htmlL = li < _cachedLineHtmls.Count && _cachedLineHtmls[li] != null
                                    ? _cachedLineHtmls[li] : "(null)";
                                var htmlEnc = System.Net.WebUtility.HtmlEncode(htmlL);
                                logSb.AppendLine($"<tr><td>{li + 1}</td><td><code>{rawL}</code></td><td><pre>{htmlEnc}</pre></td><td>{(htmlL != "(null)" ? htmlL : "")}</td></tr>");
                            }
                            logSb.AppendLine("</table></body></html>");
                            var logPath = System.IO.Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                                "mathcanvas_log.html");
                            System.IO.File.WriteAllText(logPath, logSb.ToString());
                        }
                        catch { }
                    }
                    catch
                    {
                        // Parser error — keep old cache for lines that were already OK
                    }
                }

                // Use cached per-line HTML
                var htmlLines = _cachedLineHtmls.Count > 0 ? _cachedLineHtmls : SplitHtmlToLines(htmlResult);

                double currentY = TopMargin;
                double maxWidth = 400;

                for (int i = 0; i < _rawLines.Count; i++)
                {
                    _lineYPositions.Add(currentY);
                    var rawLine = _rawLines[i];
                    // Current editing line: always use simple tokens (no parser HTML)
                    // Other lines: use cached HTML from parser
                    string lineHtml = null;
                    if (i != _currentLineIndex && i < htmlLines.Count)
                        lineHtml = htmlLines[i];

                    // ── Determine line height ──
                    double lineHeight = MinLineHeight;

                    // For title lines, bigger
                    if (rawLine.StartsWith("\"")) lineHeight = Math.Max(lineHeight, _fontSize * 1.8);

                    // For lines with fractions (contains /), make taller
                    if (!string.IsNullOrEmpty(lineHtml) && lineHtml.Contains("dvc"))
                        lineHeight = Math.Max(lineHeight, _fontSize * 2.8);

                    // For lines with vectors/matrices
                    if (!string.IsNullOrEmpty(lineHtml) && lineHtml.Contains("matrix"))
                    {
                        var rowCount = Regex.Matches(lineHtml, @"class=""tr""").Count;
                        lineHeight = Math.Max(lineHeight, _fontSize * 1.2 * Math.Max(rowCount, 1) + 10);
                    }

                    _lineHeights.Add(lineHeight);

                    // ── Current line highlight ──
                    if (i == _currentLineIndex)
                    {
                        var hl = new Rectangle
                        {
                            Width = 2000, Height = lineHeight + LineSpacing,
                            Fill = new SolidColorBrush(Color.FromArgb(20, 0, 102, 221))
                        };
                        Canvas.SetLeft(hl, 0); Canvas.SetTop(hl, currentY - 2);
                        EditorCanvas.Children.Add(hl);
                    }

                    // ── Selection highlight ──
                    if (HasSelection)
                    {
                        var (sl, sp, el2, ep) = Norm();
                        if (i >= sl && i <= el2)
                        {
                            double sx = LeftMargin + (i == sl ? MeasureTextWidth(rawLine.Substring(0, Math.Min(sp, rawLine.Length)), _fontSize) : 0);
                            double ex = LeftMargin + (i == el2 ? MeasureTextWidth(rawLine.Substring(0, Math.Min(ep, rawLine.Length)), _fontSize) : MeasureTextWidth(rawLine, _fontSize) + 8);
                            EditorCanvas.Children.Add(MakeRect(sx, currentY, Math.Max(ex - sx, 4), lineHeight, Color.FromArgb(80, 0, 102, 221)));
                        }
                    }

                    // ── Line number ──
                    var lnTb = new TextBlock
                    {
                        Text = (i + 1).ToString(),
                        FontFamily = MathStyles.UIFont, FontSize = 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                        TextAlignment = TextAlignment.Right, Width = LeftMargin - 10
                    };
                    Canvas.SetLeft(lnTb, 0); Canvas.SetTop(lnTb, currentY + (lineHeight - 14) / 2);
                    EditorCanvas.Children.Add(lnTb);

                    // ── Separator line ──
                    EditorCanvas.Children.Add(MakeLine(LeftMargin - 5, currentY, LeftMargin - 5, currentY + lineHeight,
                        Color.FromRgb(0xE0, 0xE0, 0xE0)));

                    // ═══════════════════════════════════════
                    // ── RENDER LINE CONTENT ──
                    // Priority: 1) Current editing line → tokenized
                    //           2) Parser HTML available → use it (no duplicates)
                    //           3) Fallback → type-specific rendering
                    // ═══════════════════════════════════════
                    double xPos = LeftMargin;

                    // Keywords (#sym, #end sym, #python, etc.) → HIDE completely
                    if (IsSymbolicKeyword(rawLine))
                    {
                        // Don't render — hidden like in Output
                        lineHeight = 0; // Collapse the line
                        _lineHeights[_lineHeights.Count - 1] = 0;
                    }
                    // Current editing line → always tokenized (no parser errors)
                    else if (i == _currentLineIndex)
                    {
                        if (string.IsNullOrWhiteSpace(rawLine))
                        { /* empty — cursor only */ }
                        else if (rawLine.StartsWith("\""))
                        {
                            var tb = new TextBlock { Text = rawLine.Substring(1),
                                FontFamily = new FontFamily("Arial Nova, Helvetica, sans-serif"),
                                FontSize = _fontSize * 1.4, FontWeight = FontWeights.Bold, Foreground = Brushes.Black };
                            Canvas.SetLeft(tb, xPos); Canvas.SetTop(tb, currentY + 2);
                            EditorCanvas.Children.Add(tb);
                        }
                        else if (rawLine.StartsWith("'"))
                        {
                            var tb = new TextBlock { Text = rawLine.Substring(1),
                                FontFamily = MathStyles.UIFont, FontSize = _fontSize, Foreground = Brushes.DarkGray };
                            Canvas.SetLeft(tb, xPos); Canvas.SetTop(tb, currentY + (lineHeight - _fontSize) / 2);
                            EditorCanvas.Children.Add(tb);
                        }
                        else
                        {
                            xPos = RenderTokenizedLine(rawLine, xPos, currentY, lineHeight);
                        }
                    }
                    // Graph/Visualization ($Draw, $Chart, $Fem, etc.)
                    else if (!string.IsNullOrEmpty(lineHtml) && lineHtml.Contains("CalcpadViz."))
                    {
                        // Extract width/height from options
                        var wMatch = Regex.Match(lineHtml, @"width:(\d+)");
                        var hMatch = Regex.Match(lineHtml, @"height:(\d+)");
                        double gWidth = wMatch.Success ? double.Parse(wMatch.Groups[1].Value) : 500;
                        double gHeight = hMatch.Success ? double.Parse(hMatch.Groups[1].Value) : 350;

                        // Scale down for MathCanvas
                        gWidth = Math.Min(gWidth, 500);
                        gHeight = Math.Min(gHeight, 350);
                        lineHeight = gHeight + 10;
                        _lineHeights[_lineHeights.Count - 1] = lineHeight;

                        // Create embedded WebView2
                        try
                        {
                            var graphWv = new WebView2
                            {
                                Width = gWidth,
                                Height = gHeight,
                                DefaultBackgroundColor = System.Drawing.Color.White
                            };
                            Canvas.SetLeft(graphWv, xPos);
                            Canvas.SetTop(graphWv, currentY);
                            EditorCanvas.Children.Add(graphWv);
                            _graphWebViews.Add(graphWv);

                            // Build graph HTML
                            var graphHtml = BuildGraphHtml(lineHtml, gWidth, gHeight);

                            // Init WebView2 async and load HTML
                            InitGraphWebView(graphWv, graphHtml);
                        }
                        catch
                        {
                            // Fallback: show as code text
                            var tb = MakeEquationTextBlock("[$Draw graphic]", _fontSize, false);
                            tb.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x88, 0x66));
                            Canvas.SetLeft(tb, xPos); Canvas.SetTop(tb, currentY + 4);
                            EditorCanvas.Children.Add(tb);
                        }
                    }
                    // Error in parser output
                    else if (!string.IsNullOrEmpty(lineHtml) && lineHtml.Contains("class=\"err\""))
                    {
                        var tb1 = MakeEquationTextBlock(rawLine, _fontSize, false);
                        tb1.Foreground = Brushes.Red;
                        Canvas.SetLeft(tb1, xPos); Canvas.SetTop(tb1, currentY + 2);
                        EditorCanvas.Children.Add(tb1);
                        xPos += MeasureTextWidth(rawLine, _fontSize) + 10;
                    }
                    // Parser HTML available → use it (covers equations, titles, comments, sym results)
                    else if (!string.IsNullOrEmpty(lineHtml))
                    {
                        try
                        {
                            xPos = RenderHtmlLine(lineHtml, xPos, currentY, lineHeight);
                        }
                        catch
                        {
                            // HTML render failed — fallback to tokenized
                            xPos = RenderTokenizedLine(rawLine, xPos, currentY, lineHeight);
                        }
                    }
                    // No HTML — fallback rendering by type
                    else if (string.IsNullOrWhiteSpace(rawLine))
                    {
                        // Empty line
                    }
                    else if (rawLine.StartsWith("\""))
                    {
                        var tb = new TextBlock { Text = rawLine.Substring(1),
                            FontFamily = new FontFamily("Arial Nova, Helvetica, sans-serif"),
                            FontSize = _fontSize * 1.4, FontWeight = FontWeights.Bold, Foreground = Brushes.Black };
                        Canvas.SetLeft(tb, xPos); Canvas.SetTop(tb, currentY + 2);
                        EditorCanvas.Children.Add(tb);
                    }
                    else if (rawLine.StartsWith("'"))
                    {
                        var tb = new TextBlock { Text = rawLine.Substring(1),
                            FontFamily = MathStyles.UIFont, FontSize = _fontSize, Foreground = Brushes.DarkGray };
                        Canvas.SetLeft(tb, xPos); Canvas.SetTop(tb, currentY + (lineHeight - _fontSize) / 2);
                        EditorCanvas.Children.Add(tb);
                    }
                    else if (rawLine.StartsWith("$") || rawLine.StartsWith("#"))
                    {
                        var tb = new TextBlock { Text = rawLine, FontFamily = new FontFamily("Consolas"),
                            FontSize = _fontSize * 0.85, Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)) };
                        Canvas.SetLeft(tb, xPos); Canvas.SetTop(tb, currentY + (lineHeight - _fontSize * 0.85) / 2);
                        EditorCanvas.Children.Add(tb);
                    }
                    else
                    {
                        xPos = RenderTokenizedLine(rawLine, xPos, currentY, lineHeight);
                    }

                    if (xPos > maxWidth) maxWidth = xPos;

                    // ── Cursor ──
                    if (i == _currentLineIndex && _cursorVisible)
                    {
                        var cx = LeftMargin + MeasureTextWidth(
                            rawLine.Substring(0, Math.Min(_cursorPosition, rawLine.Length)), _fontSize);
                        EditorCanvas.Children.Add(MakeLine(cx, currentY + 3, cx, currentY + lineHeight - 3,
                            Color.FromRgb(0x00, 0x66, 0xDD), 1.5));
                    }

                    currentY += lineHeight + LineSpacing;
                }

                // Canvas size — tight fit, no extra padding
                EditorCanvas.Width = Math.Max(maxWidth + 20, MainScroll.ViewportWidth / _zoom);
                EditorCanvas.Height = Math.Max(currentY + 30, MainScroll.ViewportHeight / _zoom);

                // Status
                LineInfo.Text = $"Ln {_currentLineIndex + 1}/{_rawLines.Count}  Col {_cursorPosition + 1}";
                ParseInfo.Text = parserOk ? "Parser OK" : (_parser == null ? "Sin parser" : "Sin evaluacion");
                StatusText.Text = $"{_rawLines.Count} lineas | {(parserOk ? "Evaluado ✓" : "Edicion")}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Render error: {ex.Message}";
            }
        }

        // ─── Render math line with HTML from parser ───

        private double RenderHtmlLine(string html, double x, double y, double lineHeight)
        {
            // Strip outer p/div tags
            html = Regex.Replace(html, @"^<(?:p|div)[^>]*>", "");
            html = Regex.Replace(html, @"</(?:p|div)>\s*$", "");

            // Process HTML tokens: <var>, <i>, <sup>, <sub>, <span class="dvl"/"dvc">, plain text
            var parts = ExtractHtmlParts(html);
            double xPos = x;
            double midY = y + (lineHeight - _fontSize) / 2;

            foreach (var part in parts)
            {
                if (part.IsFraction)
                {
                    // Render fraction
                    var numW = MeasureTextWidth(part.Numerator, _fontSize * 0.85);
                    var denW = MeasureTextWidth(part.Denominator, _fontSize * 0.85);
                    var fracW = Math.Max(numW, denW) + 8;
                    var fracH = _fontSize * 1.8;
                    var fracMidY = y + (lineHeight - fracH) / 2;

                    // Numerator
                    var numTb = MakeEquationTextBlock(part.Numerator, _fontSize * 0.85, part.IsVariable);
                    Canvas.SetLeft(numTb, xPos + (fracW - numW) / 2);
                    Canvas.SetTop(numTb, fracMidY);
                    EditorCanvas.Children.Add(numTb);

                    // Fraction line
                    EditorCanvas.Children.Add(MakeLine(xPos + 2, fracMidY + _fontSize * 0.85 + 2,
                        xPos + fracW - 2, fracMidY + _fontSize * 0.85 + 2, Colors.Black, 1));

                    // Denominator
                    var denTb = MakeEquationTextBlock(part.Denominator, _fontSize * 0.85, part.IsVariable);
                    Canvas.SetLeft(denTb, xPos + (fracW - denW) / 2);
                    Canvas.SetTop(denTb, fracMidY + _fontSize * 0.85 + 5);
                    EditorCanvas.Children.Add(denTb);

                    xPos += fracW + 3;
                }
                else if (part.IsSuperscript)
                {
                    var tb = MakeEquationTextBlock(part.Text, _fontSize * 0.75, false);
                    Canvas.SetLeft(tb, xPos);
                    Canvas.SetTop(tb, midY - _fontSize * 0.35);
                    EditorCanvas.Children.Add(tb);
                    xPos += MeasureTextWidth(part.Text, _fontSize * 0.75) + 1;
                }
                else if (part.IsSubscript)
                {
                    var tb = new TextBlock
                    {
                        Text = part.Text,
                        FontFamily = MathStyles.SubscriptFont,
                        FontSize = _fontSize * 0.80,
                        Foreground = part.IsVariable ? MathStyles.VariableColor : MathStyles.NumberColor
                    };
                    Canvas.SetLeft(tb, xPos);
                    Canvas.SetTop(tb, midY + _fontSize * 0.3);
                    EditorCanvas.Children.Add(tb);
                    xPos += MeasureTextWidth(part.Text, _fontSize * 0.80) + 1;
                }
                else if (part.IsRoot)
                {
                    // Render √ with content (possibly a fraction underneath)
                    double rootFontSize = _fontSize * 0.85;
                    double contentW, contentH;

                    if (!string.IsNullOrEmpty(part.RootNumerator) || !string.IsNullOrEmpty(part.RootDenominator))
                    {
                        // Root contains a fraction
                        var rNumW = MeasureTextWidth(part.RootNumerator, rootFontSize);
                        var rDenW = MeasureTextWidth(part.RootDenominator, rootFontSize);
                        contentW = Math.Max(rNumW, rDenW) + 6;
                        contentH = rootFontSize * 2.2;
                        var rootTop = y + (lineHeight - contentH - 6) / 2;

                        // √ symbol using path
                        double sqrtX = xPos;
                        double sqrtTop = rootTop;
                        double sqrtH = contentH + 4;

                        // Draw √ symbol
                        EditorCanvas.Children.Add(MakeLine(sqrtX, sqrtTop + sqrtH * 0.6, sqrtX + 3, sqrtTop + sqrtH * 0.7, Colors.Black, 1.2));
                        EditorCanvas.Children.Add(MakeLine(sqrtX + 3, sqrtTop + sqrtH * 0.7, sqrtX + 7, sqrtTop + sqrtH - 1, Colors.Black, 1.2));
                        EditorCanvas.Children.Add(MakeLine(sqrtX + 7, sqrtTop + sqrtH - 1, sqrtX + 12, sqrtTop + 1, Colors.Black, 1.2));
                        // Top line
                        EditorCanvas.Children.Add(MakeLine(sqrtX + 12, sqrtTop + 1, sqrtX + 14 + contentW, sqrtTop + 1, Colors.Black, 1.2));

                        xPos = sqrtX + 14;

                        // Numerator
                        var numTb2 = MakeEquationTextBlock(part.RootNumerator, rootFontSize, false);
                        Canvas.SetLeft(numTb2, xPos + (contentW - rNumW) / 2);
                        Canvas.SetTop(numTb2, rootTop + 3);
                        EditorCanvas.Children.Add(numTb2);

                        // Fraction line
                        EditorCanvas.Children.Add(MakeLine(xPos + 1, rootTop + rootFontSize + 4, xPos + contentW - 1, rootTop + rootFontSize + 4, Colors.Black, 1));

                        // Denominator
                        var denTb2 = MakeEquationTextBlock(part.RootDenominator, rootFontSize, false);
                        Canvas.SetLeft(denTb2, xPos + (contentW - rDenW) / 2);
                        Canvas.SetTop(denTb2, rootTop + rootFontSize + 6);
                        EditorCanvas.Children.Add(denTb2);

                        xPos += contentW + 3;
                    }
                    else
                    {
                        // Simple root: √(content)
                        var content = part.RootContent;
                        contentW = MeasureTextWidth(content, rootFontSize) + 4;
                        contentH = rootFontSize * 1.3;
                        var rootTop = y + (lineHeight - contentH - 4) / 2;

                        // √ symbol
                        double sqrtX = xPos;
                        EditorCanvas.Children.Add(MakeLine(sqrtX, rootTop + contentH * 0.5, sqrtX + 3, rootTop + contentH * 0.6, Colors.Black, 1.2));
                        EditorCanvas.Children.Add(MakeLine(sqrtX + 3, rootTop + contentH * 0.6, sqrtX + 7, rootTop + contentH + 2, Colors.Black, 1.2));
                        EditorCanvas.Children.Add(MakeLine(sqrtX + 7, rootTop + contentH + 2, sqrtX + 12, rootTop, Colors.Black, 1.2));
                        EditorCanvas.Children.Add(MakeLine(sqrtX + 12, rootTop, sqrtX + 14 + contentW, rootTop, Colors.Black, 1.2));

                        var cTb = MakeEquationTextBlock(content, rootFontSize, false);
                        Canvas.SetLeft(cTb, sqrtX + 14);
                        Canvas.SetTop(cTb, rootTop + 2);
                        EditorCanvas.Children.Add(cTb);

                        xPos = sqrtX + 16 + contentW;
                    }
                }
                else if (part.IsMatrix && part.MatrixData != null && part.MatrixData.Count > 0)
                {
                    // Render matrix/vector visually with brackets
                    int rows = part.MatrixData.Count;
                    int cols = part.MatrixData.Max(r => r.Count);
                    double cellFontSize = _fontSize * 0.9;
                    double cellH = cellFontSize * 1.4;
                    double matH = rows * cellH;
                    double matTop = y + (lineHeight - matH) / 2;

                    // Measure column widths
                    var colWidths = new double[cols];
                    for (int r = 0; r < rows; r++)
                        for (int c = 0; c < part.MatrixData[r].Count; c++)
                        {
                            var w = MeasureTextWidth(part.MatrixData[r][c], cellFontSize);
                            if (w > colWidths[c]) colWidths[c] = w;
                        }

                    double totalW = colWidths.Sum() + (cols - 1) * 12 + 8; // padding

                    // Left bracket [
                    var lBracket = new TextBlock
                    {
                        Text = rows > 1 ? "⎡\n" + string.Join("\n", Enumerable.Repeat("⎢", rows - 2)) + "\n⎣" : "[",
                        FontFamily = MathStyles.SymbolFont, FontSize = cellFontSize,
                        Foreground = Brushes.Black
                    };
                    // Simple bracket rendering
                    EditorCanvas.Children.Add(MakeLine(xPos + 2, matTop, xPos + 2, matTop + matH, Colors.Black, 1.5));
                    EditorCanvas.Children.Add(MakeLine(xPos + 2, matTop, xPos + 6, matTop, Colors.Black, 1.5));
                    EditorCanvas.Children.Add(MakeLine(xPos + 2, matTop + matH, xPos + 6, matTop + matH, Colors.Black, 1.5));
                    xPos += 8;

                    // Cell values
                    double cellStartX = xPos;
                    for (int r = 0; r < rows; r++)
                    {
                        double cx = cellStartX;
                        for (int c = 0; c < part.MatrixData[r].Count; c++)
                        {
                            var val = part.MatrixData[r][c];
                            var valW = MeasureTextWidth(val, cellFontSize);
                            var tb = MakeEquationTextBlock(val, cellFontSize, false);
                            // Right-align within column
                            Canvas.SetLeft(tb, cx + colWidths[c] - valW);
                            Canvas.SetTop(tb, matTop + r * cellH + (cellH - cellFontSize) / 2);
                            EditorCanvas.Children.Add(tb);
                            cx += colWidths[c] + 12;
                        }
                    }
                    xPos = cellStartX + totalW;

                    // Right bracket ]
                    EditorCanvas.Children.Add(MakeLine(xPos, matTop, xPos, matTop + matH, Colors.Black, 1.5));
                    EditorCanvas.Children.Add(MakeLine(xPos - 4, matTop, xPos, matTop, Colors.Black, 1.5));
                    EditorCanvas.Children.Add(MakeLine(xPos - 4, matTop + matH, xPos, matTop + matH, Colors.Black, 1.5));
                    xPos += 4;
                }
                else
                {
                    // Normal text: variable, function, number, operator
                    if (!string.IsNullOrEmpty(part.Text))
                    {
                        var tb = MakeEquationTextBlock(part.Text, _fontSize, part.IsVariable, part.IsFunction);
                        Canvas.SetLeft(tb, xPos);
                        Canvas.SetTop(tb, midY);
                        EditorCanvas.Children.Add(tb);
                        xPos += MeasureTextWidth(part.Text, _fontSize) + 1;
                    }
                }
            }

            return xPos;
        }

        // ─── Render line with token highlighting (fallback) ───

        private double RenderTokenizedLine(string rawLine, double x, double y, double lineHeight)
        {
            double xPos = x;
            double midY = y + (lineHeight - _fontSize) / 2;

            // Tokenize for syntax highlighting
            int i = 0;
            while (i < rawLine.Length)
            {
                char c = rawLine[i];
                string token;
                Brush fg;
                FontStyle fs = FontStyles.Normal;

                if (char.IsDigit(c) || (c == '.' && i + 1 < rawLine.Length && char.IsDigit(rawLine[i + 1])))
                {
                    // Number
                    int start = i;
                    while (i < rawLine.Length && (char.IsDigit(rawLine[i]) || rawLine[i] == '.')) i++;
                    token = rawLine.Substring(start, i - start);
                    fg = MathStyles.NumberColor;
                }
                else if (char.IsLetter(c) || c == '_')
                {
                    // Word (variable or function)
                    int start = i;
                    while (i < rawLine.Length && (char.IsLetterOrDigit(rawLine[i]) || rawLine[i] == '_')) i++;
                    token = rawLine.Substring(start, i - start);
                    if (MathStyles.IsKnownFunction(token))
                    {
                        fg = MathStyles.FunctionColor;
                    }
                    else
                    {
                        fg = MathStyles.VariableColor;
                        fs = FontStyles.Italic;
                    }
                }
                else if (c == '*')
                {
                    token = " · ";
                    fg = MathStyles.OperatorColor;
                    i++;
                }
                else if (c == '=')
                {
                    token = " = ";
                    fg = MathStyles.OperatorColor;
                    i++;
                }
                else if (c == ' ')
                {
                    i++;
                    continue;
                }
                else
                {
                    token = c.ToString();
                    fg = MathStyles.OperatorColor;
                    i++;
                }

                var tb = new TextBlock
                {
                    Text = token,
                    FontFamily = MathStyles.EquationFont,
                    FontStyle = fs,
                    FontSize = _fontSize,
                    Foreground = fg
                };
                Canvas.SetLeft(tb, xPos);
                Canvas.SetTop(tb, midY);
                EditorCanvas.Children.Add(tb);
                xPos += MeasureTextWidth(token, _fontSize) + 0.5;
            }
            return xPos;
        }

        // ─── HTML part extraction ───

        private class HtmlPart
        {
            public string Text = "";
            public string Numerator = "", Denominator = "";
            public bool IsVariable, IsFunction, IsSuperscript, IsSubscript, IsFraction;
            public bool IsMatrix;
            public bool IsRoot; // √ symbol
            public string RootContent = ""; // content under root (may contain fraction text)
            public string RootNumerator = "", RootDenominator = ""; // if root contains fraction
            public List<List<string>> MatrixData; // rows x cols
        }

        private List<HtmlPart> ExtractHtmlParts(string html)
        {
            var parts = new List<HtmlPart>();
            if (string.IsNullOrEmpty(html)) return parts;

            // Remove span class="eq" wrapper
            html = Regex.Replace(html, @"<span\s+class=""eq"">(.*)</span>", "$1", RegexOptions.Singleline);

            int pos = 0;
            while (pos < html.Length)
            {
                int tagStart = html.IndexOf('<', pos);
                if (tagStart < 0)
                {
                    // Rest is plain text
                    var rest = WebUtility.HtmlDecode(html.Substring(pos)).Trim();
                    if (!string.IsNullOrEmpty(rest))
                        AddTextParts(parts, rest);
                    break;
                }

                // Text before tag
                if (tagStart > pos)
                {
                    var text = WebUtility.HtmlDecode(html.Substring(pos, tagStart - pos));
                    if (!string.IsNullOrWhiteSpace(text))
                        AddTextParts(parts, text);
                }

                var tagEnd = html.IndexOf('>', tagStart);
                if (tagEnd < 0) break;

                var tag = html.Substring(tagStart, tagEnd - tagStart + 1);
                var tagName = Regex.Match(tag, @"<(\w+)").Groups[1].Value.ToLower();
                var cssClass = Regex.Match(tag, @"class=""([^""]+)""").Groups[1].Value;

                // Self-closing
                if (tag.EndsWith("/>") || tagName == "br")
                {
                    if (tagName == "br") parts.Add(new HtmlPart { Text = " " });
                    pos = tagEnd + 1;
                    continue;
                }

                // Find closing tag
                var closeTag = $"</{tagName}>";
                var closeIdx = FindMatchingClose(html, tagEnd + 1, tagName);
                if (closeIdx < 0) { pos = tagEnd + 1; continue; }
                var inner = html.Substring(tagEnd + 1, closeIdx - tagEnd - 1);
                pos = closeIdx + closeTag.Length;

                // Handle tag types
                if (tagName == "span" && cssClass == "dvc")
                {
                    // Fraction: <span class="dvc">NUMERATOR<span class="dvl"></span>DENOMINATOR</span>
                    // dvl is the horizontal line; numerator is BEFORE dvl, denominator AFTER
                    var dvlIdx = inner.IndexOf("<span class=\"dvl\">");
                    if (dvlIdx >= 0)
                    {
                        var numHtml = inner.Substring(0, dvlIdx);
                        // Find end of dvl span
                        var dvlCloseIdx = inner.IndexOf("</span>", dvlIdx);
                        var denHtml = dvlCloseIdx >= 0 ? inner.Substring(dvlCloseIdx + 7) : "";
                        parts.Add(new HtmlPart
                        {
                            IsFraction = true,
                            Numerator = StripHtml(numHtml),
                            Denominator = StripHtml(denHtml)
                        });
                    }
                    else
                    {
                        parts.Add(new HtmlPart { Text = StripHtml(inner) });
                    }
                }
                else if (tagName == "span" && cssClass == "dvl")
                {
                    // dvl alone = fraction line (should be handled inside dvc)
                    // skip
                }
                else if (tagName == "span" && cssClass == "dvr")
                {
                    // Integral/Sum container: <span class="dvr"><small>upper</small><span class="nary">∫</span><small>lower</small></span>
                    // Extract the nary symbol and limits
                    var naryMatch = Regex.Match(inner, @"<span\s+class=""nary"">(.*?)</span>");
                    var narySymbol = naryMatch.Success ? StripHtml(naryMatch.Groups[1].Value) : "∫";
                    var smallMatches = Regex.Matches(inner, @"<small>(.*?)</small>");
                    string upperLimit = smallMatches.Count > 0 ? StripHtml(smallMatches[0].Groups[1].Value) : "";
                    string lowerLimit = smallMatches.Count > 1 ? StripHtml(smallMatches[1].Groups[1].Value) : "";

                    // Render: just show the symbol with limits as text for now
                    var symbolText = narySymbol;
                    if (!string.IsNullOrEmpty(upperLimit)) symbolText = upperLimit + "\n" + symbolText;
                    if (!string.IsNullOrEmpty(lowerLimit)) symbolText += "\n" + lowerLimit;

                    parts.Add(new HtmlPart { Text = narySymbol, IsFunction = true });
                }
                else if (tagName == "span" && cssClass == "nary")
                {
                    // N-ary symbol (∫, ∑, ∏) standalone
                    parts.Add(new HtmlPart { Text = StripHtml(inner), IsFunction = true });
                }
                else if (tagName == "span" && (cssClass == "o1" || cssClass == "o2" || cssClass == "o3"))
                {
                    // Root: <span class="o1"><span class="r1"></span><span class="dvc">num<span class="dvl"></span>den</span></span>
                    // Check if it contains a fraction (dvc)
                    var dvcIdx = inner.IndexOf("<span class=\"dvc\">");
                    if (dvcIdx >= 0)
                    {
                        // Extract fraction inside root
                        var dvcClose = FindMatchingClose(inner, dvcIdx + 17, "span");
                        if (dvcClose >= 0)
                        {
                            var dvcInner = inner.Substring(dvcIdx + 17, dvcClose - dvcIdx - 17);
                            var dvlIdx2 = dvcInner.IndexOf("<span class=\"dvl\">");
                            if (dvlIdx2 >= 0)
                            {
                                var rNum = StripHtml(dvcInner.Substring(0, dvlIdx2));
                                var dvlClose2 = dvcInner.IndexOf("</span>", dvlIdx2);
                                var rDen = dvlClose2 >= 0 ? StripHtml(dvcInner.Substring(dvlClose2 + 7)) : "";
                                parts.Add(new HtmlPart { IsRoot = true, RootNumerator = rNum, RootDenominator = rDen });
                            }
                            else
                            {
                                parts.Add(new HtmlPart { IsRoot = true, RootContent = StripHtml(dvcInner) });
                            }
                        }
                        else
                        {
                            parts.Add(new HtmlPart { IsRoot = true, RootContent = StripHtml(inner) });
                        }
                    }
                    else
                    {
                        // Simple root without fraction
                        parts.Add(new HtmlPart { IsRoot = true, RootContent = StripHtml(inner) });
                    }
                }
                else if (tagName == "span" && (cssClass == "r1" || cssClass == "r2" || cssClass == "r3"))
                {
                    // Root symbol indicator — skip (handled by o1/o2/o3 parent)
                }
                else if (tagName == "span" && cssClass == "matrix")
                {
                    // Vector/Matrix: <span class="matrix">
                    //   <span class="tr"><span class="td"></span><span class="td">10</span><span class="td"></span></span>
                    //   <span class="tr"><span class="td"></span><span class="td">20</span><span class="td"></span></span>
                    // Each tr = row, each td = cell (some empty for padding)
                    // Extract all text content between td tags
                    var allValues = new List<List<string>>();
                    // Split by tr tags
                    var trParts = inner.Split(new[] { "<span class=\"tr\">" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var trPart in trParts)
                    {
                        // Extract td values
                        var tdMatches = Regex.Matches(trPart, @"<span class=""td"">(.*?)</span>");
                        var rowValues = new List<string>();
                        foreach (Match td in tdMatches)
                        {
                            var val = StripHtml(td.Groups[1].Value).Trim();
                            if (!string.IsNullOrEmpty(val))
                                rowValues.Add(val);
                        }
                        if (rowValues.Count > 0)
                            allValues.Add(rowValues);
                    }

                    // Store matrix data for visual rendering
                    parts.Add(new HtmlPart { IsMatrix = true, MatrixData = allValues });
                }
                else if (tagName == "var")
                {
                    // Variable - might contain <span class="vec"> for vector arrow
                    var varText = StripHtml(inner);
                    if (inner.Contains("class=\"vec\""))
                        varText = varText.Replace("⃗", "").Trim(); // Remove combining arrow
                    parts.Add(new HtmlPart { Text = varText, IsVariable = true });
                }
                else if (tagName == "i")
                {
                    parts.Add(new HtmlPart { Text = StripHtml(inner), IsFunction = true });
                }
                else if (tagName == "sup")
                {
                    parts.Add(new HtmlPart { Text = StripHtml(inner), IsSuperscript = true });
                }
                else if (tagName == "sub")
                {
                    parts.Add(new HtmlPart { Text = StripHtml(inner), IsSubscript = true });
                }
                else if (tagName == "span" && cssClass == "eq")
                {
                    // Equation container - recurse
                    var innerParts = ExtractHtmlParts(inner);
                    parts.AddRange(innerParts);
                }
                else if (tagName == "span" || tagName == "b" || tagName == "strong")
                {
                    // Generic span/bold - extract text
                    var innerParts = ExtractHtmlParts(inner);
                    if (innerParts.Count > 0)
                        parts.AddRange(innerParts);
                    else
                    {
                        var text = StripHtml(inner);
                        if (!string.IsNullOrEmpty(text))
                            parts.Add(new HtmlPart { Text = text });
                    }
                }
                else
                {
                    // Other tags: extract text
                    var innerParts = ExtractHtmlParts(inner);
                    if (innerParts.Count > 0)
                        parts.AddRange(innerParts);
                }
            }

            return parts;
        }

        private void AddTextParts(List<HtmlPart> parts, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            // Clean up HTML space entities that weren't decoded
            text = text.Replace("&ensp;", " ").Replace("&hairsp;", "").Replace("&thinsp;", " ")
                       .Replace("&nbsp;", " ").Replace("\u2002", " ").Replace("\u200A", "");
            text = text.Trim();
            if (!string.IsNullOrEmpty(text))
                parts.Add(new HtmlPart { Text = text });
        }

        // ─── Split HTML result into per-line ───

        private List<string> SplitHtmlToLines(string html)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(html))
            {
                for (int i = 0; i < _rawLines.Count; i++) result.Add(null);
                return result;
            }

            // Split HTML on closing p/h/div tags
            var parts = Regex.Split(html, @"(?<=</(?:p|h[1-6]|div|pre)>)");
            var htmlParts = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

            // Smart mapping: some raw lines don't produce HTML output
            // (empty lines, #sym, #end sym, #python, #maxima keywords)
            // We need to map HTML parts to the raw lines that DO produce output
            int htmlIdx = 0;
            for (int i = 0; i < _rawLines.Count; i++)
            {
                if (IsOutputProducingLine(_rawLines[i]))
                {
                    // This line produces HTML output — map next HTML part to it
                    result.Add(htmlIdx < htmlParts.Count ? htmlParts[htmlIdx++] : null);
                }
                else
                {
                    result.Add(null); // No HTML for this line (empty, keyword, etc.)
                }
            }
            return result;
        }

        // ─── HTML utilities ───

        private static int FindMatchingClose(string html, int start, string tag)
        {
            int depth = 1; int pos = start;
            while (pos < html.Length && depth > 0)
            {
                int no = html.IndexOf($"<{tag}", pos, StringComparison.OrdinalIgnoreCase);
                int nc = html.IndexOf($"</{tag}>", pos, StringComparison.OrdinalIgnoreCase);
                if (nc < 0) return -1;
                if (no >= 0 && no < nc)
                {
                    var te = html.IndexOf('>', no);
                    if (te >= 0 && html[te - 1] != '/') depth++;
                    pos = no + tag.Length + 1;
                }
                else { depth--; if (depth == 0) return nc; pos = nc + tag.Length + 3; }
            }
            return -1;
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            // Convert <sup> to Unicode superscript before stripping
            html = Regex.Replace(html, @"<sup>(\d+)</sup>", m => ToSuperscript(m.Groups[1].Value));
            // Convert <sub> to Unicode subscript
            html = Regex.Replace(html, @"<sub>(\w+)</sub>", m => ToSubscript(m.Groups[1].Value));
            return WebUtility.HtmlDecode(Regex.Replace(html, @"<[^>]+>", "")).Trim();
        }

        private static string ToSuperscript(string text)
        {
            var map = new Dictionary<char, char>
            {
                {'0', '\u2070'}, {'1', '\u00B9'}, {'2', '\u00B2'}, {'3', '\u00B3'},
                {'4', '\u2074'}, {'5', '\u2075'}, {'6', '\u2076'}, {'7', '\u2077'},
                {'8', '\u2078'}, {'9', '\u2079'}, {'+', '\u207A'}, {'-', '\u207B'},
                {'n', '\u207F'}
            };
            var sb = new StringBuilder();
            foreach (var c in text)
                sb.Append(map.ContainsKey(c) ? map[c] : c);
            return sb.ToString();
        }

        private static string ToSubscript(string text)
        {
            var map = new Dictionary<char, char>
            {
                {'0', '\u2080'}, {'1', '\u2081'}, {'2', '\u2082'}, {'3', '\u2083'},
                {'4', '\u2084'}, {'5', '\u2085'}, {'6', '\u2086'}, {'7', '\u2087'},
                {'8', '\u2088'}, {'9', '\u2089'}, {'+', '\u208A'}, {'-', '\u208B'}
            };
            var sb = new StringBuilder();
            foreach (var c in text)
                sb.Append(map.ContainsKey(c) ? map[c] : c);
            return sb.ToString();
        }

        // ─── UI Helpers ───

        private TextBlock MakeEquationTextBlock(string text, double fontSize, bool isVariable, bool isFunction = false)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = MathStyles.EquationFont,
                FontStyle = isVariable ? FontStyles.Italic : FontStyles.Normal,
                FontSize = fontSize,
                Foreground = isFunction ? MathStyles.FunctionColor :
                             isVariable ? MathStyles.VariableColor : MathStyles.NumberColor
            };
        }

        private Rectangle MakeRect(double x, double y, double w, double h, Color fill)
        {
            var r = new Rectangle { Width = w, Height = h, Fill = new SolidColorBrush(fill) };
            Canvas.SetLeft(r, x); Canvas.SetTop(r, y);
            return r;
        }

        private Line MakeLine(double x1, double y1, double x2, double y2, Color stroke, double thickness = 1)
        {
            return new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = new SolidColorBrush(stroke), StrokeThickness = thickness };
        }

        // ─── Text Measurement ───

        private double MeasureTextWidth(string text, double fontSize)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            try
            {
                var ft = new FormattedText(text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    MathStyles.EquationTypeface,
                    fontSize, Brushes.Black, _dpi);
                return ft.Width;
            }
            catch { return text.Length * fontSize * 0.55; }
        }

        // ─── Scroll ───

        private void EnsureCursorVisible()
        {
            if (_currentLineIndex >= _lineYPositions.Count) return;
            var cy = _lineYPositions[_currentLineIndex];
            var ch = _currentLineIndex < _lineHeights.Count ? _lineHeights[_currentLineIndex] : MinLineHeight;

            if (cy * _zoom < MainScroll.VerticalOffset)
                MainScroll.ScrollToVerticalOffset(cy * _zoom - 10);
            else if ((cy + ch) * _zoom > MainScroll.VerticalOffset + MainScroll.ViewportHeight)
                MainScroll.ScrollToVerticalOffset((cy + ch) * _zoom - MainScroll.ViewportHeight + 10);
        }

        private void NotifyTextChanged()
        {
            TextChanged?.Invoke(GetText());
        }

        // ─── Graph WebView2 ───

        private string BuildGraphHtml(string lineHtml, double width, double height)
        {
            // Build standalone HTML page for the graph
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
            sb.AppendLine("<style>body{margin:0;padding:0;overflow:hidden;}</style>");
            sb.AppendLine("</head><body>");

            // Include CalcpadViz script
            if (!string.IsNullOrEmpty(_vizScriptPath) && File.Exists(_vizScriptPath))
            {
                sb.AppendLine("<script>");
                sb.AppendLine(File.ReadAllText(_vizScriptPath));
                sb.AppendLine("</script>");
            }

            // Include the div + script from parser
            sb.AppendLine(lineHtml);
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private async void InitGraphWebView(WebView2 wv, string html)
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync(
                    null,
                    System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "CalcpadMCGraphs"),
                    new CoreWebView2EnvironmentOptions());
                await wv.EnsureCoreWebView2Async(env);
                wv.CoreWebView2.Settings.AreDevToolsEnabled = false;
                wv.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                wv.NavigateToString(html);
            }
            catch { }
        }

        /// <summary>
        /// Check if a line is a symbolic block keyword (#sym, #end sym, #python, #maxima, #pip, etc.)
        /// These lines are processed by the parser but shown as keywords, not as code.
        /// Lines INSIDE the block (diff, integrate, etc.) flow through the parser HTML rendering.
        /// </summary>
        /// <summary>
        /// Check if a line is a symbolic block start/end keyword.
        /// These lines are shown as purple keywords and DON'T produce HTML output.
        /// Note: "#sym diff(...)" is INLINE symbolic — it DOES produce output, so NOT a keyword.
        /// Lines INSIDE #sym blocks (diff, integrate, etc.) go through normal math rendering.
        /// </summary>
        private static bool IsSymbolicKeyword(string line)
        {
            var trimmed = line.Trim().ToLowerInvariant();
            // Only block-level keywords (no output):
            return trimmed == "#sym" || trimmed == "#end sym" ||
                   trimmed == "#python" || trimmed == "#end python" ||
                   trimmed == "#maxima" || trimmed == "#end maxima" ||
                   trimmed.StartsWith("#pip ") ||
                   trimmed == "#end";
        }

        /// <summary>
        /// Check if a line produces parser HTML output.
        /// Inline #sym (e.g. "#sym diff(x^2;x)") DOES produce output.
        /// </summary>
        private static bool IsOutputProducingLine(string line)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) return false;
            if (IsSymbolicKeyword(trimmed)) return false;
            // Inline #sym produces output
            if (trimmed.ToLowerInvariant().StartsWith("#sym ")) return true;
            return true; // Most lines produce output
        }
    }
}
