using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Calcpad.Wpf.MathEditor
{
    /// <summary>
    /// Elemento de subíndice: base_subscript
    /// </summary>
    public class MathSubscript : MathElement
    {
        public MathElement Base { get; set; }
        public MathElement Subscript { get; set; }

        public MathSubscript()
        {
            Base = new MathText();
            Subscript = new MathText();
            Base.Parent = this;
            Subscript.Parent = this;
        }

        public MathSubscript(MathElement baseElement, MathElement subscript)
        {
            Base = baseElement;
            Subscript = subscript;
            Base.Parent = this;
            Subscript.Parent = this;
        }

        // NO usar constantes locales - usar MathStyles para mantener sincronizado con template.html

        public override void Measure(double fontSize)
        {
            // Usar valores de MathStyles (idénticos a template.html)
            Base.Measure(fontSize);
            Subscript.Measure(fontSize * MathStyles.SubscriptSizeRatio); // 80%

            Width = Base.Width + Subscript.Width;
            // El subíndice empieza a 40% de la altura de la base
            // La altura total es: posición del subíndice + altura del subíndice
            Height = Base.Height * 0.4 + Subscript.Height;
            // Asegurar que la altura mínima sea la de la base
            if (Height < Base.Height) Height = Base.Height;
            Baseline = Base.Baseline;
        }

        public override void Render(Canvas canvas, double x, double y, double fontSize)
        {
            X = x;
            Y = y;

            // Renderizar base
            Base.Render(canvas, x, y, fontSize);

            // Renderizar subíndice (más pequeño, abajo a la derecha)
            // template.html: .eq sub { vertical-align: -18%; }
            // El subíndice se posiciona justo debajo de la línea base de la variable
            var subX = x + Base.Width;
            // Calcular posición Y: el subíndice empieza aproximadamente a 40% de la altura de la base
            // para que su parte superior esté cerca de la línea base
            var subY = y + Base.Height * 0.4;

            // Renderizar subíndice con fuente Calibri como en template.html
            // .eq sub { font-family: Calibri, Candara, Corbel, sans-serif; font-size: 80%; }
            RenderSubscriptElement(canvas, subX, subY, fontSize * MathStyles.SubscriptSizeRatio, Subscript);
        }

        /// <summary>
        /// Renderiza el elemento de subíndice con la fuente correcta (Calibri) como en template.html
        /// Aplica highlighting correcto: números en negro, variables en azul
        /// </summary>
        private void RenderSubscriptElement(Canvas canvas, double x, double y, double fontSize, MathElement element)
        {
            if (element is MathText textElement)
            {
                textElement.X = x;
                textElement.Y = y;

                // Determinar color según el contenido (igual que MathText.Render)
                Brush foreground;
                if (textElement.IsSelected)
                {
                    foreground = MathStyles.SelectionColor;
                }
                else
                {
                    // Detectar si es número o variable
                    string text = textElement.DisplayText;
                    if (!string.IsNullOrEmpty(text) && (char.IsDigit(text[0]) || text[0] == '.'))
                    {
                        foreground = MathStyles.NumberColor; // Negro para números
                    }
                    else
                    {
                        foreground = MathStyles.VariableColor; // Azul para variables
                    }
                }

                // Usar fuente Calibri para subíndices como en template.html
                var textBlock = new TextBlock
                {
                    Text = textElement.DisplayText,
                    FontFamily = MathStyles.SubscriptFont, // Calibri
                    FontStyle = FontStyles.Normal,
                    FontSize = fontSize,
                    Foreground = foreground
                };

                Canvas.SetLeft(textBlock, x);
                Canvas.SetTop(textBlock, y);
                canvas.Children.Add(textBlock);

                // Dibujar cursor si está aquí
                if (textElement.IsCursorHere)
                {
                    var cursorX = x + GetTextWidth(textElement.DisplayText.Substring(0, Math.Min(textElement.CursorPosition, textElement.DisplayText.Length)), fontSize);
                    var line = new System.Windows.Shapes.Line
                    {
                        X1 = cursorX,
                        Y1 = y + 2,
                        X2 = cursorX,
                        Y2 = y + fontSize - 2,
                        Stroke = MathStyles.CursorColor,
                        StrokeThickness = 1.5
                    };
                    canvas.Children.Add(line);
                }
            }
            else
            {
                // Otros elementos, usar render normal
                element.Render(canvas, x, y, fontSize);
            }
        }

        private double GetTextWidth(string text, double fontSize)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var formattedText = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                MathStyles.SubscriptTypeface,
                fontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);
            return formattedText.Width;
        }

        public override string ToCalcpad()
        {
            return $"{Base.ToCalcpad()}_{Subscript.ToCalcpad()}";
        }

        public override MathElement HitTest(double x, double y)
        {
            var hit = Subscript.HitTest(x, y);
            if (hit != null) return hit;

            hit = Base.HitTest(x, y);
            if (hit != null) return hit;

            if (x >= X && x <= X + Width && y >= Y && y <= Y + Height)
                return this;

            return null;
        }
    }
}
