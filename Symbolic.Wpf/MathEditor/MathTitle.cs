using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Calcpad.Wpf.MathEditor
{
    /// <summary>
    /// Elemento de titulo (texto con comillas dobles)
    /// Soporta tags HTML: h1-h6, strong/b, em/i, u
    /// Tamanios segun template.html: h1=2.1em, h2=1.7em, h3=1.4em, h4=1.2em, h5=1.1em, h6=1.0em
    /// </summary>
    public class MathTitle : MathElement
    {
        public string Text { get; set; } = "";
        public string RawText { get; set; } = ""; // Texto original con tags
        public int CursorPosition { get; set; } = 0;

        // Fuente para titulos segun template.html
        private static readonly FontFamily TitleFont = new FontFamily("Arial Nova, Helvetica, sans-serif");
        private static readonly Typeface TitleTypeface = new Typeface(TitleFont, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        private static readonly Typeface TitleTypefaceBold = new Typeface(TitleFont, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Typeface TitleTypefaceItalic = new Typeface(TitleFont, FontStyles.Italic, FontWeights.Normal, FontStretches.Normal);
        private static readonly Typeface TitleTypefaceBoldItalic = new Typeface(TitleFont, FontStyles.Italic, FontWeights.Bold, FontStretches.Normal);

        // Tamanios segun template.html (lineas 27-32)
        private static readonly Dictionary<string, double> HeadingSizeRatios = new Dictionary<string, double>
        {
            { "h1", 2.1 },
            { "h2", 1.7 },
            { "h3", 1.4 },
            { "h4", 1.2 },
            { "h5", 1.1 },
            { "h6", 1.0 }
        };

        // Formato detectado del HTML
        // Por defecto: H3 (1.4em, negrita) como en Hekatan output
        private double _sizeRatio = 1.4;
        private bool _isBold = true;
        private bool _isItalic = false;
        private bool _isUnderline = false;
        private string _headingTag = "h3";

        public MathTitle() { }
        public MathTitle(string text)
        {
            RawText = text;
            // Solo parsear si tiene tags HTML, sino usar defaults (H3)
            if (text.Contains("<") && text.Contains(">"))
            {
                ParseHtmlTags(text);
            }
            else
            {
                Text = text;
            }
        }

        /// <summary>
        /// Parsea el texto para detectar tags HTML y extraer el contenido
        /// </summary>
        private void ParseHtmlTags(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                Text = "";
                _sizeRatio = 1.0;
                _isBold = false;
                _isItalic = false;
                _isUnderline = false;
                _headingTag = "";
                return;
            }

            string text = input;
            _sizeRatio = 1.0;
            _isBold = false;
            _isItalic = false;
            _isUnderline = false;
            _headingTag = "";

            // Detectar heading tags (h1-h6)
            var headingMatch = Regex.Match(text, @"<(h[1-6])(?:\s[^>]*)?>(.+?)</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (headingMatch.Success)
            {
                _headingTag = headingMatch.Groups[1].Value.ToLower();
                if (HeadingSizeRatios.TryGetValue(_headingTag, out double ratio))
                {
                    _sizeRatio = ratio;
                }
                text = headingMatch.Groups[2].Value;
            }

            // Detectar bold (strong o b)
            if (Regex.IsMatch(text, @"<(strong|b)(?:\s[^>]*)?>", RegexOptions.IgnoreCase))
            {
                _isBold = true;
                text = Regex.Replace(text, @"<(strong|b)(?:\s[^>]*)?>|</(strong|b)>", "", RegexOptions.IgnoreCase);
            }

            // Detectar italic (em o i)
            if (Regex.IsMatch(text, @"<(em|i)(?:\s[^>]*)?>", RegexOptions.IgnoreCase))
            {
                _isItalic = true;
                text = Regex.Replace(text, @"<(em|i)(?:\s[^>]*)?>|</(em|i)>", "", RegexOptions.IgnoreCase);
            }

            // Detectar underline (u)
            if (Regex.IsMatch(text, @"<u(?:\s[^>]*)?>", RegexOptions.IgnoreCase))
            {
                _isUnderline = true;
                text = Regex.Replace(text, @"<u(?:\s[^>]*)?>|</u>", "", RegexOptions.IgnoreCase);
            }

            // Eliminar otros tags HTML (p, span, div, br, etc.)
            text = StripHtmlTags(text);

            // Decodificar entidades HTML
            text = DecodeHtmlEntities(text);

            Text = text.Trim();
        }

        /// <summary>
        /// Elimina todos los tags HTML restantes
        /// </summary>
        private string StripHtmlTags(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            // Eliminar tags HTML
            return Regex.Replace(input, @"<[^>]+>", "");
        }

        /// <summary>
        /// Decodifica entidades HTML comunes
        /// </summary>
        private string DecodeHtmlEntities(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            return input
                .Replace("&nbsp;", " ")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&amp;", "&")
                .Replace("&quot;", "\"")
                .Replace("&apos;", "'")
                .Replace("&#39;", "'")
                .Replace("&ndash;", "–")
                .Replace("&mdash;", "—")
                .Replace("&deg;", "°")
                .Replace("&plusmn;", "±")
                .Replace("&times;", "×")
                .Replace("&divide;", "÷");
        }

        private Typeface GetTypeface()
        {
            if (_isBold && _isItalic)
                return TitleTypefaceBoldItalic;
            if (_isBold)
                return TitleTypefaceBold;
            if (_isItalic)
                return TitleTypefaceItalic;
            return TitleTypeface;
        }

        public override void Measure(double fontSize)
        {
            var titleFontSize = fontSize * _sizeRatio;

            if (string.IsNullOrEmpty(Text))
            {
                Width = titleFontSize * 0.5;  // Ancho minimo para cursor
                Height = titleFontSize;
                Baseline = titleFontSize * 0.8;
                return;
            }

            var formattedText = new FormattedText(
                Text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                GetTypeface(),
                titleFontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

            Width = formattedText.Width;
            Height = formattedText.Height;
            Baseline = formattedText.Baseline;
        }

        public override void Render(Canvas canvas, double x, double y, double fontSize)
        {
            X = x;
            Y = y;
            var titleFontSize = fontSize * _sizeRatio;

            if (!string.IsNullOrEmpty(Text))
            {
                var textBlock = new TextBlock
                {
                    Text = Text,
                    FontFamily = TitleFont,
                    FontSize = titleFontSize,
                    FontWeight = _isBold ? FontWeights.Bold : FontWeights.Normal,
                    FontStyle = _isItalic ? FontStyles.Italic : FontStyles.Normal,
                    Foreground = Brushes.Black
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
                var cursorX = x + GetCursorOffset(titleFontSize);
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
            if (CursorPosition == 0 || string.IsNullOrEmpty(Text))
                return 0;

            var textBefore = Text.Substring(0, Math.Min(CursorPosition, Text.Length));
            var formattedText = new FormattedText(
                textBefore,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                GetTypeface(),
                fontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

            return formattedText.Width;
        }

        public override string ToCalcpad()
        {
            // En Hekatan, el titulo es "texto (sin cerrar)
            return "\"" + Text;
        }

        public void InsertChar(char c)
        {
            Text = Text.Insert(CursorPosition, c.ToString());
            CursorPosition++;
            RawText = Text;
        }

        public void DeleteChar()
        {
            if (CursorPosition > 0 && Text.Length > 0)
            {
                Text = Text.Remove(CursorPosition - 1, 1);
                CursorPosition--;
                RawText = Text;
            }
        }

        public void SetText(string text)
        {
            RawText = text;
            ParseHtmlTags(text);
        }
    }
}