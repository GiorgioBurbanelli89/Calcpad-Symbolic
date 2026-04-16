using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Calcpad.Wpf.MathEditor
{
    public class MathComment : MathElement
    {
        private string _text = "";
        private string _displayText = "";
        private bool _isBold = false;
        private bool _isItalic = false;
        private bool _isUnderline = false;
        private int _headingLevel = 0; // 0 = normal, 1-6 = h1-h6
        private bool _isParagraph = false;

        public string Text
        {
            get => _text;
            set
            {
                _text = value ?? "";
                ParseHtmlTags(_text);
            }
        }

        public bool IsClosed { get; set; } = false;
        public string DisplayText => _displayText;
        public int CursorPosition { get; set; } = 0;

        private static readonly FontFamily BodyFont = new FontFamily("Segoe UI, Arial Nova, Helvetica, sans-serif");
        private static readonly FontFamily HeadingFont = new FontFamily("Segoe UI, Arial Nova, Helvetica, sans-serif");
        
        private static readonly Typeface BodyTypeface = new Typeface(BodyFont, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        private static readonly Typeface BodyTypefaceBold = new Typeface(BodyFont, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Typeface BodyTypefaceItalic = new Typeface(BodyFont, FontStyles.Italic, FontWeights.Normal, FontStretches.Normal);
        private static readonly Typeface BodyTypefaceBoldItalic = new Typeface(BodyFont, FontStyles.Italic, FontWeights.Bold, FontStretches.Normal);
        
        // Typefaces para headings (siempre bold)
        private static readonly Typeface HeadingTypeface = new Typeface(HeadingFont, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Typeface HeadingTypefaceItalic = new Typeface(HeadingFont, FontStyles.Italic, FontWeights.Bold, FontStretches.Normal);

        public MathComment() { }
        public MathComment(string text)
        {
            _text = text ?? "";
            ParseHtmlTags(_text);
        }

        private void ParseHtmlTags(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                _displayText = "";
                _isBold = false;
                _isItalic = false;
                _isUnderline = false;
                _headingLevel = 0;
                _isParagraph = false;
                return;
            }

            string text = input;
            _isBold = false;
            _isItalic = false;
            _isUnderline = false;
            _headingLevel = 0;
            _isParagraph = false;

            // Detectar headings h1-h6
            var headingMatch = Regex.Match(text, @"<h([1-6])(?:\s[^>]*)?>", RegexOptions.IgnoreCase);
            if (headingMatch.Success)
            {
                _headingLevel = int.Parse(headingMatch.Groups[1].Value);
                _isBold = true; // Headings son bold por defecto
                text = Regex.Replace(text, @"<h[1-6](?:\s[^>]*)?>|</h[1-6]>", "", RegexOptions.IgnoreCase);
            }

            // Detectar párrafos
            if (Regex.IsMatch(text, @"<p(?:\s[^>]*)?>", RegexOptions.IgnoreCase))
            {
                _isParagraph = true;
                text = Regex.Replace(text, @"<p(?:\s[^>]*)?>|</p>", "", RegexOptions.IgnoreCase);
            }

            // Detectar bold
            if (Regex.IsMatch(text, @"<(strong|b)(?:\s[^>]*)?>", RegexOptions.IgnoreCase))
            {
                _isBold = true;
                text = Regex.Replace(text, @"<(strong|b)(?:\s[^>]*)?>|</(strong|b)>", "", RegexOptions.IgnoreCase);
            }

            // Detectar italic
            if (Regex.IsMatch(text, @"<(em|i)(?:\s[^>]*)?>", RegexOptions.IgnoreCase))
            {
                _isItalic = true;
                text = Regex.Replace(text, @"<(em|i)(?:\s[^>]*)?>|</(em|i)>", "", RegexOptions.IgnoreCase);
            }

            // Detectar underline
            if (Regex.IsMatch(text, @"<u(?:\s[^>]*)?>", RegexOptions.IgnoreCase))
            {
                _isUnderline = true;
                text = Regex.Replace(text, @"<u(?:\s[^>]*)?>|</u>", "", RegexOptions.IgnoreCase);
            }

            // Convertir <br> a espacios (o mantenerlos para multi-línea)
            text = Regex.Replace(text, @"<br\s*/?>", " ", RegexOptions.IgnoreCase);

            // Limpiar otros tags HTML restantes
            text = Regex.Replace(text, @"<[^>]+>", "");
            _displayText = WebUtility.HtmlDecode(text).Trim();
        }

        private double GetFontSizeMultiplier()
        {
            // Multiplicadores de tamaño según el nivel de heading
            // Similar a los estilos CSS típicos de browsers
            switch (_headingLevel)
            {
                case 1: return 2.0;    // h1 = 2em
                case 2: return 1.5;    // h2 = 1.5em
                case 3: return 1.17;   // h3 = 1.17em
                case 4: return 1.0;    // h4 = 1em (bold)
                case 5: return 0.83;   // h5 = 0.83em
                case 6: return 0.67;   // h6 = 0.67em
                default: return 1.0;
            }
        }

        private Brush GetForegroundBrush()
        {
            // Los headings pueden tener un color diferente (más oscuro/enfatizado)
            if (_headingLevel > 0 && _headingLevel <= 3)
            {
                return new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)); // Gris oscuro como Output
            }
            return Brushes.Black;
        }

        private Typeface GetTypeface()
        {
            if (_headingLevel > 0)
            {
                // Headings usan tipografía especial (bold)
                return _isItalic ? HeadingTypefaceItalic : HeadingTypeface;
            }
            
            if (_isBold && _isItalic) return BodyTypefaceBoldItalic;
            if (_isBold) return BodyTypefaceBold;
            if (_isItalic) return BodyTypefaceItalic;
            return BodyTypeface;
        }

        public override void Measure(double fontSize)
        {
            var displayText = DisplayText;
            double actualFontSize = fontSize * GetFontSizeMultiplier();
            
            if (string.IsNullOrEmpty(displayText))
            {
                Width = actualFontSize * 0.5;
                Height = actualFontSize;
                Baseline = actualFontSize * 0.8;
                return;
            }

            var formattedText = new FormattedText(
                displayText,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                GetTypeface(),
                actualFontSize,
                GetForegroundBrush(),
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

            Width = formattedText.Width;
            Height = formattedText.Height;
            Baseline = formattedText.Baseline;
            
            // Agregar espacio extra antes/después de headings y párrafos
            if (_headingLevel > 0)
            {
                Height += actualFontSize * 0.5; // Margen inferior para headings
            }
            if (_isParagraph)
            {
                Height += fontSize * 0.3; // Margen inferior para párrafos
            }
        }

        public override void Render(Canvas canvas, double x, double y, double fontSize)
        {
            X = x;
            Y = y;
            var displayText = DisplayText;
            double actualFontSize = fontSize * GetFontSizeMultiplier();

            if (!string.IsNullOrEmpty(displayText))
            {
                var textBlock = new TextBlock
                {
                    Text = displayText,
                    FontFamily = _headingLevel > 0 ? HeadingFont : BodyFont,
                    FontSize = actualFontSize,
                    FontWeight = (_isBold || _headingLevel > 0) ? FontWeights.Bold : FontWeights.Normal,
                    FontStyle = _isItalic ? FontStyles.Italic : FontStyles.Normal,
                    Foreground = GetForegroundBrush()
                };

                if (_isUnderline)
                {
                    textBlock.TextDecorations = TextDecorations.Underline;
                }

                Canvas.SetLeft(textBlock, x);
                Canvas.SetTop(textBlock, y);
                canvas.Children.Add(textBlock);
            }

            if (IsCursorHere)
            {
                var cursorX = x + GetCursorOffset(fontSize);
                var line = new System.Windows.Shapes.Line
                {
                    X1 = cursorX,
                    Y1 = y + 2,
                    X2 = cursorX,
                    Y2 = y + Height - 2,
                    Stroke = MathStyles.CursorColor,
                    StrokeThickness = 1.5
                };
                canvas.Children.Add(line);
            }
        }


        private double GetCursorOffset(double fontSize)
        {
            if (CursorPosition == 0 || string.IsNullOrEmpty(_text))
                return 0;

            double actualFontSize = fontSize * GetFontSizeMultiplier();
            var textBeforeCursor = _text.Substring(0, Math.Min(CursorPosition, _text.Length));
            var cleanText = Regex.Replace(textBeforeCursor, @"<[^>]+>", "");
            var displayTextBefore = WebUtility.HtmlDecode(cleanText);

            var formattedText = new FormattedText(
                displayTextBefore,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                GetTypeface(),
                actualFontSize,
                GetForegroundBrush(),
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

            return formattedText.Width;
        }

        public override string ToCalcpad()
        {
            if (IsClosed)
                return "'" + _text + "'";
            else
                return "'" + _text;
        }

        public void InsertChar(char c)
        {
            _text = _text.Insert(CursorPosition, c.ToString());
            CursorPosition++;
            ParseHtmlTags(_text);
        }

        public void DeleteChar()
        {
            if (CursorPosition > 0 && _text.Length > 0)
            {
                _text = _text.Remove(CursorPosition - 1, 1);
                CursorPosition--;
                ParseHtmlTags(_text);
            }
        }
    }
}
