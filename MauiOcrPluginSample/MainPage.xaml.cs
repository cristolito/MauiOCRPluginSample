using CommunityToolkit.Maui.Core.Primitives;
using Plugin.Maui.OCR;
using System.Diagnostics;
using CommunityToolkit.Maui.Core;
using SkiaSharp;

namespace MauiOcrPluginSample
{
    public partial class MainPage : ContentPage
    {
        private ICameraProvider cameraProvider;
        private bool isProcessingImage;
        int pageCount;
        double widthCamera = 0;
        double heigthCamera = 0;

        public MainPage(ICameraProvider cameraProvider)
        {
            InitializeComponent();

            this.cameraProvider = cameraProvider;
            MyCamera.MediaCaptured += MyCamera_MediaCaptured;
            Loaded += (s, e) =>
            {
                pageCount = Navigation.NavigationStack.Count;
            };
        }
        protected override async void OnAppearing()
        {
            base.OnAppearing();
#if ANDROID
            var activity = Platform.CurrentActivity;
            activity!.RequestedOrientation = Android.Content.PM.ScreenOrientation.Landscape;
#endif
            await OcrPlugin.Default.InitAsync();
            await cameraProvider.RefreshAvailableCameras(CancellationToken.None);
            MyCamera.SelectedCamera = cameraProvider.AvailableCameras
                .Where(c => c.Position == CameraPosition.Rear).FirstOrDefault();
        }
        protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
        {
            base.OnNavigatedFrom(args);
            if (Navigation.NavigationStack.Count < pageCount)
            {
                Cleanup();
            }
        }
        void Cleanup()
        {
            MyCamera.MediaCaptured -= MyCamera_MediaCaptured;
            MyCamera.Handler?.DisconnectHandler();
        }
        // Método para obtener las dimensiones reales del ContentView
        private (double containerWidth, double containerHeight) GetCameraContainerDimensions()
        {
            return (MyCamera.Width, MyCamera.Height);
        }
        private void OnCameraContentSizeChanged(object sender, EventArgs e)
        {
            if (MyCamera.Width <= 0 || MyCamera.Height <= 0)
                return;

            if (heigthCamera <= 0) heigthCamera = MyCamera.Height / 2;
            if (widthCamera <= 0) widthCamera = MyCamera.Height;

            CaptureBorder.HeightRequest = heigthCamera;
            CaptureBorder.WidthRequest = widthCamera;
        }
        private Stream? CropImageToBorderArea(Stream imageStream)
        {
            try
            {
                using var original = SKBitmap.Decode(imageStream);

                int imageWidth = original.Width;
                int imageHeight = original.Height;

                // Determinar si la imagen está en modo normal (altura = 3/4 ancho) o inverso (ancho = 3/4 altura)
                bool isNormalOrientation = imageHeight <= imageWidth;

                double uiWidth, uiHeight;
                double rectWidth, rectHeight;
                double rectX, rectY;

                // Factor de ajuste para reducir ligeramente el rectángulo (ej. 0.95 = 5% más pequeño)
                double scaleAdjustment = 0.98; // Ajusta este valor según necesidad (0.9 - 0.99)

                if (isNormalOrientation)
                {
                    // Caso normal: altura = 3/4 del ancho
                    uiWidth = widthCamera;
                    uiHeight = widthCamera * 3 / 4;

                    // Rectángulo de recorte: ancho = altura original, alto = mitad del ancho original
                    rectWidth = uiHeight * scaleAdjustment;  // Ajustado para ser más pequeño
                    rectHeight = (uiWidth / 2) * scaleAdjustment;  // Ajustado para ser más pequeño

                    rectX = (uiWidth - rectWidth) / 2;
                    rectY = (uiHeight - rectHeight) / 2;
                }
                else
                {
                    // Caso inverso: ancho = 3/4 de la altura
                    uiHeight = widthCamera;
                    uiWidth = widthCamera * 3 / 4;

                    // Rectángulo de recorte: ancho = mitad de la altura original, alto = ancho original
                    rectWidth = (uiHeight / 2) * scaleAdjustment;  // Ajustado para ser más pequeño
                    rectHeight = uiWidth * scaleAdjustment;  // Ajustado para ser más pequeño

                    rectX = (uiWidth - rectWidth) / 2;
                    rectY = (uiHeight - rectHeight) / 2;
                }

                double scaleX = imageWidth / uiWidth;
                double scaleY = imageHeight / uiHeight;

                int cropX = (int)(rectX * scaleX);
                int cropY = (int)(rectY * scaleY);
                int cropWidth = (int)(rectWidth * scaleX);
                int cropHeight = (int)(rectHeight * scaleY);

                cropX = Math.Max(0, cropX);
                cropY = Math.Max(0, cropY);
                cropWidth = Math.Min(cropWidth, imageWidth - cropX);
                cropHeight = Math.Min(cropHeight, imageHeight - cropY);

                // Recortar
                var cropped = new SKBitmap(cropWidth, cropHeight);
                using (var canvas = new SKCanvas(cropped))
                {
                    var sourceRect = new SKRectI(cropX, cropY, cropX + cropWidth, cropY + cropHeight);
                    var destRect = new SKRectI(0, 0, cropWidth, cropHeight);
                    canvas.DrawBitmap(original, sourceRect, destRect);
                }

                var resultStream = new MemoryStream();
                cropped.Encode(resultStream, SKEncodedImageFormat.Jpeg, 90);
                resultStream.Position = 0;
                return resultStream;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al recortar imagen: {ex.Message}");
                return null;
            }
        }

        private async void MyCamera_MediaCaptured(object? sender, CommunityToolkit.Maui.Views.MediaCapturedEventArgs e)
        {
            if (e.Media == null || e.Media.Length == 0)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    DisplayAlert("Error", "No se pudo capturar la foto", "OK"));
                return;
            }

            try
            {
                byte[] rawImageBytes;
                using (var tempStream = new MemoryStream())
                {
                    e.Media.Position = 0;
                    await e.Media.CopyToAsync(tempStream);
                    rawImageBytes = tempStream.ToArray();
                }

                // Recortar imagen
                byte[] croppedImageBytes;
                using (var inputImageStream = new MemoryStream(rawImageBytes))
                using (var croppedImageStream = CropImageToBorderArea(inputImageStream))
                {
                    if (croppedImageStream == null)
                        throw new Exception("No se pudo recortar la imagen");

                    using var finalStream = new MemoryStream();
                    await croppedImageStream.CopyToAsync(finalStream);
                    croppedImageBytes = finalStream.ToArray();
                }

                // Enviar la imagen recortada al OCR
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await ProcessImageWithOcr(croppedImageBytes);
                });
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    DisplayAlert("Error", $"Error al procesar la imagen: {ex.Message}", "OK"));
            }
            finally
            {
                e.Media?.Dispose();
            }
        }

        private async void OnResetCamera(object sender, EventArgs e)
        {
            try
            {
                var startCameraPreviewTCS = new CancellationTokenSource(TimeSpan.FromSeconds(3));

                await MyCamera.StartCameraPreview(startCameraPreviewTCS.Token);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private async void OnCapturePhotoClicked(object sender, EventArgs e)
        {
            if (isProcessingImage) return;

            isProcessingImage = true;
            loadingIndicator.IsVisible = true;
            try
            {
                await MyCamera.CaptureImage(CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                isProcessingImage = false;
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
            loadingIndicator.IsVisible = false;
            await Navigation.PushAsync(new OcrResultPage(ocrResult.AllText, imageBytes));
        }
    }
}
