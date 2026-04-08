using System.Windows;
using System.Windows.Media;

namespace Calcpad.Wpf.MathEditor
{
    /// <summary>
    /// Estilos visuales para el MathEditor - IDÉNTICOS al template.html de Calcpad
    /// FUENTE: Hekatan.Cli/doc/template.html
    /// </summary>
    public static class MathStyles
    {
        // ============================================================================
        // FUENTES - Exactamente como template.html línea 48-50
        // .eq, input[type="text"], table.matrix { font-family: 'Georgia Pro', 'Century Schoolbook', 'Times New Roman', Times, serif; }
        // ============================================================================
        public static readonly FontFamily EquationFont = new FontFamily("Georgia Pro, Century Schoolbook, Times New Roman, Times, serif");

        // .eq sub, .eq small { font-family: Calibri, Candara, Corbel, sans-serif; }
        public static readonly FontFamily SubscriptFont = new FontFamily("Calibri, Candara, Corbel, sans-serif");
        public static readonly FontFamily SymbolFont = new FontFamily("Cambria Math");
        public static readonly FontFamily UIFont = new FontFamily("Segoe UI, Arial Nova, Helvetica, sans-serif");

        // ============================================================================
        // COLORES - Exactamente como template.html
        // ============================================================================
        // .eq var { color: #06d; } (línea 52-55)
        public static readonly Brush VariableColor = new SolidColorBrush(Color.FromRgb(0x00, 0x66, 0xDD));

        // .eq i { color: #086; } (línea 57-61) - funciones
        public static readonly Brush FunctionColor = new SolidColorBrush(Color.FromRgb(0x00, 0x88, 0x66));

        // i.unit { color: #043; } (línea 63-67) - unidades
        public static readonly Brush UnitColor = new SolidColorBrush(Color.FromRgb(0x00, 0x44, 0x33));

        public static readonly Brush NumberColor = Brushes.Black;
        public static readonly Brush OperatorColor = Brushes.Black;
        public static readonly Brush TextColor = Brushes.Black;
        public static readonly Brush SelectionColor = Brushes.Blue;
        public static readonly Brush CursorColor = new SolidColorBrush(Color.FromRgb(0x00, 0x66, 0xDD));
        public static readonly Brush LineColor = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
        public static readonly Brush BracketColor = Brushes.Black;

        // Colores de fondo
        public static readonly Brush EditorBackground = Brushes.White;
        public static readonly Brush LineNumberBackground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
        public static readonly Brush SelectionBackground = new SolidColorBrush(Color.FromArgb(50, 0, 102, 221));

        // ============================================================================
        // TAMAÑOS RELATIVOS - Exactamente como template.html
        // ============================================================================
        // .eq var { font-size: 105%; } (línea 54)
        public const double VariableSizeRatio = 1.05;

        // .eq i { font-size: 90%; } (línea 60) - funciones
        public const double FunctionSizeRatio = 0.90;

        // .eq sub { font-size: 80%; } (línea 80)
        public const double SubscriptSizeRatio = 0.80;

        // .eq sup { font-size: 75%; } (línea 88)
        public const double SuperscriptSizeRatio = 0.75;

        // .eq small { font-size: 70%; } (línea 93)
        public const double SmallSizeRatio = 0.70;

        // sup.unit { font-size: 70%; } (línea 71)
        public const double UnitSupSizeRatio = 0.70;

        public const double FractionSizeRatio = 0.85;

        // ============================================================================
        // POSICIONAMIENTO - Exactamente como template.html
        // ============================================================================
        // .eq sub { vertical-align: -18%; } (línea 81)
        public const double SubscriptVerticalAlign = -0.18;

        // .eq sup { margin-top: -3pt; margin-left: 1pt; } (línea 86-87)
        public const double SuperscriptMarginTop = -3.0;  // en pt
        public const double SuperscriptMarginLeft = 1.0;  // en pt

        // Espaciado
        public const double LineHeight = 1.5;
        public const double ElementSpacing = 4.0;
        public const double FractionLineThickness = 1.0;
        public const double BracketThickness = 1.5;

        // Typefaces para FormattedText
        public static readonly Typeface EquationTypeface = new Typeface(EquationFont, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        public static readonly Typeface EquationItalicTypeface = new Typeface(EquationFont, FontStyles.Italic, FontWeights.Normal, FontStretches.Normal);
        public static readonly Typeface SubscriptTypeface = new Typeface(SubscriptFont, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        public static readonly Typeface SymbolTypeface = new Typeface(SymbolFont, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        /// <summary>
        /// Detecta el tipo de contenido y devuelve el color apropiado
        /// </summary>
        public static Brush GetColorForContent(string text)
        {
            if (string.IsNullOrEmpty(text))
                return TextColor;

            // Si es un número
            if (char.IsDigit(text[0]) || (text[0] == '-' && text.Length > 1 && char.IsDigit(text[1])))
                return NumberColor;

            // Si es una función conocida
            if (IsKnownFunction(text))
                return FunctionColor;

            // Si es un operador
            if (IsOperator(text))
                return OperatorColor;

            // Por defecto, variable
            return VariableColor;
        }

        /// <summary>
        /// Verifica si el texto es una función conocida
        /// </summary>
        public static bool IsKnownFunction(string text)
        {
            var functions = new[]
            {
                "sin", "cos", "tan", "cot", "sec", "csc",
                "asin", "acos", "atan", "acot", "asec", "acsc",
                "sinh", "cosh", "tanh", "coth", "sech", "csch",
                "asinh", "acosh", "atanh", "acoth", "asech", "acsch",
                "log", "ln", "exp", "sqrt", "cbrt", "root",
                "abs", "sign", "floor", "ceiling", "round", "trunc",
                "min", "max", "sum", "product", "average",
                "if", "switch", "not", "and", "or", "xor",
                "vector", "matrix", "len", "size", "det", "inv", "transpose"
            };

            string lowerText = text.ToLowerInvariant();
            foreach (var func in functions)
            {
                if (lowerText == func || lowerText.StartsWith(func + "("))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Verifica si el texto es un operador
        /// </summary>
        public static bool IsOperator(string text)
        {
            var operators = new[] { "+", "-", "*", "/", "^", "=", "<", ">", "≤", "≥", "≠", "≡", "∧", "∨", "⊕" };
            foreach (var op in operators)
            {
                if (text == op)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Verifica si un carácter individual es un operador o símbolo que no debe ser itálica
        /// </summary>
        public static bool IsOperatorChar(char c)
        {
            // Operadores y símbolos que deben ser fuente normal (no itálica)
            return c == '+' || c == '-' || c == '*' || c == '/' || c == '^' ||
                   c == '=' || c == '<' || c == '>' || c == '(' || c == ')' ||
                   c == '[' || c == ']' || c == '{' || c == '}' || c == '|' ||
                   c == ',' || c == ';' || c == ':' || c == '.' || c == ' ' ||
                   c == '≤' || c == '≥' || c == '≠' || c == '≡' || c == '∧' || c == '∨' || c == '⊕';
        }
    }
}
