using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Calcpad.Wpf.MathEditor
{
    /// <summary>
    /// Elemento para bloques de código externo (HTML, CSS, TypeScript, C, Fortran, etc.)
    /// Se muestra como: | LANGUAGE [+/-] con código colapsable y editable
    /// </summary>
    public class MathExternalBlock : MathElement
    {
        public string Language { get; set; }  // "html", "css", "c", "fortran", etc.
        public string Code { get; set; }      // Contenido del bloque (sin @{} delimiters)
        public bool IsCollapsed { get; set; } // true = [+], false = [-]

        // Soporte para cursor y edición (similar a MathText)
        public int CursorPosition { get; set; } = 0;
        public int CursorLine { get; set; } = 0; // Línea actual dentro del código
        public bool IsEditing { get; set; } = false; // true cuando está en modo edición

        // Guardar la posición X real donde se renderiza el código (para cálculo preciso del cursor)
        private double _renderedCodeStartX = 0;

        private const double FoldButtonSize = 14;
        private const double BarWidth = 3;
        private const double Padding = 5;
        private const double LineHeight = 1.2; // Factor de altura de línea
        private const double BorderOffset = 1; // BorderThickness del fondo

        public MathExternalBlock(string language, string code, bool collapsed = true)
        {
            Language = language?.ToUpperInvariant() ?? "CODE";
            Code = code ?? "";
            IsCollapsed = collapsed;
        }

        public override void Measure(double fontSize)
        {
            // Siempre mostrar ambos botones [+][-] cuando está expandido, solo [+] cuando está colapsado
            var headerStr = IsCollapsed ? $"| {Language} [+]" : $"| {Language} [+][-]";
            var formattedText = new FormattedText(
                headerStr,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas"),
                fontSize * 0.9,
                Brushes.Black,
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

            // FIXED: Include BarWidth in total width for correct hit testing
            Width = BarWidth + formattedText.Width + Padding * 3;
            Height = formattedText.Height + Padding;
            Baseline = Height * 0.8;

            // Si está expandido, agregar altura del código
            if (!IsCollapsed && !string.IsNullOrEmpty(Code))
            {
                var codeLines = Code.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                var codeHeight = codeLines.Length * fontSize * 1.2;
                Height += codeHeight + Padding;

                // FIXED: Calculate max width of code lines for correct hit testing
                double maxCodeWidth = 0;
                foreach (var line in codeLines)
                {
                    var lineText = new FormattedText(
                        line,
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Consolas"),
                        fontSize * 0.85,
                        Brushes.Black,
                        VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);
                    maxCodeWidth = Math.Max(maxCodeWidth, lineText.Width);
                }

                // Use the wider of header width or code width
                Width = Math.Max(Width, BarWidth + maxCodeWidth + Padding * 4);
            }
        }

        public override void Render(Canvas canvas, double x, double y, double fontSize)
        {
            X = x;
            Y = y;

            // Fondo con borde
            var background = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Width = Width,
                Height = Height
            };
            Canvas.SetLeft(background, x);
            Canvas.SetTop(background, y);
            canvas.Children.Add(background);

            // Barra vertical izquierda (indicador de bloque)
            var bar = new Rectangle
            {
                Width = BarWidth,
                Height = Height - 2,
                Fill = GetLanguageColor()
            };
            Canvas.SetLeft(bar, x + 2);
            Canvas.SetTop(bar, y + 1);
            canvas.Children.Add(bar);

            // Texto del header: | LANGUAGE [+] o | LANGUAGE [+][-]
            // Colapsado: solo [+] | Expandido: [+] para colapsar, [-] indica estado
            var headerStr = IsCollapsed ? $"| {Language} [+]" : $"| {Language} [+][-]";
            var headerText = new TextBlock
            {
                Text = headerStr,
                FontFamily = new FontFamily("Consolas"),
                FontSize = fontSize * 0.9,
                FontWeight = FontWeights.Bold,
                Foreground = GetLanguageColor(),
                ToolTip = IsCollapsed ? "Click [+] para expandir" : "Click [+] para colapsar"
            };
            Canvas.SetLeft(headerText, x + BarWidth + Padding);
            Canvas.SetTop(headerText, y + Padding / 2);
            canvas.Children.Add(headerText);

            // Si está expandido, mostrar el código (editable)
            if (!IsCollapsed && !string.IsNullOrEmpty(Code))
            {
                var codeLines = Code.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                double codeY = y + fontSize * LineHeight + Padding;
                double codeFontSize = fontSize * 0.85;
                double lineH = fontSize * LineHeight;

                // Guardar la posición X real donde se renderiza el código
                _renderedCodeStartX = x + BarWidth + Padding * 2;

                for (int lineIdx = 0; lineIdx < codeLines.Length; lineIdx++)
                {
                    var lineText = codeLines[lineIdx];
                    var codeText = new TextBlock
                    {
                        Text = lineText,
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = codeFontSize,
                        Foreground = Brushes.Black,
                        TextWrapping = TextWrapping.NoWrap
                    };
                    Canvas.SetLeft(codeText, _renderedCodeStartX);
                    Canvas.SetTop(codeText, codeY);
                    canvas.Children.Add(codeText);

                    // Dibujar cursor si está en esta línea
                    if (IsCursorHere && CursorLine == lineIdx)
                    {
                        var cursorX = _renderedCodeStartX + GetCursorOffset(lineText, CursorPosition, codeFontSize);

                        // Cursor
                        var cursorLine = new Line
                        {
                            X1 = cursorX,
                            Y1 = codeY,
                            X2 = cursorX,
                            Y2 = codeY + lineH,
                            Stroke = Brushes.DarkBlue,
                            StrokeThickness = 1.5
                        };
                        canvas.Children.Add(cursorLine);
                    }

                    codeY += lineH;
                }
            }
            // Si está colapsado pero tiene cursor, mostrar cursor después del header - más visible
            else if (IsCollapsed && IsCursorHere)
            {
                var cursorX = x + Width - Padding;
                var cursorLine = new Line
                {
                    X1 = cursorX,
                    Y1 = y + 2,
                    X2 = cursorX,
                    Y2 = y + Height - 2,
                    Stroke = Brushes.Black,
                    StrokeThickness = 2
                };
                canvas.Children.Add(cursorLine);
            }

            // Highlight si está seleccionado
            if (IsSelected)
            {
                var selectionRect = new Rectangle
                {
                    Width = Width,
                    Height = Height,
                    Stroke = Brushes.Blue,
                    StrokeThickness = 2,
                    Fill = new SolidColorBrush(Color.FromArgb(30, 0, 120, 215))
                };
                Canvas.SetLeft(selectionRect, x);
                Canvas.SetTop(selectionRect, y);
                canvas.Children.Add(selectionRect);
            }
        }

        /// <summary>
        /// Obtiene el color según el lenguaje
        /// </summary>
        private Brush GetLanguageColor()
        {
            return Language.ToLowerInvariant() switch
            {
                "html" => new SolidColorBrush(Color.FromRgb(0xE3, 0x4C, 0x26)), // Naranja HTML
                "css" => new SolidColorBrush(Color.FromRgb(0x26, 0x4D, 0xE4)),  // Azul CSS
                "ts" or "typescript" or "js" or "javascript" => new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC)), // Azul TypeScript
                "c" => new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),    // Gris C
                "cpp" or "c++" => new SolidColorBrush(Color.FromRgb(0x00, 0x59, 0x9C)), // Azul C++
                "fortran" => new SolidColorBrush(Color.FromRgb(0x73, 0x4F, 0x96)), // Morado Fortran
                "python" => new SolidColorBrush(Color.FromRgb(0x30, 0x72, 0xA4)), // Azul Python
                "csharp" or "cs" => new SolidColorBrush(Color.FromRgb(0x68, 0x21, 0x7A)), // Morado C#
                "rust" => new SolidColorBrush(Color.FromRgb(0xDE, 0xA5, 0x84)), // Naranja Rust
                "markdown" or "md" => new SolidColorBrush(Color.FromRgb(0x08, 0x3F, 0xA1)), // Azul Markdown
                _ => new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80))      // Gris por defecto
            };
        }

        public override string ToCalcpad()
        {
            // Retornar el formato original: @{language}\ncode\n@{end language}
            var langLower = Language.ToLowerInvariant();
            return $"@{{{langLower}}}\n{Code}\n@{{end {langLower}}}";
        }

        /// <summary>
        /// Toggle collapse/expand al hacer click
        /// </summary>
        public void ToggleCollapse()
        {
            IsCollapsed = !IsCollapsed;
        }

        /// <summary>
        /// Indica si un punto está en el área del header (para toggle collapse)
        /// El header es la primera línea con "| LANGUAGE [+/-]"
        /// </summary>
        public bool IsClickOnHeader(double clickX, double clickY, double fontSize)
        {
            // Calcular la altura real del header usando FormattedText (igual que en Measure)
            var headerStr = IsCollapsed ? $"| {Language} [+]" : $"| {Language} [+][-]";
            var formattedText = new FormattedText(
                headerStr,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas"),
                fontSize * 0.9,
                Brushes.Black,
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

            // El header se dibuja en Y + Padding/2, altura = formattedText.Height
            double headerTop = Y;
            double headerBottom = Y + formattedText.Height + Padding;

            // Verificar si el click está dentro del área del header
            return clickX >= X && clickX <= X + Width && clickY >= headerTop && clickY <= headerBottom;
        }

        public override MathElement HitTest(double x, double y)
        {
            // Verificar si está dentro del bloque completo
            if (x >= X && x <= X + Width && y >= Y && y <= Y + Height)
            {
                return this;
            }

            return null;
        }

        /// <summary>
        /// Calcula el offset horizontal del cursor para una línea de código
        /// Usa ancho fijo por carácter (Consolas es monoespaciada)
        /// </summary>
        private double GetCursorOffset(string lineText, int cursorPos, double fontSize)
        {
            if (cursorPos <= 0 || string.IsNullOrEmpty(lineText))
                return 0;

            // Consolas es monoespaciada - calcular ancho de un carácter de referencia
            var charWidth = GetMonospaceCharWidth(fontSize);
            return cursorPos * charWidth;
        }

        /// <summary>
        /// Obtiene el ancho de un carácter en fuente Consolas (monoespaciada)
        /// </summary>
        private double GetMonospaceCharWidth(double fontSize)
        {
            // Usar un carácter visible para medir (no espacio)
            var formattedText = new FormattedText(
                "M",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas"),
                fontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

            return formattedText.Width;
        }

        /// <summary>
        /// Obtiene las líneas del código como array
        /// </summary>
        public string[] GetCodeLines()
        {
            return Code?.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None) ?? new string[] { "" };
        }

        /// <summary>
        /// Obtiene la línea actual donde está el cursor
        /// </summary>
        public string GetCurrentLine()
        {
            var lines = GetCodeLines();
            if (CursorLine >= 0 && CursorLine < lines.Length)
                return lines[CursorLine];
            return "";
        }

        /// <summary>
        /// Establece el contenido de la línea actual donde está el cursor
        /// No modifica CursorPosition - eso lo hace el llamador si es necesario
        /// </summary>
        public void SetCurrentLine(string newContent)
        {
            var lines = new System.Collections.Generic.List<string>(GetCodeLines());
            LogToFile($"[SetCurrentLine] CursorLine={CursorLine} lines.Count={lines.Count} newContent='{newContent}'");
            if (CursorLine >= 0 && CursorLine < lines.Count)
            {
                var oldLine = lines[CursorLine];
                lines[CursorLine] = newContent ?? "";
                Code = string.Join("\n", lines);
                LogToFile($"[SetCurrentLine] oldLine='{oldLine}' -> newLine='{newContent}'");
            }
            else
            {
                LogToFile($"[SetCurrentLine] SKIPPED - CursorLine out of range");
            }
        }

        private static void LogToFile(string message)
        {
            try
            {
                var logPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "calcpad_debug.log");
                System.IO.File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss.fff} {message}\n");
            }
            catch { }
        }

        /// <summary>
        /// Inserta un carácter en la posición del cursor
        /// </summary>
        public void InsertChar(char c)
        {
            var lines = GetCodeLines();
            if (CursorLine >= 0 && CursorLine < lines.Length)
            {
                var line = lines[CursorLine];
                var pos = Math.Min(CursorPosition, line.Length);
                lines[CursorLine] = line.Insert(pos, c.ToString());
                Code = string.Join("\n", lines);
                CursorPosition++;
            }
        }

        /// <summary>
        /// Inserta una nueva línea (Enter)
        /// </summary>
        public void InsertNewLine()
        {
            var lines = new System.Collections.Generic.List<string>(GetCodeLines());
            if (CursorLine >= 0 && CursorLine < lines.Count)
            {
                var line = lines[CursorLine];
                var pos = Math.Min(CursorPosition, line.Length);
                var before = line.Substring(0, pos);
                var after = line.Substring(pos);
                lines[CursorLine] = before;
                lines.Insert(CursorLine + 1, after);
                Code = string.Join("\n", lines);
                CursorLine++;
                CursorPosition = 0;
            }
        }

        /// <summary>
        /// Elimina el carácter antes del cursor (Backspace)
        /// </summary>
        public void DeleteChar()
        {
            var lines = new System.Collections.Generic.List<string>(GetCodeLines());
            if (CursorLine >= 0 && CursorLine < lines.Count)
            {
                var line = lines[CursorLine];
                if (CursorPosition > 0 && CursorPosition <= line.Length)
                {
                    lines[CursorLine] = line.Remove(CursorPosition - 1, 1);
                    CursorPosition--;
                }
                else if (CursorPosition == 0 && CursorLine > 0)
                {
                    // Unir con la línea anterior
                    var prevLine = lines[CursorLine - 1];
                    CursorPosition = prevLine.Length;
                    lines[CursorLine - 1] = prevLine + line;
                    lines.RemoveAt(CursorLine);
                    CursorLine--;
                }
                Code = string.Join("\n", lines);
            }
        }

        /// <summary>
        /// Elimina el carácter después del cursor (Delete)
        /// </summary>
        public void DeleteForward()
        {
            var lines = new System.Collections.Generic.List<string>(GetCodeLines());
            if (CursorLine >= 0 && CursorLine < lines.Count)
            {
                var line = lines[CursorLine];
                if (CursorPosition < line.Length)
                {
                    lines[CursorLine] = line.Remove(CursorPosition, 1);
                }
                else if (CursorLine < lines.Count - 1)
                {
                    // Unir con la línea siguiente
                    lines[CursorLine] = line + lines[CursorLine + 1];
                    lines.RemoveAt(CursorLine + 1);
                }
                Code = string.Join("\n", lines);
            }
        }

        /// <summary>
        /// Mueve el cursor a la izquierda
        /// </summary>
        public void MoveCursorLeft()
        {
            if (CursorPosition > 0)
            {
                CursorPosition--;
            }
            else if (CursorLine > 0)
            {
                CursorLine--;
                var lines = GetCodeLines();
                CursorPosition = lines[CursorLine].Length;
            }
        }

        /// <summary>
        /// Mueve el cursor a la derecha
        /// </summary>
        public void MoveCursorRight()
        {
            var lines = GetCodeLines();
            var currentLineLen = CursorLine < lines.Length ? lines[CursorLine].Length : 0;
            if (CursorPosition < currentLineLen)
            {
                CursorPosition++;
            }
            else if (CursorLine < lines.Length - 1)
            {
                CursorLine++;
                CursorPosition = 0;
            }
        }

        /// <summary>
        /// Mueve el cursor arriba
        /// </summary>
        public void MoveCursorUp()
        {
            if (CursorLine > 0)
            {
                CursorLine--;
                var lines = GetCodeLines();
                CursorPosition = Math.Min(CursorPosition, lines[CursorLine].Length);
            }
        }

        /// <summary>
        /// Mueve el cursor abajo
        /// </summary>
        public void MoveCursorDown()
        {
            var lines = GetCodeLines();
            if (CursorLine < lines.Length - 1)
            {
                CursorLine++;
                CursorPosition = Math.Min(CursorPosition, lines[CursorLine].Length);
            }
        }

        /// <summary>
        /// Calcula la línea y posición del cursor basado en coordenadas de click
        /// Recibe elementX y elementY para cálculos precisos
        /// </summary>
        public void SetCursorFromClick(double clickX, double clickY, double fontSize, double elementX, double elementY)
        {
            if (IsCollapsed)
            {
                CursorLine = 0;
                CursorPosition = 0;
                return;
            }

            var lines = GetCodeLines();
            // El código empieza después del header
            var codeStartY = elementY + fontSize * LineHeight + Padding;
            var lineHeightPx = fontSize * LineHeight;

            // Encontrar la línea
            int lineIdx = (int)((clickY - codeStartY) / lineHeightPx);
            lineIdx = Math.Max(0, Math.Min(lineIdx, lines.Length - 1));
            CursorLine = lineIdx;

            // Encontrar la posición en la línea
            var lineText = lines[lineIdx];
            // Calcular codeStartX usando elementX (igual que en Render)
            var codeStartX = elementX + BarWidth + Padding * 2;
            var relativeX = clickX - codeStartX;
            var codeFontSize = fontSize * 0.85;

            // Si relativeX es negativo, poner cursor al inicio
            if (relativeX < 0)
            {
                CursorPosition = 0;
                return;
            }

            // Buscar la posición usando GetCursorOffset para consistencia con el Render
            int pos = 0;
            double lastOffset = 0;
            for (int i = 0; i <= lineText.Length; i++)
            {
                var offset = GetCursorOffset(lineText, i, codeFontSize);
                if (offset > relativeX)
                {
                    // Verificar si estamos más cerca de esta posición o la anterior
                    if (i > 0)
                    {
                        var prevOffset = GetCursorOffset(lineText, i - 1, codeFontSize);
                        pos = (relativeX - prevOffset < offset - relativeX) ? i - 1 : i;
                    }
                    else
                    {
                        pos = 0;
                    }
                    break;
                }
                lastOffset = offset;
                pos = i;
            }
            CursorPosition = pos;
        }

        /// <summary>
        /// Versión legacy que usa X e Y almacenados
        /// </summary>
        public void SetCursorFromClick(double clickX, double clickY, double fontSize)
        {
            SetCursorFromClick(clickX, clickY, fontSize, X, Y);
        }

        private double GetCharWidth(char c, double fontSize)
        {
            var formattedText = new FormattedText(
                c.ToString(),
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas"),
                fontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);
            return formattedText.Width;
        }
    }
}
