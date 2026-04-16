using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Calcpad.Wpf.MathEditor
{
    /// <summary>
    /// Elemento para layout de múltiples columnas en MathEditor
    /// Permite mostrar varios parsers/elementos lado a lado
    /// Sintaxis: #columns N ... #column ... #column ... #end columns
    /// </summary>
    public class MathColumns : MathElement
    {
        /// <summary>
        /// Lista de columnas, cada una contiene una lista de elementos
        /// </summary>
        public List<List<MathElement>> Columns { get; set; } = new List<List<MathElement>>();

        /// <summary>
        /// Número de columnas definido
        /// </summary>
        public int ColumnCount { get; set; } = 2;

        /// <summary>
        /// Índice de la columna actualmente activa (donde está el cursor)
        /// </summary>
        public int ActiveColumnIndex { get; set; } = 0;

        /// <summary>
        /// Índice del elemento activo dentro de la columna activa
        /// </summary>
        public int ActiveElementIndex { get; set; } = 0;

        private const double ColumnGap = 15;      // Espacio entre columnas
        private const double Padding = 8;          // Padding interno
        private const double HeaderHeight = 20;    // Altura del header "#columns N"
        private const double MinColumnWidth = 100; // Ancho mínimo de columna

        // Anchos calculados de cada columna
        private double[] _columnWidths;
        private double[] _columnHeights;

        public MathColumns(int columnCount = 2)
        {
            ColumnCount = Math.Max(2, Math.Min(4, columnCount));

            // Inicializar columnas vacías
            for (int i = 0; i < ColumnCount; i++)
            {
                Columns.Add(new List<MathElement>());
            }
        }

        /// <summary>
        /// Agrega un elemento a una columna específica
        /// </summary>
        public void AddElementToColumn(int columnIndex, MathElement element)
        {
            if (columnIndex >= 0 && columnIndex < Columns.Count)
            {
                element.Parent = this;
                Columns[columnIndex].Add(element);
            }
        }

        /// <summary>
        /// Obtiene el elemento activo (donde está el cursor)
        /// </summary>
        public MathElement GetActiveElement()
        {
            if (ActiveColumnIndex >= 0 && ActiveColumnIndex < Columns.Count)
            {
                var column = Columns[ActiveColumnIndex];
                if (ActiveElementIndex >= 0 && ActiveElementIndex < column.Count)
                {
                    return column[ActiveElementIndex];
                }
            }
            return null;
        }

        public override void Measure(double fontSize)
        {
            _columnWidths = new double[ColumnCount];
            _columnHeights = new double[ColumnCount];

            // Medir cada columna
            for (int colIdx = 0; colIdx < Columns.Count; colIdx++)
            {
                double colWidth = MinColumnWidth;
                double colHeight = 0;

                foreach (var element in Columns[colIdx])
                {
                    element.Measure(fontSize);
                    colWidth = Math.Max(colWidth, element.Width);
                    colHeight += element.Height + 5; // 5px gap entre elementos
                }

                _columnWidths[colIdx] = colWidth;
                _columnHeights[colIdx] = colHeight;
            }

            // Calcular ancho total
            double totalWidth = Padding * 2;
            for (int i = 0; i < ColumnCount; i++)
            {
                totalWidth += _columnWidths[i];
                if (i < ColumnCount - 1)
                    totalWidth += ColumnGap;
            }

            // Calcular altura máxima
            double maxHeight = 0;
            for (int i = 0; i < ColumnCount; i++)
            {
                maxHeight = Math.Max(maxHeight, _columnHeights[i]);
            }

            Width = totalWidth;
            Height = HeaderHeight + maxHeight + Padding * 2;
            Baseline = Height * 0.5;
        }

        public override void Render(Canvas canvas, double x, double y, double fontSize)
        {
            X = x;
            Y = y;

            // Fondo del contenedor de columnas
            var background = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Width = Width,
                Height = Height
            };
            Canvas.SetLeft(background, x);
            Canvas.SetTop(background, y);
            canvas.Children.Add(background);

            // Header: "@{columns N}"
            var headerText = new TextBlock
            {
                Text = $"@{{columns {ColumnCount}}}",
                FontFamily = new FontFamily("Consolas"),
                FontSize = fontSize * 0.8,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC))
            };
            Canvas.SetLeft(headerText, x + Padding);
            Canvas.SetTop(headerText, y + 2);
            canvas.Children.Add(headerText);

            // Línea separadora del header
            var headerLine = new Line
            {
                X1 = x + 2,
                Y1 = y + HeaderHeight,
                X2 = x + Width - 2,
                Y2 = y + HeaderHeight,
                Stroke = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                StrokeThickness = 1
            };
            canvas.Children.Add(headerLine);

            // Renderizar cada columna
            double currentX = x + Padding;
            double contentY = y + HeaderHeight + Padding;

            for (int colIdx = 0; colIdx < Columns.Count; colIdx++)
            {
                // Fondo de la columna (sutil)
                var colBackground = new Rectangle
                {
                    Width = _columnWidths[colIdx],
                    Height = Height - HeaderHeight - Padding * 2,
                    Fill = colIdx == ActiveColumnIndex && IsCursorHere
                        ? new SolidColorBrush(Color.FromArgb(20, 0, 120, 215))
                        : new SolidColorBrush(Color.FromArgb(10, 128, 128, 128)),
                    RadiusX = 2,
                    RadiusY = 2
                };
                Canvas.SetLeft(colBackground, currentX);
                Canvas.SetTop(colBackground, contentY);
                canvas.Children.Add(colBackground);

                // Renderizar elementos de la columna
                double elementY = contentY;
                for (int elemIdx = 0; elemIdx < Columns[colIdx].Count; elemIdx++)
                {
                    var element = Columns[colIdx][elemIdx];

                    // Si este es el elemento activo, marcar el cursor
                    if (colIdx == ActiveColumnIndex && elemIdx == ActiveElementIndex && IsCursorHere)
                    {
                        element.IsCursorHere = true;
                    }
                    else
                    {
                        element.IsCursorHere = false;
                    }

                    element.Render(canvas, currentX, elementY, fontSize);
                    elementY += element.Height + 5;
                }

                // Separador vertical entre columnas
                if (colIdx < Columns.Count - 1)
                {
                    var separator = new Line
                    {
                        X1 = currentX + _columnWidths[colIdx] + ColumnGap / 2,
                        Y1 = contentY,
                        X2 = currentX + _columnWidths[colIdx] + ColumnGap / 2,
                        Y2 = y + Height - Padding,
                        Stroke = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)),
                        StrokeThickness = 1,
                        StrokeDashArray = new DoubleCollection { 3, 3 }
                    };
                    canvas.Children.Add(separator);
                }

                currentX += _columnWidths[colIdx] + ColumnGap;
            }

            // Highlight si está seleccionado
            if (IsSelected)
            {
                var selectionRect = new Rectangle
                {
                    Width = Width,
                    Height = Height,
                    Stroke = Brushes.Blue,
                    StrokeThickness = 2,
                    Fill = new SolidColorBrush(Color.FromArgb(30, 0, 120, 215))
                };
                Canvas.SetLeft(selectionRect, x);
                Canvas.SetTop(selectionRect, y);
                canvas.Children.Add(selectionRect);
            }
        }

        public override string ToCalcpad()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"@{{columns {ColumnCount}}}");

            for (int colIdx = 0; colIdx < Columns.Count; colIdx++)
            {
                foreach (var element in Columns[colIdx])
                {
                    sb.AppendLine(element.ToCalcpad());
                }

                if (colIdx < Columns.Count - 1)
                {
                    sb.AppendLine("@{column}");
                }
            }

            sb.Append("@{end columns}");
            return sb.ToString();
        }

        public override MathElement HitTest(double x, double y)
        {
            // Verificar si está dentro del contenedor
            if (x >= X && x <= X + Width && y >= Y && y <= Y + Height)
            {
                // Determinar en qué columna se hizo click
                double currentX = X + Padding;
                double contentY = Y + HeaderHeight + Padding;

                for (int colIdx = 0; colIdx < Columns.Count; colIdx++)
                {
                    double colRight = currentX + _columnWidths[colIdx];

                    if (x >= currentX && x <= colRight && y >= contentY)
                    {
                        // Buscar en los elementos de esta columna
                        // IMPORTANTE: Calcular Y de cada elemento igual que en Render
                        double elementY = contentY;
                        for (int elemIdx = 0; elemIdx < Columns[colIdx].Count; elemIdx++)
                        {
                            var element = Columns[colIdx][elemIdx];

                            // Actualizar X, Y del elemento para hit testing correcto
                            // (igual que en Render)
                            element.X = currentX;
                            element.Y = elementY;

                            var hit = element.HitTest(x, y);
                            if (hit != null)
                            {
                                ActiveColumnIndex = colIdx;
                                ActiveElementIndex = elemIdx;
                                return hit;
                            }

                            // Avanzar Y para el siguiente elemento (igual que en Render: element.Height + 5)
                            elementY += element.Height + 5;
                        }

                        // Click en la columna pero no en un elemento específico
                        ActiveColumnIndex = colIdx;
                        ActiveElementIndex = Columns[colIdx].Count > 0 ? 0 : -1;
                        return this;
                    }

                    currentX += _columnWidths[colIdx] + ColumnGap;
                }

                return this;
            }

            return null;
        }

        /// <summary>
        /// Navega a la columna anterior
        /// </summary>
        public bool MoveToPreviousColumn()
        {
            if (ActiveColumnIndex > 0)
            {
                ActiveColumnIndex--;
                ActiveElementIndex = Math.Min(ActiveElementIndex, Columns[ActiveColumnIndex].Count - 1);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Navega a la columna siguiente
        /// </summary>
        public bool MoveToNextColumn()
        {
            if (ActiveColumnIndex < Columns.Count - 1)
            {
                ActiveColumnIndex++;
                ActiveElementIndex = Math.Min(ActiveElementIndex, Columns[ActiveColumnIndex].Count - 1);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Navega al elemento anterior en la columna actual
        /// </summary>
        public bool MoveToPreviousElement()
        {
            if (ActiveElementIndex > 0)
            {
                ActiveElementIndex--;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Navega al elemento siguiente en la columna actual
        /// </summary>
        public bool MoveToNextElement()
        {
            var column = Columns[ActiveColumnIndex];
            if (ActiveElementIndex < column.Count - 1)
            {
                ActiveElementIndex++;
                return true;
            }
            return false;
        }
    }
}
