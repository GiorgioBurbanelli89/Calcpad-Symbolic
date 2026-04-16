using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Calcpad.Wpf.MathEditor
{
    /// <summary>
    /// Elemento de potencia: base^exponente
    /// </summary>
    public class MathPower : MathElement
    {
        public MathElement Base { get; set; }
        public MathElement Exponent { get; set; }

        // NO usar constantes locales - usar MathStyles para mantener sincronizado con template.html

        public MathPower()
        {
            Base = new MathText();
            Exponent = new MathText();
            Base.Parent = this;
            Exponent.Parent = this;
        }

        public MathPower(MathElement baseElement, MathElement exponent)
        {
            Base = baseElement;
            Exponent = exponent;
            Base.Parent = this;
            Exponent.Parent = this;
        }

        public override void Measure(double fontSize)
        {
            // Usar valores de MathStyles (idénticos a template.html)
            // .eq sup { font-size: 75%; margin-top: -3pt; margin-left: 1pt; display: inline-block; }
            Base.Measure(fontSize);
            Exponent.Measure(fontSize * MathStyles.SuperscriptSizeRatio); // 75%

            // Ancho = base + exponente (el margin-left es muy pequeño, ~1pt)
            Width = Base.Width + Exponent.Width + 1;

            // IMPORTANTE: La altura es SOLO la de la base (como en HTML, <sup> no cambia la altura del padre)
            // Esto permite que los elementos siguientes se alineen correctamente
            Height = Base.Height;
            Baseline = Base.Baseline;
        }

        public override void Render(Canvas canvas, double x, double y, double fontSize)
        {
            X = x;
            Y = y;

            // Renderizar base en la posición normal
            Base.Render(canvas, x, y, fontSize);

            // Renderizar exponente arriba a la derecha de la base
            // El exponente sube aproximadamente 40% de la altura de la base (como <sup> en HTML)
            var expX = x + Base.Width + 1; // margin-left: 1pt
            var expY = y - (Base.Height * 0.35); // Subir el exponente
            Exponent.Render(canvas, expX, expY, fontSize * MathStyles.SuperscriptSizeRatio);

            // Borde de selección
            if (IsSelected)
            {
                var border = new Rectangle
                {
                    Width = Width,
                    Height = Height,
                    Stroke = Brushes.LightBlue,
                    StrokeThickness = 1,
                    Fill = new SolidColorBrush(Color.FromArgb(30, 0, 120, 255))
                };
                Canvas.SetLeft(border, x);
                Canvas.SetTop(border, y);
                canvas.Children.Add(border);
            }
        }

        public override string ToCalcpad()
        {
            var baseStr = Base.ToCalcpad();
            var expStr = Exponent.ToCalcpad();

            // Si la base es compleja, necesita paréntesis
            if (NeedsParentheses(baseStr))
                baseStr = $"({baseStr})";

            // Si el exponente es complejo, necesita paréntesis
            if (NeedsParentheses(expStr))
                return $"{baseStr}^({expStr})";

            return $"{baseStr}^{expStr}";
        }

        private bool NeedsParentheses(string expr)
        {
            return expr.Contains('+') || expr.Contains('-') ||
                   expr.Contains('*') || expr.Contains('/') ||
                   expr.Contains('^');
        }

        public override MathElement HitTest(double x, double y)
        {
            var baseHit = Base.HitTest(x, y);
            if (baseHit != null) return baseHit;

            var expHit = Exponent.HitTest(x, y);
            if (expHit != null) return expHit;

            return base.HitTest(x, y);
        }
    }
}
