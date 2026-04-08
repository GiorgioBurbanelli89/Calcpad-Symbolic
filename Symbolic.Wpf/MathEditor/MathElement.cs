using System;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Calcpad.Wpf.MathEditor
{
    /// <summary>
    /// Clase base abstracta para todos los elementos matemáticos
    /// </summary>
    public abstract class MathElement
    {
        public MathElement Parent { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; protected set; }
        public double Height { get; protected set; }
        public double Baseline { get; protected set; } // Línea base para alineación vertical

        public bool IsSelected { get; set; }
        public bool IsCursorHere { get; set; }

        /// <summary>
        /// Mide el tamaño del elemento
        /// </summary>
        public abstract void Measure(double fontSize);

        /// <summary>
        /// Renderiza el elemento en el canvas
        /// </summary>
        public abstract void Render(Canvas canvas, double x, double y, double fontSize);

        /// <summary>
        /// Convierte a sintaxis Calcpad
        /// </summary>
        public abstract string ToCalcpad();

        /// <summary>
        /// Obtiene el elemento en la posición especificada
        /// </summary>
        public virtual MathElement HitTest(double x, double y)
        {
            if (x >= X && x <= X + Width && y >= Y && y <= Y + Height)
                return this;
            return null;
        }
    }

    /// <summary>
    /// Elemento de texto simple
    /// </summary>
    public class MathText : MathElement
    {
        private string _text = "";

        /// <summary>
        /// El texto original (para guardar en archivo)
        /// </summary>
        public string Text
        {
            get => _text;
            set => _text = value ?? "";
        }

        /// <summary>
        /// Texto para mostrar (con transformaciones visuales para operadores)
        /// </summary>
        public string DisplayText
        {
            get
            {
                var decoded = WebUtility.HtmlDecode(_text);
                // Transformar operadores para visualizacion mejorada
                decoded = TransformOperatorsForDisplay(decoded);
                return decoded;
            }
        }

        /// <summary>
        /// Transforma operadores para visualizacion matematica correcta
        /// </summary>
        private static string TransformOperatorsForDisplay(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var result = new System.Text.StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                
                // Reemplazar * con · (middle dot)
                if (c == '*')
                {
                    // Solo agregar espacio si no hay uno ya
                    if (result.Length > 0 && result[result.Length - 1] != ' ')
                        result.Append(' ');
                    result.Append('·'); // Middle dot ·
                    // Agregar espacio despues si no hay
                    if (i + 1 < text.Length && text[i + 1] != ' ')
                        result.Append(' ');
                }
                // Agregar espacios alrededor de =
                else if (c == '=')
                {
                    if (result.Length > 0 && result[result.Length - 1] != ' ')
                        result.Append(' ');
                    result.Append(c);
                    if (i + 1 < text.Length && text[i + 1] != ' ')
                        result.Append(' ');
                }
                // Agregar espacio despues de coma
                else if (c == ',')
                {
                    result.Append(c);
                    if (i + 1 < text.Length && text[i + 1] != ' ')
                        result.Append(' ');
                }
                else
                {
                    result.Append(c);
                }
            }
            return result.ToString();
        }

        public int CursorPosition { get; set; } = 0;
        public bool IsVariable { get; set; } = true; // true = variable (itálica), false = número/operador
        public bool IsVector { get; set; } = false; // true = mostrar flecha de vector sobre el nombre

        // Selección de texto
        public int SelectionStart { get; set; } = -1; // -1 = sin selección
        public int SelectionEnd { get; set; } = -1;
        public bool HasSelection => SelectionStart >= 0 && SelectionEnd >= 0 && SelectionStart != SelectionEnd;

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
            if (end > Text.Length) end = Text.Length;
            return Text.Substring(start, end - start);
        }

        public void DeleteSelection()
        {
            if (!HasSelection) return;
            int start = Math.Min(SelectionStart, SelectionEnd);
            int end = Math.Max(SelectionStart, SelectionEnd);
            Text = Text.Remove(start, end - start);
            CursorPosition = start;
            ClearSelection();
        }

        public MathText() { }
        public MathText(string text) { Text = text; }

        public override void Measure(double fontSize)
        {
            var displayText = DisplayText; // Usar texto decodificado para medir

            if (string.IsNullOrEmpty(displayText))
            {
                Width = 2; // Ancho mínimo solo para cursor (2px)
                Height = fontSize;
                Baseline = fontSize * 0.8;
                return;
            }

            var typeface = GetTypeface();
            var formattedText = new FormattedText(
                displayText,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

            Width = formattedText.Width;
            Height = formattedText.Height;
            Baseline = formattedText.Baseline;

            // Si es vector, agregar espacio para el carácter de flecha Unicode
            if (IsVector)
            {
                // El carácter ⃗ se dibuja antes del texto, necesita un poco más de ancho
                Width += fontSize * 0.15; // Espacio extra para la flecha
            }
        }

        private Typeface GetTypeface()
        {
            // Detectar tipo de contenido
            if (string.IsNullOrEmpty(Text))
                return MathStyles.EquationItalicTypeface;

            // Números y operadores: fuente normal
            if (char.IsDigit(Text[0]) || MathStyles.IsOperator(Text))
                return MathStyles.EquationTypeface;

            // Funciones: fuente normal
            if (MathStyles.IsKnownFunction(Text))
                return MathStyles.EquationTypeface;

            // Variables: fuente itálica
            return MathStyles.EquationItalicTypeface;
        }

        public override void Render(Canvas canvas, double x, double y, double fontSize)
        {
            X = x;
            Y = y;

            double currentX = x;

            // Dibujar fondo de selección si el elemento completo está seleccionado
            if (IsSelected && Width > 0 && Height > 0)
            {
                var selectionBackground = new System.Windows.Shapes.Rectangle
                {
                    Width = Math.Max(Width, 4), // Mínimo 4px de ancho para elementos vacíos
                    Height = Height,
                    Fill = new SolidColorBrush(Color.FromArgb(100, 0, 120, 215)) // Azul semitransparente
                };
                Canvas.SetLeft(selectionBackground, x);
                Canvas.SetTop(selectionBackground, y);
                canvas.Children.Add(selectionBackground);
            }

            // Si es vector, dibujar el carácter de flecha Unicode antes del texto
            if (IsVector)
            {
                // Usar el mismo carácter y estilo que Hekatan: ⃗ (U+20D7)
                var arrowBlock = new TextBlock
                {
                    Text = "⃗", // COMBINING RIGHT ARROW ABOVE
                    FontFamily = new FontFamily("Cambria Math"),
                    FontStyle = FontStyles.Normal,
                    FontSize = fontSize,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0xAA, 0xFF)) // #8af
                };

                Canvas.SetLeft(arrowBlock, currentX);
                Canvas.SetTop(arrowBlock, y - fontSize * 0.1); // Ajuste vertical como en CSS
                canvas.Children.Add(arrowBlock);

                // No avanzar currentX, el carácter combinante se superpone
            }

            // Tokenizar el texto DECODIFICADO para aplicar highlighting apropiado
            var tokens = TokenizeForHighlighting(DisplayText);

            foreach (var token in tokens)
            {
                Brush foreground;
                FontStyle fontStyle;

                if (IsSelected)
                {
                    foreground = MathStyles.SelectionColor;
                    fontStyle = FontStyles.Normal;
                }
                else if (token.Type == TokenType.Function)
                {
                    foreground = MathStyles.FunctionColor;
                    fontStyle = FontStyles.Normal;
                }
                else if (token.Type == TokenType.Number || token.Type == TokenType.Operator)
                {
                    foreground = MathStyles.NumberColor;
                    fontStyle = FontStyles.Normal;
                }
                else // Variable
                {
                    foreground = MathStyles.VariableColor;
                    fontStyle = FontStyles.Italic;
                }

                var textBlock = new TextBlock
                {
                    Text = token.Text,
                    FontFamily = MathStyles.EquationFont,
                    FontStyle = fontStyle,
                    FontSize = fontSize,
                    Foreground = foreground
                };

                Canvas.SetLeft(textBlock, currentX);
                Canvas.SetTop(textBlock, y);
                canvas.Children.Add(textBlock);

                // Medir el ancho del token para el siguiente
                var formattedText = new FormattedText(
                    token.Text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(MathStyles.EquationFont, fontStyle, FontWeights.Normal, FontStretches.Normal),
                    fontSize,
                    Brushes.Black,
                    VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);
                currentX += formattedText.Width;
            }

            // Dibujar selección si hay
            if (HasSelection)
            {
                double selStartX = x + GetOffsetForPosition(SelectionStart, fontSize);
                double selEndX = x + GetOffsetForPosition(SelectionEnd, fontSize);
                double selLeft = Math.Min(selStartX, selEndX);
                double selWidth = Math.Abs(selEndX - selStartX);

                var selectionRect = new System.Windows.Shapes.Rectangle
                {
                    Width = selWidth,
                    Height = Height - 4,
                    Fill = new SolidColorBrush(Color.FromArgb(100, 0, 120, 215)) // Azul semitransparente
                };
                Canvas.SetLeft(selectionRect, selLeft);
                Canvas.SetTop(selectionRect, y + 2);
                canvas.Children.Add(selectionRect);
            }

            // Dibujar cursor si está aquí
            if (IsCursorHere)
            {
                var cursorX = x + GetCursorOffset(fontSize);
                var line = new System.Windows.Shapes.Line
                {
                    X1 = cursorX,
                    Y1 = y + 2,
                    X2 = cursorX,
                    Y2 = y + Height - 2,
                    Stroke = MathStyles.CursorColor,
                    StrokeThickness = 1.5
                };
                canvas.Children.Add(line);
            }
        }

        private double GetOffsetForPosition(int position, double fontSize)
        {
            if (position <= 0 || string.IsNullOrEmpty(_text))
                return 0;

            var originalBefore = _text.Substring(0, Math.Min(position, _text.Length));
            var decodedBefore = WebUtility.HtmlDecode(originalBefore);

            var formattedText = new FormattedText(
                decodedBefore,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                GetTypeface(),
                fontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

            return formattedText.Width;
        }

        private double GetCursorOffset(double fontSize)
        {
            var displayText = DisplayText;
            if (CursorPosition == 0 || string.IsNullOrEmpty(displayText))
                return 0;

            // Calcular posición del cursor basándose en el texto decodificado
            // Pero el CursorPosition está en términos del texto original
            // Necesitamos mapear la posición del cursor original al texto decodificado
            var originalBefore = _text.Substring(0, Math.Min(CursorPosition, _text.Length));
            var decodedBefore = WebUtility.HtmlDecode(originalBefore);

            var formattedText = new FormattedText(
                decodedBefore,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                GetTypeface(),
                fontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

            return formattedText.Width;
        }

        public override string ToCalcpad() => Text;

        public void InsertChar(char c)
        {
            Text = Text.Insert(CursorPosition, c.ToString());
            CursorPosition++;
        }

        public void DeleteChar()
        {
            if (CursorPosition > 0 && Text.Length > 0)
            {
                Text = Text.Remove(CursorPosition - 1, 1);
                CursorPosition--;
            }
        }

        /// <summary>
        /// Tokeniza el texto para aplicar highlighting
        /// </summary>
        private static System.Collections.Generic.List<HighlightToken> TokenizeForHighlighting(string text)
        {
            var tokens = new System.Collections.Generic.List<HighlightToken>();
            if (string.IsNullOrEmpty(text))
                return tokens;

            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];

                // Números
                if (char.IsDigit(c) || (c == '.' && i + 1 < text.Length && char.IsDigit(text[i + 1])))
                {
                    int start = i;
                    while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.'))
                        i++;
                    tokens.Add(new HighlightToken(text.Substring(start, i - start), TokenType.Number));
                }
                // Operadores y símbolos
                else if (MathStyles.IsOperatorChar(c))
                {
                    tokens.Add(new HighlightToken(c.ToString(), TokenType.Operator));
                    i++;
                }
                // Palabras (funciones o variables)
                else if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
                        i++;
                    string word = text.Substring(start, i - start);
                    var type = MathStyles.IsKnownFunction(word) ? TokenType.Function : TokenType.Variable;
                    tokens.Add(new HighlightToken(word, type));
                }
                else
                {
                    tokens.Add(new HighlightToken(c.ToString(), TokenType.Operator));
                    i++;
                }
            }
            return tokens;
        }
    }

    /// <summary>
    /// Tipos de token para highlighting
    /// </summary>
    internal enum TokenType
    {
        Number,
        Operator,
        Function,
        Variable
    }

    /// <summary>
    /// Token con tipo para highlighting
    /// </summary>
    internal class HighlightToken
    {
        public string Text { get; }
        public TokenType Type { get; }

        public HighlightToken(string text, TokenType type)
        {
            Text = text;
            Type = type;
        }
    }
}
