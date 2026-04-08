using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Calcpad.Wpf.MathEditor
{
    /// <summary>
    /// Elemento de matriz: [a, b; c, d]
    /// </summary>
    public class MathMatrix : MathElement
    {
        public List<List<MathElement>> Cells { get; set; }
        public int Rows => Cells?.Count ?? 0;
        public int Cols => Cells?.Count > 0 ? Cells[0].Count : 0;

        private const double CellPadding = 6.0;
        private const double BracketWidth = 8.0;
        private double[] _colWidths;
        private double[] _rowHeights;

        public MathMatrix(int rows = 2, int cols = 2)
        {
            Cells = new List<List<MathElement>>();
            for (int i = 0; i < rows; i++)
            {
                var row = new List<MathElement>();
                for (int j = 0; j < cols; j++)
                {
                    var cell = new MathText();
                    cell.Parent = this;
                    row.Add(cell);
                }
                Cells.Add(row);
            }
        }

        public MathElement GetCell(int row, int col)
        {
            if (row >= 0 && row < Rows && col >= 0 && col < Cols)
                return Cells[row][col];
            return null;
        }

        public void SetCell(int row, int col, MathElement element)
        {
            if (row >= 0 && row < Rows && col >= 0 && col < Cols)
            {
                element.Parent = this;
                Cells[row][col] = element;
            }
        }

        /// <summary>
        /// Agrega una nueva fila en la posición especificada
        /// </summary>
        public void AddRow(int atIndex)
        {
            if (atIndex < 0) atIndex = 0;
            if (atIndex > Rows) atIndex = Rows;

            var newRow = new List<MathElement>();
            for (int j = 0; j < Cols; j++)
            {
                var cell = new MathText();
                cell.Parent = this;
                newRow.Add(cell);
            }
            Cells.Insert(atIndex, newRow);
        }

        /// <summary>
        /// Agrega una nueva columna en la posición especificada
        /// </summary>
        public void AddColumn(int atIndex)
        {
            if (atIndex < 0) atIndex = 0;
            if (atIndex > Cols) atIndex = Cols;

            for (int i = 0; i < Rows; i++)
            {
                var cell = new MathText();
                cell.Parent = this;
                Cells[i].Insert(atIndex, cell);
            }
        }

        /// <summary>
        /// Elimina una fila en la posición especificada
        /// </summary>
        public bool RemoveRow(int atIndex)
        {
            if (atIndex >= 0 && atIndex < Rows && Rows > 1)
            {
                Cells.RemoveAt(atIndex);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Elimina una columna en la posición especificada
        /// </summary>
        public bool RemoveColumn(int atIndex)
        {
            if (atIndex >= 0 && atIndex < Cols && Cols > 1)
            {
                for (int i = 0; i < Rows; i++)
                {
                    Cells[i].RemoveAt(atIndex);
                }
                return true;
            }
            return false;
        }

        public override void Measure(double fontSize)
        {
            var innerFontSize = fontSize * 0.85;

            _colWidths = new double[Cols];
            _rowHeights = new double[Rows];

            // Medir cada celda
            for (int i = 0; i < Rows; i++)
            {
                for (int j = 0; j < Cols; j++)
                {
                    var cell = Cells[i][j];
                    cell.Measure(innerFontSize);
                    _colWidths[j] = Math.Max(_colWidths[j], cell.Width);
                    _rowHeights[i] = Math.Max(_rowHeights[i], cell.Height);
                }
            }

            // Calcular dimensiones totales
            double totalWidth = BracketWidth * 2;
            foreach (var w in _colWidths)
                totalWidth += w + CellPadding;
            totalWidth -= CellPadding; // Quitar el último padding

            double totalHeight = 0;
            foreach (var h in _rowHeights)
                totalHeight += h + CellPadding;
            totalHeight -= CellPadding; // Quitar el último padding
            totalHeight += CellPadding * 2; // Padding vertical de corchetes

            Width = totalWidth;
            Height = totalHeight;
            Baseline = Height / 2;
        }

        public override void Render(Canvas canvas, double x, double y, double fontSize)
        {
            X = x;
            Y = y;

            var innerFontSize = fontSize * 0.85;

            // Dibujar corchete izquierdo [
            DrawBracket(canvas, x, y, Height, true);

            // Dibujar celdas
            double currentY = y + CellPadding;
            for (int i = 0; i < Rows; i++)
            {
                double currentX = x + BracketWidth;
                for (int j = 0; j < Cols; j++)
                {
                    var cell = Cells[i][j];
                    // Centrar en la celda
                    double cellX = currentX + (_colWidths[j] - cell.Width) / 2;
                    double cellY = currentY + (_rowHeights[i] - cell.Height) / 2;
                    cell.Render(canvas, cellX, cellY, innerFontSize);
                    currentX += _colWidths[j] + CellPadding;
                }
                currentY += _rowHeights[i] + CellPadding;
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
                // Corchete izquierdo [
                figure.StartPoint = new Point(x + BracketWidth - 2, y);
                figure.Segments.Add(new LineSegment(new Point(x + 2, y), true));
                figure.Segments.Add(new LineSegment(new Point(x + 2, y + height), true));
                figure.Segments.Add(new LineSegment(new Point(x + BracketWidth - 2, y + height), true));
            }
            else
            {
                // Corchete derecho ]
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

        public override string ToCalcpad()
        {
            var parts = new List<string>();
            for (int i = 0; i < Rows; i++)
            {
                var rowParts = new List<string>();
                for (int j = 0; j < Cols; j++)
                {
                    rowParts.Add(Cells[i][j].ToCalcpad());
                }
                parts.Add(string.Join("; ", rowParts));
            }
            return "[" + string.Join("|", parts) + "]";
        }

        public override MathElement HitTest(double x, double y)
        {
            foreach (var row in Cells)
            {
                foreach (var cell in row)
                {
                    var hit = cell.HitTest(x, y);
                    if (hit != null) return hit;
                }
            }
            return base.HitTest(x, y);
        }
    }
}
