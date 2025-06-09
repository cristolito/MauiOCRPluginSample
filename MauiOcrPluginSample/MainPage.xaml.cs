using Plugin.Maui.OCR;
using System.Diagnostics;

namespace MauiOcrPluginSample
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Inicializar OCR
            await OcrPlugin.Default.InitAsync();

            // Inicializar cámara si no está ya inicializada
            if (realCaptureCamera.Cameras.Count > 0 && realCaptureCamera.Camera == null)
            {
                realCaptureCamera.Camera = realCaptureCamera.Cameras.First();
                await StartCameraAsync();
            }
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();
            await StopCameraAsync();
        }

        private async Task StartCameraAsync()
        {
            await realCaptureCamera.StopCameraAsync();

            await realCaptureCamera.StartCameraAsync();
        }

        private async Task StopCameraAsync()
        {
            await realCaptureCamera.StopCameraAsync();
        }

        private async void CameraView_CamerasLoaded(object sender, EventArgs e)
        {
            if (realCaptureCamera.Cameras.Count > 0)
            {
                realCaptureCamera.Camera = realCaptureCamera.Cameras.First();
                await StartCameraAsync();
            }
        }

        private async void OnSwitchCameraClicked(object sender, EventArgs e)
        {
            if (realCaptureCamera.Cameras.Count > 1)
            {
                realCaptureCamera.Camera = realCaptureCamera.Camera == realCaptureCamera.Cameras[0] ?
                    realCaptureCamera.Cameras[1] : realCaptureCamera.Cameras[0];

                await StartCameraAsync();
            }
        }
        
        private async void OnResetCamera(object sender, EventArgs e)
        {
            if (realCaptureCamera.Cameras.Count > 1)
            {
                await StartCameraAsync();
            }
        }

        private async void OnCapturePhotoClicked(object sender, EventArgs e)
        {
            try
            {
                loadingIndicator.IsVisible = true;

                // Capturar solo del área de recorte
                var imageStream = await realCaptureCamera.TakePhotoAsync();


                if (imageStream == null || imageStream.Length == 0)
                {
                    await DisplayAlert("Error", "No se pudo capturar la foto", "OK");
                    return;
                }

                byte[] imageBytes;
                using (var memoryStream = new MemoryStream())
                {
                    await imageStream.CopyToAsync(memoryStream);
                    imageBytes = memoryStream.ToArray();
                }

                // Procesar OCR
                await ProcessImageWithOcr(imageBytes);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                await DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                loadingIndicator.IsVisible = false;
            }
        }

        private async void OnSelectFromGalleryClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await MediaPicker.Default.PickPhotoAsync();

                if (result == null) return;

                loadingIndicator.IsVisible = true;

                using var stream = await result.OpenReadAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                var imageBytes = memoryStream.ToArray();

                // Procesar OCR
                await ProcessImageWithOcr(imageBytes);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                await DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                loadingIndicator.IsVisible = false;
            }
        }

        private async Task ProcessImageWithOcr(byte[] imageBytes)
        {
            var ocrResult = await OcrPlugin.Default.RecognizeTextAsync(imageBytes);

            if (!ocrResult.Success)
            {
                await DisplayAlert("Error", "No se pudo reconocer texto en la imagen", "OK");
                return;
            }

            // Navegar a página de resultados
            await Navigation.PushAsync(new OcrResultPage(ocrResult.AllText, imageBytes));
        }
    }
}
