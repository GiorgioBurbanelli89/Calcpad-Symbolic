using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Calcpad.Wpf.MathEditor
{
    /// <summary>
    /// Elemento de integral: ∫ f(x) dx con límites opcionales
    /// </summary>
    public class MathIntegral : MathElement
    {
        public MathElement Integrand { get; set; }      // f(x)
        public MathElement Variable { get; set; }        // dx
        public MathElement LowerLimit { get; set; }      // Límite inferior (opcional)
        public MathElement UpperLimit { get; set; }      // Límite superior (opcional)
        public bool HasLimits { get; set; }

        private const double SymbolWidth = 20.0;
        private const double Padding = 4.0;

        public MathIntegral(bool hasLimits = false)
        {
            HasLimits = hasLimits;
            Integrand = new MathText();
            Variable = new MathText("x");
            Integrand.Parent = this;
            Variable.Parent = this;

            if (hasLimits)
            {
                LowerLimit = new MathText();
                UpperLimit = new MathText();
                LowerLimit.Parent = this;
                UpperLimit.Parent = this;
            }
        }

        public override void Measure(double fontSize)
        {
            var innerFontSize = fontSize * 0.85;
            var limitFontSize = fontSize * 0.6;

            Integrand.Measure(innerFontSize);
            Variable.Measure(innerFontSize);

            double symbolHeight = fontSize * 1.5;
            double limitsWidth = 0;
            double limitsHeight = 0;

            if (HasLimits && LowerLimit != null && UpperLimit != null)
            {
                LowerLimit.Measure(limitFontSize);
                UpperLimit.Measure(limitFontSize);
                limitsWidth = Math.Max(LowerLimit.Width, UpperLimit.Width);
                limitsHeight = LowerLimit.Height + UpperLimit.Height;
            }

            // Ancho total: símbolo + límites + integrando + "d" + variable
            Width = SymbolWidth + limitsWidth + Padding + Integrand.Width + Padding + fontSize * 0.3 + Variable.Width + Padding;

            // Altura: máximo entre símbolo y contenido
            Height = Math.Max(symbolHeight + limitsHeight * 0.5, Integrand.Height);

            Baseline = Height * 0.6;
        }

        public override void Render(Canvas canvas, double x, double y, double fontSize)
        {
            X = x;
            Y = y;

            var innerFontSize = fontSize * 0.85;
            var limitFontSize = fontSize * 0.6;
            var symbolHeight = fontSize * 1.5;

            // Dibujar símbolo de integral ∫
            var integralSymbol = new TextBlock
            {
                Text = "∫",
                FontFamily = new FontFamily("Cambria Math"),
                FontSize = fontSize * 1.8,
                Foreground = IsSelected ? Brushes.Blue : Brushes.Black
            };
            Canvas.SetLeft(integralSymbol, x);
            Canvas.SetTop(integralSymbol, y + (Height - symbolHeight) / 2 - fontSize * 0.3);
            canvas.Children.Add(integralSymbol);

            double currentX = x + SymbolWidth;

            // Dibujar límites si existen
            if (HasLimits && LowerLimit != null && UpperLimit != null)
            {
                LowerLimit.Measure(limitFontSize);
                UpperLimit.Measure(limitFontSize);

                // Límite superior
                UpperLimit.Render(canvas, currentX, y, limitFontSize);

                // Límite inferior
                LowerLimit.Render(canvas, currentX, y + Height - LowerLimit.Height, limitFontSize);

                currentX += Math.Max(LowerLimit.Width, UpperLimit.Width) + Padding;
            }

            // Dibujar integrando
            Integrand.Render(canvas, currentX, y + (Height - Integrand.Height) / 2, innerFontSize);
            currentX += Integrand.Width + Padding;

            // Dibujar "d"
            var dSymbol = new TextBlock
            {
                Text = "d",
                FontFamily = new FontFamily("Cambria Math"),
                FontStyle = FontStyles.Italic,
                FontSize = innerFontSize,
                Foreground = IsSelected ? Brushes.Blue : Brushes.Black
            };
            Canvas.SetLeft(dSymbol, currentX);
            Canvas.SetTop(dSymbol, y + (Height - Integrand.Height) / 2);
            canvas.Children.Add(dSymbol);
            currentX += fontSize * 0.4;

            // Dibujar variable
            Variable.Render(canvas, currentX, y + (Height - Variable.Height) / 2, innerFontSize);

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
            var integrand = Integrand.ToCalcpad();
            var variable = Variable.ToCalcpad();

            if (HasLimits && LowerLimit != null && UpperLimit != null)
            {
                var lower = LowerLimit.ToCalcpad();
                var upper = UpperLimit.ToCalcpad();
                return $"$Integral{{{integrand} @ {variable} = {lower} : {upper}}}";
            }

            return $"$Integral{{{integrand} @ {variable}}}";
        }

        public override MathElement HitTest(double x, double y)
        {
            var hit = Integrand.HitTest(x, y);
            if (hit != null) return hit;

            hit = Variable.HitTest(x, y);
            if (hit != null) return hit;

            if (HasLimits)
            {
                if (LowerLimit != null)
                {
                    hit = LowerLimit.HitTest(x, y);
                    if (hit != null) return hit;
                }
                if (UpperLimit != null)
                {
                    hit = UpperLimit.HitTest(x, y);
                    if (hit != null) return hit;
                }
            }

            return base.HitTest(x, y);
        }
    }
}
