using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace MauiOcrPluginSample
{
    public partial class OcrResultPage : ContentPage
    {
        private string[] _originalLines;
        private bool _updatingText = false;
        private bool _hasDecimalApplied = false;

        public OcrResultPage(List<string> recognizedLines, byte[] imageBytes)
        {
            InitializeComponent();

            // Guardar líneas originales
            _originalLines = recognizedLines.ToArray();

            // Mostrar imagen y texto inicial
            capturedImage.Source = ImageSource.FromStream(() => new MemoryStream(imageBytes));
            UpdateEditorText();

#if ANDROID
            var activity = Platform.CurrentActivity;
            activity!.RequestedOrientation = Android.Content.PM.ScreenOrientation.Portrait;
#endif
        }

        private void UpdateEditorText()
        {
            _updatingText = true;

            var lines = new List<string>(_originalLines);

            if (decimalCheckBox.IsChecked)
            {
                // Aplicar formato decimal al último número si está activo
                if (lines.Count > 0)
                {
                    string lastLine = lines[lines.Count - 1];

                    // Solo procesar si es un número sin punto decimal
                    if (decimal.TryParse(lastLine, out decimal number) && !lastLine.Contains('.'))
                    {
                        // Insertar punto antes del último dígito
                        if (lastLine.Length > 1)
                        {
                            lines[lines.Count - 1] = lastLine.Insert(lastLine.Length - 1, ".");
                            _hasDecimalApplied = true;
                        }
                    }
                }
            }
            else if (_hasDecimalApplied)
            {
                // Quitar formato decimal del último número si estaba aplicado
                if (lines.Count > 0)
                {
                    string lastLine = lines[lines.Count - 1];

                    // Solo procesar si es un número con punto decimal
                    if (lastLine.Contains('.'))
                    {
                        // Quitar el punto decimal
                        lines[lines.Count - 1] = lastLine.Replace(".", "");
                        _hasDecimalApplied = false;
                    }
                }
            }

            // Unir líneas manteniendo saltos
            ocrTextEditor.Text = string.Join(Environment.NewLine, lines);

            _updatingText = false;
        }

        private void OnDecimalCheckBoxChanged(object sender, CheckedChangedEventArgs e)
        {
            if (!_updatingText)
            {
                // Actualizar las líneas originales con el texto editado
                _originalLines = ocrTextEditor.Text.Split(
                    new[] { Environment.NewLine },
                    StringSplitOptions.None);

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
                    StringSplitOptions.None);
                _hasDecimalApplied = false; // Resetear ya que el usuario editó manualmente
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