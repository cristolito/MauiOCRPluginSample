using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace MauiOcrPluginSample
{
    public partial class OcrResultPage : ContentPage
    {
        public OcrResultPage(string recognizedText, byte[] imageBytes)
        {
            InitializeComponent();

            // Mostrar resultados
            recognizedTextEditor.Text = recognizedText;
            capturedImage.Source = ImageSource.FromStream(() => new MemoryStream(imageBytes));
#if ANDROID
            var activity = Platform.CurrentActivity;
            activity!.RequestedOrientation = Android.Content.PM.ScreenOrientation.Portrait;
#endif
        }

        private async void OnCopyTextClicked(object sender, EventArgs e)
        {
            await Clipboard.Default.SetTextAsync(recognizedTextEditor.Text);
            await DisplayAlert("Éxito", "Texto copiado al portapapeles", "OK");
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}