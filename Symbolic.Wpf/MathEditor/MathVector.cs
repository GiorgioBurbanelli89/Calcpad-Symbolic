using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Calcpad.Wpf.MathEditor
{
    /// <summary>
    /// Elemento de vector: [a; b; c] - vector columna
    /// </summary>
    public class MathVector : MathElement
    {
        public List<MathElement> Elements { get; set; }
        public int Length => Elements?.Count ?? 0;
        public bool IsColumn { get; set; } = true; // true = columna, false = fila

        private const double ElementPadding = 4.0;
        private const double BracketWidth = 6.0;

        public MathVector(int length = 3, bool isColumn = true)
        {
            IsColumn = isColumn;
            Elements = new List<MathElement>();
            for (int i = 0; i < length; i++)
            {
                var element = new MathText();
                element.Parent = this;
                Elements.Add(element);
            }
        }

        public MathElement GetElement(int index)
        {
            if (index >= 0 && index < Length)
                return Elements[index];
            return null;
        }

        public void SetElement(int index, MathElement element)
        {
            if (index >= 0 && index < Length)
            {
                element.Parent = this;
                Elements[index] = element;
            }
        }

        /// <summary>
        /// Elimina un elemento del vector por índice
        /// </summary>
        public bool RemoveElement(int index)
        {
            if (index >= 0 && index < Length && Length > 1)
            {
                Elements.RemoveAt(index);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Obtiene el índice de un elemento dentro del vector
        /// </summary>
        public int IndexOf(MathElement element)
        {
            return Elements.IndexOf(element);
        }

        public override void Measure(double fontSize)
        {
            var innerFontSize = fontSize * 0.85;

            double maxWidth = 0;
            double maxHeight = 0;
            double maxBaseline = 0;
            double totalSize = 0;

            foreach (var elem in Elements)
            {
                elem.Measure(innerFontSize);
                maxWidth = Math.Max(maxWidth, elem.Width);
                maxHeight = Math.Max(maxHeight, elem.Height);
                maxBaseline = Math.Max(maxBaseline, elem.Baseline);
                if (IsColumn)
                    totalSize += elem.Height + ElementPadding;
                else
                    totalSize += elem.Width + ElementPadding;
            }
            totalSize -= ElementPadding; // Quitar el último padding

            if (IsColumn)
            {
                Width = maxWidth + BracketWidth * 2 + ElementPadding * 2;
                Height = totalSize + ElementPadding * 2;
                Baseline = Height / 2; // Centrado vertical para vectores columna
            }
            else
            {
                Width = totalSize + BracketWidth * 2 + ElementPadding * 2;
                // Para vector fila: El baseline debe alinearse con texto normal del fontSize externo
                double normalTextBaseline = fontSize * 0.8;

                // Calcular Height para que los corchetes encierren el contenido correctamente
                // El contenido se posiciona en: y + Baseline - elem.Baseline
                // Si contentTop es negativo, necesitamos ajustar el Baseline
                double contentTop = normalTextBaseline - maxBaseline;
                if (contentTop < ElementPadding)
                {
                    // Ajustar Baseline para que el contenido tenga padding arriba
                    normalTextBaseline += (ElementPadding - contentTop);
                    contentTop = ElementPadding;
                }
                Baseline = normalTextBaseline;

                double contentBottom = contentTop + maxHeight;
                Height = contentBottom + ElementPadding;
            }
        }

        public override void Render(Canvas canvas, double x, double y, double fontSize)
        {
            X = x;
            Y = y;

            var innerFontSize = fontSize * 0.85;

            // NOTA: La flecha de vector va sobre el NOMBRE de la variable (ej: ⃗A),
            // no sobre el contenido [1; 2; 3]. Hekatan lo maneja al evaluar
            // y generar el HTML de salida.

            // Dibujar corchete izquierdo [
            DrawBracket(canvas, x, y, Height, true);

            // Dibujar elementos
            if (IsColumn)
            {
                double currentY = y + ElementPadding;
                foreach (var elem in Elements)
                {
                    double elemX = x + BracketWidth + (Width - BracketWidth * 2 - elem.Width) / 2;
                    elem.Render(canvas, elemX, currentY, innerFontSize);
                    currentY += elem.Height + ElementPadding;
                }
            }
            else
            {
                // Para vector fila: alinear el baseline del contenido con el baseline del vector
                double currentX = x + BracketWidth + ElementPadding;
                foreach (var elem in Elements)
                {
                    // Posicionar cada elemento para que su baseline coincida con el baseline del vector
                    double elemY = y + Baseline - elem.Baseline;
                    elem.Render(canvas, currentX, elemY, innerFontSize);
                    currentX += elem.Width + ElementPadding;
                }
            }

            // Dibujar corchete derecho ]
            DrawBracket(canvas, x + Width - BracketWidth, y, Height, false);

            // Fondo de selección visible (azul semitransparente)
            if (IsSelected)
            {
                var border = new Rectangle
                {
                    Width = Width,
                    Height = Height,
                    Stroke = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                    StrokeThickness = 2,
                    Fill = new SolidColorBrush(Color.FromArgb(100, 0, 120, 215)) // Azul semitransparente
                };
                Canvas.SetLeft(border, x);
                Canvas.SetTop(border, y);
                canvas.Children.Add(border);
            }
        }

        private void DrawBracket(Canvas canvas, double x, double y, double height, bool isLeft)
        {
            var path = new Path
            {
                Stroke = IsSelected ? Brushes.Blue : Brushes.Black,
                StrokeThickness = 1.5
            };

            var figure = new PathFigure();
            if (isLeft)
            {
                figure.StartPoint = new Point(x + BracketWidth - 2, y);
                figure.Segments.Add(new LineSegment(new Point(x + 2, y), true));
                figure.Segments.Add(new LineSegment(new Point(x + 2, y + height), true));
                figure.Segments.Add(new LineSegment(new Point(x + BracketWidth - 2, y + height), true));
            }
            else
            {
                figure.StartPoint = new Point(x + 2, y);
                figure.Segments.Add(new LineSegment(new Point(x + BracketWidth - 2, y), true));
                figure.Segments.Add(new LineSegment(new Point(x + BracketWidth - 2, y + height), true));
                figure.Segments.Add(new LineSegment(new Point(x + 2, y + height), true));
            }

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            path.Data = geometry;
            canvas.Children.Add(path);
        }

        private void DrawVectorArrow(Canvas canvas, double x, double y, double width)
        {
            // Dibujar flecha horizontal con punta hacia la derecha
            var arrowColor = new SolidColorBrush(Color.FromRgb(0x88, 0xAA, 0xFF)); // Color #8af como en CSS

            // Línea principal de la flecha
            var line = new Line
            {
                X1 = x + 4,
                Y1 = y,
                X2 = x + width - 4,
                Y2 = y,
                Stroke = arrowColor,
                StrokeThickness = 1.2
            };
            canvas.Children.Add(line);

            // Punta de la flecha (triángulo)
            var arrowHead = new Path
            {
                Stroke = arrowColor,
                StrokeThickness = 1.2,
                Fill = arrowColor
            };

            var figure = new PathFigure();
            figure.StartPoint = new Point(x + width - 8, y - 3);
            figure.Segments.Add(new LineSegment(new Point(x + width - 3, y), true));
            figure.Segments.Add(new LineSegment(new Point(x + width - 8, y + 3), true));
            figure.IsClosed = true;

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            arrowHead.Data = geometry;
            canvas.Children.Add(arrowHead);
        }

        public override string ToCalcpad()
        {
            var parts = new List<string>();
            foreach (var elem in Elements)
            {
                parts.Add(elem.ToCalcpad());
            }

            if (IsColumn)
                return "[" + string.Join("|", parts) + "]"; // Vector columna usa |
            else
                return "[" + string.Join("; ", parts) + "]"; // Vector fila usa ;
        }

        public override MathElement HitTest(double x, double y)
        {
            foreach (var elem in Elements)
            {
                var hit = elem.HitTest(x, y);
                if (hit != null) return hit;
            }
            return base.HitTest(x, y);
        }
    }
}
