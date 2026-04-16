using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Calcpad.Wpf.MathEditor
{
    /// <summary>
    /// Elemento para código de lenguajes externos (HTML, CSS, TypeScript, C, Fortran, etc.)
    /// Permite edición directa con cursor, similar a MathText pero con estilo de código monoespaciado
    /// </summary>
    public class MathCode : MathElement
    {
        private string _code = "";

        /// <summary>
        /// El código fuente
        /// </summary>
        public string Code
        {
            get => _code;
            set => _code = value ?? "";
        }

        /// <summary>
        /// Lenguaje del código (html, css, ts, c, fortran, etc.)
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Posición del cursor dentro del código
        /// </summary>
        public int CursorPosition { get; set; } = 0;

        // Selección de texto
        public int SelectionStart { get; set; } = -1;
        public int SelectionEnd { get; set; } = -1;
        public bool HasSelection => SelectionStart >= 0 && SelectionEnd >= 0 && SelectionStart != SelectionEnd;

        public MathCode() { }

        public MathCode(string code, string language = "code")
        {
            Code = code;
            Language = language?.ToLowerInvariant() ?? "code";
        }

        public void ClearSelection()
        {
            SelectionStart = -1;
            SelectionEnd = -1;
        }

        public void SetSelection(int start, int end)
        {
            SelectionStart = Math.Min(start, end);
            SelectionEnd = Math.Max(start, end);
        }

        public string GetSelectedText()
        {
            if (!HasSelection) return "";
            int start = Math.Min(SelectionStart, SelectionEnd);
            int end = Math.Max(SelectionStart, SelectionEnd);
            if (start < 0) start = 0;
            if (end > Code.Length) end = Code.Length;
            return Code.Substring(start, end - start);
        }

        public void DeleteSelection()
        {
            if (!HasSelection) return;
            int start = Math.Min(SelectionStart, SelectionEnd);
            int end = Math.Max(SelectionStart, SelectionEnd);
            Code = Code.Remove(start, end - start);
            CursorPosition = start;
            ClearSelection();
        }

        public override void Measure(double fontSize)
        {
            if (string.IsNullOrEmpty(Code))
            {
                Width = 4; // Ancho mínimo para cursor
                Height = fontSize * 0.85;
                Baseline = Height * 0.8;
                return;
            }

            var formattedText = new FormattedText(
                Code,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas"),
                fontSize * 0.85,
                Brushes.Black,
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

            Width = formattedText.Width + 4; // +4 para padding
            Height = formattedText.Height;
            Baseline = formattedText.Baseline;
        }

        public override void Render(Canvas canvas, double x, double y, double fontSize)
        {
            X = x;
            Y = y;

            var codeFont = new FontFamily("Consolas");
            var codeFontSize = fontSize * 0.85;
            var foreground = GetLanguageColor();

            // Dibujar fondo de selección si hay selección
            if (HasSelection)
            {
                double selStartX = x + GetOffsetForPosition(SelectionStart, codeFontSize);
                double selEndX = x + GetOffsetForPosition(SelectionEnd, codeFontSize);
                double selLeft = Math.Min(selStartX, selEndX);
                double selWidth = Math.Abs(selEndX - selStartX);

                var selectionRect = new Rectangle
                {
                    Width = selWidth,
                    Height = Height - 2,
                    Fill = new SolidColorBrush(Color.FromArgb(100, 0, 120, 215))
                };
                Canvas.SetLeft(selectionRect, selLeft);
                Canvas.SetTop(selectionRect, y + 1);
                canvas.Children.Add(selectionRect);
            }

            // Dibujar fondo si está seleccionado todo el elemento
            if (IsSelected)
            {
                var selectionBackground = new Rectangle
                {
                    Width = Math.Max(Width, 4),
                    Height = Height,
                    Fill = new SolidColorBrush(Color.FromArgb(100, 0, 120, 215))
                };
                Canvas.SetLeft(selectionBackground, x);
                Canvas.SetTop(selectionBackground, y);
                canvas.Children.Add(selectionBackground);
            }

            // Dibujar el código
            if (!string.IsNullOrEmpty(Code))
            {
                var textBlock = new TextBlock
                {
                    Text = Code,
                    FontFamily = codeFont,
                    FontSize = codeFontSize,
                    Foreground = foreground
                };
                Canvas.SetLeft(textBlock, x);
                Canvas.SetTop(textBlock, y);
                canvas.Children.Add(textBlock);
            }

            // Dibujar cursor si está aquí
            if (IsCursorHere)
            {
                var cursorX = x + GetCursorOffset(codeFontSize);
                var line = new Line
                {
                    X1 = cursorX,
                    Y1 = y + 2,
                    X2 = cursorX,
                    Y2 = y + Height - 2,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1.5
                };
                canvas.Children.Add(line);
            }
        }

        private double GetOffsetForPosition(int position, double fontSize)
        {
            if (position <= 0 || string.IsNullOrEmpty(_code))
                return 0;

            var textBefore = _code.Substring(0, Math.Min(position, _code.Length));
            var formattedText = new FormattedText(
                textBefore,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas"),
                fontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

            return formattedText.Width;
        }

        private double GetCursorOffset(double fontSize)
        {
            if (CursorPosition == 0 || string.IsNullOrEmpty(_code))
                return 0;

            var textBefore = _code.Substring(0, Math.Min(CursorPosition, _code.Length));
            var formattedText = new FormattedText(
                textBefore,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas"),
                fontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

            return formattedText.Width;
        }

        /// <summary>
        /// Obtiene el color según el lenguaje
        /// </summary>
        private Brush GetLanguageColor()
        {
            return (Language?.ToLowerInvariant()) switch
            {
                "html" => new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)), // Negro para HTML
                "css" => new SolidColorBrush(Color.FromRgb(0x26, 0x4D, 0xE4)),  // Azul CSS
                "ts" or "typescript" or "js" or "javascript" => new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC)),
                "c" => new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),    // Gris C
                "cpp" or "c++" => new SolidColorBrush(Color.FromRgb(0x00, 0x59, 0x9C)),
                "fortran" => new SolidColorBrush(Color.FromRgb(0x73, 0x4F, 0x96)),
                "python" => new SolidColorBrush(Color.FromRgb(0x30, 0x72, 0xA4)),
                "csharp" or "cs" => new SolidColorBrush(Color.FromRgb(0x68, 0x21, 0x7A)),
                "rust" => new SolidColorBrush(Color.FromRgb(0xDE, 0xA5, 0x84)),
                "markdown" or "md" => new SolidColorBrush(Color.FromRgb(0x08, 0x3F, 0xA1)),
                _ => Brushes.Black
            };
        }

        public override string ToCalcpad() => Code;

        /// <summary>
        /// Inserta un carácter en la posición del cursor
        /// </summary>
        public void InsertChar(char c)
        {
            if (HasSelection)
            {
                DeleteSelection();
            }
            Code = Code.Insert(CursorPosition, c.ToString());
            CursorPosition++;
        }

        /// <summary>
        /// Inserta texto en la posición del cursor
        /// </summary>
        public void InsertText(string text)
        {
            if (HasSelection)
            {
                DeleteSelection();
            }
            Code = Code.Insert(CursorPosition, text);
            CursorPosition += text.Length;
        }

        /// <summary>
        /// Elimina el carácter antes del cursor (Backspace)
        /// </summary>
        public void DeleteChar()
        {
            if (HasSelection)
            {
                DeleteSelection();
                return;
            }
            if (CursorPosition > 0 && Code.Length > 0)
            {
                Code = Code.Remove(CursorPosition - 1, 1);
                CursorPosition--;
            }
        }

        /// <summary>
        /// Elimina el carácter después del cursor (Delete)
        /// </summary>
        public void DeleteForward()
        {
            if (HasSelection)
            {
                DeleteSelection();
                return;
            }
            if (CursorPosition < Code.Length)
            {
                Code = Code.Remove(CursorPosition, 1);
            }
        }
    }
}
