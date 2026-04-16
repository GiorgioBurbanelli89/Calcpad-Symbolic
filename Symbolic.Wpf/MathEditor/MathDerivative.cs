using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Calcpad.Wpf.MathEditor
{
    /// <summary>
    /// Elemento de derivada: df/dx o d²f/dx²
    /// </summary>
    public class MathDerivative : MathElement
    {
        public MathElement Function { get; set; }     // f
        public MathElement Variable { get; set; }      // x
        public int Order { get; set; } = 1;            // Orden de la derivada (1, 2, etc.)

        private const double LineThickness = 1.0;
        private const double Padding = 2.0;

        public MathDerivative(int order = 1)
        {
            Order = order;
            Function = new MathText("f");
            Variable = new MathText("x");
            Function.Parent = this;
            Variable.Parent = this;
        }

        public override void Measure(double fontSize)
        {
            var innerFontSize = fontSize * 0.85;
            Function.Measure(innerFontSize);
            Variable.Measure(innerFontSize);

            // Medir "d" o "d²"
            string dText = Order > 1 ? $"d{ToSuperscript(Order)}" : "d";
            var formattedD = new FormattedText(
                dText,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cambria Math"),
                innerFontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

            double dWidth = formattedD.Width;
            double numeratorWidth = dWidth + Function.Width;
            double denominatorWidth = dWidth + Variable.Width;

            Width = Math.Max(numeratorWidth, denominatorWidth) + Padding * 2;
            Height = Function.Height + LineThickness + Variable.Height + Padding * 4;
            Baseline = Function.Height + Padding * 2;
        }

        public override void Render(Canvas canvas, double x, double y, double fontSize)
        {
            X = x;
            Y = y;

            var innerFontSize = fontSize * 0.85;
            string dText = Order > 1 ? $"d{ToSuperscript(Order)}" : "d";

            // Numerador: df o d²f
            var numY = y + Padding;
            var dNum = new TextBlock
            {
                Text = dText,
                FontFamily = new FontFamily("Cambria Math"),
                FontStyle = FontStyles.Italic,
                FontSize = innerFontSize,
                Foreground = IsSelected ? Brushes.Blue : Brushes.Black
            };

            var formattedD = new FormattedText(
                dText,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Cambria Math"),
                innerFontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

            double dWidth = formattedD.Width;
            double numeratorWidth = dWidth + Function.Width;
            double denominatorWidth = dWidth + Variable.Width;
            double maxWidth = Math.Max(numeratorWidth, denominatorWidth);

            // Centrar numerador
            double numStartX = x + (Width - numeratorWidth) / 2;
            Canvas.SetLeft(dNum, numStartX);
            Canvas.SetTop(dNum, numY);
            canvas.Children.Add(dNum);

            Function.Render(canvas, numStartX + dWidth, numY, innerFontSize);

            // Línea de fracción
            var lineY = y + Function.Height + Padding * 2;
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

            // Denominador: dx o dx²
            var denY = lineY + LineThickness + Padding;
            double denStartX = x + (Width - denominatorWidth) / 2;

            var dDen = new TextBlock
            {
                Text = dText,
                FontFamily = new FontFamily("Cambria Math"),
                FontStyle = FontStyles.Italic,
                FontSize = innerFontSize,
                Foreground = IsSelected ? Brushes.Blue : Brushes.Black
            };
            Canvas.SetLeft(dDen, denStartX);
            Canvas.SetTop(dDen, denY);
            canvas.Children.Add(dDen);

            Variable.Render(canvas, denStartX + dWidth, denY, innerFontSize);

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

        private string ToSuperscript(int n)
        {
            var superscripts = new[] { '⁰', '¹', '²', '³', '⁴', '⁵', '⁶', '⁷', '⁸', '⁹' };
            if (n >= 0 && n <= 9)
                return superscripts[n].ToString();
            return n.ToString();
        }

        public override string ToCalcpad()
        {
            var func = Function.ToCalcpad();
            var variable = Variable.ToCalcpad();

            if (Order == 1)
                return $"$Derivative{{{func} @ {variable}}}";
            else
                return $"$Derivative{{{func} @ {variable} : {Order}}}";
        }

        public override MathElement HitTest(double x, double y)
        {
            var hit = Function.HitTest(x, y);
            if (hit != null) return hit;

            hit = Variable.HitTest(x, y);
            if (hit != null) return hit;

            return base.HitTest(x, y);
        }
    }
}
