using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MauiOcrPluginSample
{
    public partial class OcrResultPage : ContentPage
    {
        private string[] _originalLines;
        private bool _updatingText = false;
        private int _currentDecimalFormat = 0;

        public OcrResultPage(List<string> recognizedLines, byte[] imageBytes)
        {
            InitializeComponent();

            // Guardar líneas originales (sin formato)
            _originalLines = recognizedLines.ToArray();

            // Mostrar imagen y texto inicial
            capturedImage.Source = ImageSource.FromStream(() => new MemoryStream(imageBytes));
            UpdateEditorText();

#if ANDROID
            //Forzar orientación en android. Esto es ponerlo horizontal
            var activity = Platform.CurrentActivity;
            activity!.RequestedOrientation = Android.Content.PM.ScreenOrientation.Portrait;
#endif
        }

        private void UpdateEditorText()
        {
            _updatingText = true;

            var lines = new List<string>(_originalLines);

            // Aplicar formato decimal según selección
            for (int i = 0; i < lines.Count; i++)
            {
                string numericPart = ExtractNumericPart(lines[i]);

                if (!string.IsNullOrEmpty(numericPart))
                {
                    string formattedNumber = FormatNumber(numericPart);
                    lines[i] = lines[i].Replace(numericPart, formattedNumber);
                }
            }

            // Unir líneas manteniendo saltos para el editor
            ocrTextEditor.Text = string.Join(Environment.NewLine, lines);

            _updatingText = false;
        }

        private string ExtractNumericPart(string input)
        {
            // Extraer solo la parte numérica (incluyendo puntos y comas existentes)
            var match = Regex.Match(input, @"[\d.,]+");
            return match.Success ? match.Value : string.Empty;
        }

        private string FormatNumber(string numericString)
        {
            if (string.IsNullOrEmpty(numericString))
                return numericString;

            // Primero eliminar ceros iniciales no significativos (excepto si hay un decimal)
            string trimmedNumber = numericString.TrimStart('0');

            // Manejar caso cuando todos eran ceros (ej: "000" -> "0")
            if (string.IsNullOrEmpty(trimmedNumber))
            {
                trimmedNumber = "0";
            }
            // Manejar caso cuando el primer caracter es el punto/decimal (ej: ".25" -> "0.25")
            else if (trimmedNumber.StartsWith(".") || trimmedNumber.StartsWith(","))
            {
                trimmedNumber = "0" + trimmedNumber;
            }

            // Limpiar el número (quitar todos los puntos y comas para el procesamiento)
            string cleanNumber = trimmedNumber.Replace(".", "").Replace(",", "");

            switch (_currentDecimalFormat)
            {
                case 1: // 1 decimal
                    if (cleanNumber.Length >= 2)
                    {
                        return $"{cleanNumber.Substring(0, cleanNumber.Length - 1)}.{cleanNumber[^1]}";
                    }
                    else if (cleanNumber.Length == 1)
                    {
                        return $"0.{cleanNumber}";
                    }
                    break;

                case 2: // 2 decimales
                    if (cleanNumber.Length >= 3)
                    {
                        return $"{cleanNumber.Substring(0, cleanNumber.Length - 2)}.{cleanNumber.Substring(cleanNumber.Length - 2)}";
                    }
                    else if (cleanNumber.Length == 2)
                    {
                        return $"0.{cleanNumber}";
                    }
                    else if (cleanNumber.Length == 1)
                    {
                        return $"0.0{cleanNumber}";
                    }
                    break;
            }

            // Caso por defecto (sin decimales)
            return cleanNumber;
        }

        private void OnDecimalFormatChanged(object sender, EventArgs e)
        {
            if (!_updatingText && decimalFormatPicker.SelectedIndex >= 0)
            {
                _currentDecimalFormat = decimalFormatPicker.SelectedIndex;

                // Actualizar las líneas originales con el texto sin formato
                _originalLines = ocrTextEditor.Text.Split(
                    new[] { Environment.NewLine },
                    StringSplitOptions.None)
                    .Select(line => Regex.Replace(line, @"[^\d]", "")) // Quitar puntos/commas
                    .ToArray();

                UpdateEditorText();
            }
        }

        private void OnOcrTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_updatingText)
            {
                // Actualizar líneas originales cuando el usuario edita
                _originalLines = e.NewTextValue.Split(
                    new[] { Environment.NewLine },
                    StringSplitOptions.None)
                    .Select(line => Regex.Replace(line, @"[^\d]", "")) // Guardar solo dígitos
                    .ToArray();
            }
        }

        private async void OnCopyTextClicked(object sender, EventArgs e)
        {
            await Clipboard.Default.SetTextAsync(ocrTextEditor.Text);
            await DisplayAlert("Éxito", "Texto copiado al portapapeles", "OK");
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}