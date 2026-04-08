using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Calcpad.Wpf.MathEditor
{
    /// <summary>
    /// Elemento de raíz: √x o ⁿ√x
    /// </summary>
    public class MathRoot : MathElement
    {
        public MathElement Radicand { get; set; }  // Lo que está bajo la raíz
        public MathElement Index { get; set; }      // Índice de la raíz (null para raíz cuadrada)

        private const double RootSymbolWidth = 12.0;
        private const double TopLineExtend = 4.0;
        private const double VerticalPadding = 2.0;

        public MathRoot()
        {
            Radicand = new MathText();
            Radicand.Parent = this;
        }

        public MathRoot(MathElement radicand, MathElement index = null)
        {
            Radicand = radicand;
            Index = index;
            Radicand.Parent = this;
            if (Index != null) Index.Parent = this;
        }

        public override void Measure(double fontSize)
        {
            Radicand.Measure(fontSize);

            double indexWidth = 0;
            double indexHeight = 0;
            if (Index != null)
            {
                Index.Measure(fontSize * 0.6);
                indexWidth = Index.Width;
                indexHeight = Index.Height;
            }

            // Ancho = símbolo raíz + contenido + extensión
            Width = RootSymbolWidth + Radicand.Width + TopLineExtend + indexWidth * 0.5;

            // Altura = contenido + padding + espacio para línea superior
            Height = Radicand.Height + VerticalPadding * 2 + 4;

            // Baseline alineada con el contenido
            Baseline = VerticalPadding + 4 + Radicand.Baseline;
        }

        public override void Render(Canvas canvas, double x, double y, double fontSize)
        {
            X = x;
            Y = y;

            double indexWidth = 0;
            if (Index != null)
            {
                Index.Measure(fontSize * 0.6);
                indexWidth = Index.Width * 0.5;
            }

            var symbolX = x + indexWidth;
            var contentX = symbolX + RootSymbolWidth;
            var contentY = y + VerticalPadding + 4;

            // Dibujar símbolo de raíz √
            var rootPath = new Path
            {
                Stroke = IsSelected ? Brushes.Blue : Brushes.Black,
                StrokeThickness = 1.5,
                Data = CreateRootSymbol(symbolX, y, Radicand.Height + VerticalPadding * 2 + 4, Radicand.Width + TopLineExtend)
            };
            canvas.Children.Add(rootPath);

            // Dibujar índice si existe
            if (Index != null)
            {
                Index.Render(canvas, x, y, fontSize * 0.6);
            }

            // Renderizar contenido bajo la raíz
            Radicand.Render(canvas, contentX, contentY, fontSize);

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

        private Geometry CreateRootSymbol(double x, double y, double height, double contentWidth)
        {
            // Crear forma del símbolo de raíz √
            var startX = x;
            var startY = y + height * 0.6;

            var figure = new PathFigure { StartPoint = new Point(startX, startY) };

            // Pequeña línea diagonal hacia abajo-derecha
            figure.Segments.Add(new LineSegment(new Point(startX + 3, startY + 2), true));

            // Línea diagonal hacia abajo (el "gancho" de la raíz)
            figure.Segments.Add(new LineSegment(new Point(startX + 6, y + height - 2), true));

            // Línea diagonal hacia arriba (subida del símbolo)
            figure.Segments.Add(new LineSegment(new Point(startX + RootSymbolWidth, y + 2), true));

            // Línea horizontal superior
            figure.Segments.Add(new LineSegment(new Point(startX + RootSymbolWidth + contentWidth, y + 2), true));

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return geometry;
        }

        public override string ToCalcpad()
        {
            var content = Radicand.ToCalcpad();

            if (Index == null)
            {
                // Raíz cuadrada
                return $"sqrt({content})";
            }
            else
            {
                // Raíz n-ésima
                var n = Index.ToCalcpad();
                return $"root({content};{n})";
            }
        }

        public override MathElement HitTest(double x, double y)
        {
            var radicandHit = Radicand.HitTest(x, y);
            if (radicandHit != null) return radicandHit;

            if (Index != null)
            {
                var indexHit = Index.HitTest(x, y);
                if (indexHit != null) return indexHit;
            }

            return base.HitTest(x, y);
        }
    }
}
