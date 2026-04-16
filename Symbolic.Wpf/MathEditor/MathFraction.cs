using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Calcpad.Wpf.MathEditor
{
    /// <summary>
    /// Elemento de fracción: numerador / denominador
    /// </summary>
    public class MathFraction : MathElement
    {
        public MathElement Numerator { get; set; }
        public MathElement Denominator { get; set; }

        // Valores del template.html: .dvl { border-bottom: solid 1pt black; margin: 1pt }
        // .dvc { padding-left: 2pt; padding-right: 2pt; }
        private const double LineThickness = 1.0;   // border-bottom: solid 1pt
        private const double VerticalPadding = 1.0; // margin-top/bottom: 1pt
        private const double HorizontalPadding = 2.0; // padding-left/right: 2pt

        public MathFraction()
        {
            Numerator = new MathText();
            Denominator = new MathText();
            Numerator.Parent = this;
            Denominator.Parent = this;
        }

        public MathFraction(MathElement numerator, MathElement denominator)
        {
            Numerator = numerator;
            Denominator = denominator;
            Numerator.Parent = this;
            Denominator.Parent = this;
        }

        public override void Measure(double fontSize)
        {
            // Medir numerador y denominador con fuente más pequeña
            var innerFontSize = fontSize * 0.85;
            Numerator.Measure(innerFontSize);
            Denominator.Measure(innerFontSize);

            // Ancho = máximo de numerador y denominador + padding
            Width = Math.Max(Numerator.Width, Denominator.Width) + HorizontalPadding * 2;

            // Altura = numerador + línea + denominador + paddings
            Height = Numerator.Height + LineThickness + Denominator.Height + VerticalPadding * 4;

            // Baseline en el centro de la línea de fracción
            Baseline = Numerator.Height + VerticalPadding * 2 + LineThickness / 2;
        }

        public override void Render(Canvas canvas, double x, double y, double fontSize)
        {
            X = x;
            Y = y;

            var innerFontSize = fontSize * 0.85;

            // Recalcular posiciones
            var numX = x + (Width - Numerator.Width) / 2;
            var numY = y + VerticalPadding;

            var lineY = y + Numerator.Height + VerticalPadding * 2;

            var denX = x + (Width - Denominator.Width) / 2;
            var denY = lineY + LineThickness + VerticalPadding;

            // Renderizar numerador
            Numerator.Render(canvas, numX, numY, innerFontSize);

            // Dibujar línea de fracción
            var line = new Line
            {
                X1 = x + 2,
                Y1 = lineY,
                X2 = x + Width - 2,
                Y2 = lineY,
                Stroke = IsSelected ? Brushes.Blue : Brushes.Black,
                StrokeThickness = LineThickness
            };
            canvas.Children.Add(line);

            // Renderizar denominador
            Denominator.Render(canvas, denX, denY, innerFontSize);

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
            var num = Numerator.ToCalcpad();
            var den = Denominator.ToCalcpad();

            // Si son expresiones simples, no necesitan paréntesis
            if (IsSimple(num) && IsSimple(den))
                return $"{num}/{den}";

            return $"({num})/({den})";
        }

        private bool IsSimple(string expr)
        {
            // Una expresión simple no contiene operadores
            return !expr.Contains('+') && !expr.Contains('-') &&
                   !expr.Contains('*') && !expr.Contains('/');
        }

        public override MathElement HitTest(double x, double y)
        {
            // Primero verificar si está en numerador o denominador
            var numHit = Numerator.HitTest(x, y);
            if (numHit != null) return numHit;

            var denHit = Denominator.HitTest(x, y);
            if (denHit != null) return denHit;

            // Si no, verificar si está en la fracción misma
            return base.HitTest(x, y);
        }
    }
}
