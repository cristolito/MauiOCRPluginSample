using CommunityToolkit.Maui.Core.Primitives;
using Plugin.Maui.OCR;
using System.Diagnostics;
using CommunityToolkit.Maui.Core;
using SkiaSharp;
using System.Globalization;
using System.Text.RegularExpressions;
using System.IO;

namespace MauiOcrPluginSample
{
    public partial class MainPage : ContentPage
    {
        private ICameraProvider cameraProvider;
        private bool isProcessingImage;
        int pageCount;
        double widthCamera = 0;
        double heigthCamera = 0;
        private bool _isFlashOn = false;
        private float _currentZoom = 1.0f;

        #region Control de Zoom con Slider

        private void OnZoomSliderChanged(object sender, ValueChangedEventArgs e)
        {
            _currentZoom = (float)e.NewValue;
            MyCamera.ZoomFactor = _currentZoom;
        }

        #endregion

        #region Control de Flash

        private async void OnFlashButtonClicked(object sender, EventArgs e)
        {
            try
            {
                _isFlashOn = !_isFlashOn;

                // Actualizar UI del botón
                flashButton.Text = _isFlashOn ? "⚡ ON" : "⚡ OFF";
                flashButton.BackgroundColor = _isFlashOn ? Color.FromArgb("#FFD700") : Color.FromArgb("#333333");
                MyCamera.CameraFlashMode = _isFlashOn ? CameraFlashMode.On : CameraFlashMode.Off;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al controlar flash: {ex.Message}");
                await DisplayAlert("Error", "No se pudo activar el flash", "OK");
                _isFlashOn = false;
                flashButton.Text = "⚡ OFF";
                flashButton.BackgroundColor = Color.FromArgb("#333333");
            }
        }

        #endregion

        private static readonly Dictionary<string, string> CharacterReplacements = new()
            {
                {"|", "1"}, {"!", "1"}, {"i", "1"}, {"I", "1"},
                {"s", "5"}, {"S", "5"}, {"§", "5"}, {"$", "5"},
                {"t", "7"}, {"T", "7"}, {"?", "7"}, {"Z", "7"},
                {"o", "0"}, {"O", "0"}, {"°", "0"}, {"Q", "0"}, {"D", "0"},
                {"b", "6"}, {"B", "8"}, {"g", "9"}, {"q", "9"}
            };

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

            zoomSlider.Value = _currentZoom;
            flashButton.Text = _isFlashOn ? "⚡ ON" : "⚡ OFF";
            flashButton.BackgroundColor = _isFlashOn ? Color.FromArgb("#FFD700") : Color.FromArgb("#333333");
            // Actualizar slider según capacidades de la cámara
            if (MyCamera.SelectedCamera != null)
            {
                zoomSlider.Maximum = MyCamera.SelectedCamera.MaximumZoomFactor;
                zoomSlider.Minimum = MyCamera.SelectedCamera.MinimumZoomFactor;
            }
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
                double scaleAdjustment = .9; // Ajusta este valor según necesidad (0.9 - 0.99)

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

                    using var processedStream = PreprocessImageForOcr(e.Media);
                    await processedStream.CopyToAsync(tempStream);
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
            var options = new OcrOptions.Builder()
                .SetTryHard(true)
                .Build();

            var ocrResult = await OcrPlugin.Default.RecognizeTextAsync(imageBytes, options);

            if (!ocrResult.Success)
            {
                await DisplayAlert("Error", "No se pudo reconocer texto en la imagen", "OK");
                return;
            }

            // Procesar cada línea por separado
            var cleanedLines = new List<string>();
            foreach (var line in ocrResult.Lines)
            {
                var cleanedLine = CleanOdometerReading(line);
                if (!string.IsNullOrEmpty(cleanedLine))
                {
                    cleanedLines.Add(cleanedLine);
                }
            }

            loadingIndicator.IsVisible = false;

            // Pasar la lista de resultados en lugar del texto unido
            await Navigation.PushAsync(new OcrResultPage(cleanedLines, imageBytes));
        }
        private Stream PreprocessImageForOcr(Stream imageStream)
        {
            try
            {
                // Cargar imagen original
                using var original = SKBitmap.Decode(imageStream);
                if (original == null) return imageStream;

                // Convertir a escala de grises
                using var grayBitmap = new SKBitmap(original.Width, original.Height);
                using (var canvas = new SKCanvas(grayBitmap))
                {
                    var paint = new SKPaint
                    {
                        ColorFilter = SKColorFilter.CreateColorMatrix(new float[]
                        {
                            0.299f, 0.587f, 0.114f, 0, 0,
                            0.299f, 0.587f, 0.114f, 0, 0,
                            0.299f, 0.587f, 0.114f, 0, 0,
                            0, 0, 0, 1, 0
                        })
                    };
                    canvas.DrawBitmap(original, 0, 0, paint);
                }

                // Aumentar contraste
                using var contrastBitmap = new SKBitmap(grayBitmap.Width, grayBitmap.Height);
                using (var canvas = new SKCanvas(contrastBitmap))
                {
                    var contrast = 1.5f; // Ajustar según necesidad
                    var matrix = new float[]
                    {
                        contrast, 0, 0, 0, (1 - contrast) * 0.5f,
                        0, contrast, 0, 0, (1 - contrast) * 0.5f,
                        0, 0, contrast, 0, (1 - contrast) * 0.5f,
                        0, 0, 0, 1, 0
                    };

                    var paint = new SKPaint
                    {
                        ColorFilter = SKColorFilter.CreateColorMatrix(matrix)
                    };
                    canvas.DrawBitmap(grayBitmap, 0, 0, paint);
                }

                // Reducir ruido (filtro bilateral o desenfoque)
                using var finalBitmap = new SKBitmap(contrastBitmap.Width, contrastBitmap.Height);
                using (var canvas = new SKCanvas(finalBitmap))
                {
                    var paint = new SKPaint
                    {
                        ImageFilter = SKImageFilter.CreateBlur(1f, 1f)
                    };
                    canvas.DrawBitmap(contrastBitmap, 0, 0, paint);
                }

                // Guardar resultado
                var resultStream = new MemoryStream();
                finalBitmap.Encode(resultStream, SKEncodedImageFormat.Jpeg, 90);
                resultStream.Position = 0;
                return resultStream;
            }
            catch
            {
                // Si falla el procesamiento, devolver la imagen original
                imageStream.Position = 0;
                return imageStream;
            }
        }
        public string CleanOdometerReading(string rawText)
        {
            string cleaned = string.Empty;

            foreach (var replacement in CharacterReplacements)
            {
                cleaned = cleaned.Replace(replacement.Key, replacement.Value);
            }

            cleaned = Regex.Replace(rawText, @"[^\d.,]", "");

            

            cleaned = ProcessDecimalSeparators(cleaned);

            if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            {
                return FormatOdometerResult(cleaned, result);
            }

            return string.Empty;
        }

        private string ProcessDecimalSeparators(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Caso 1: Contiene coma (formato europeo)
            if (input.Contains(','))
            {
                // Reemplazar todos los puntos (separadores de miles)
                input = input.Replace(".", "");
                // Convertir coma decimal a punto
                return input.Replace(",", ".");
            }

            // Caso 2: Contiene punto
            if (input.Contains('.'))
            {
                // Si hay más de un punto, asumir que son separadores de miles
                if (input.Count(c => c == '.') > 1)
                {
                    return input.Replace(".", "");
                }

                // Si solo hay un punto, verificar si es decimal
                var parts = input.Split('.');
                if (parts.Length == 2)
                {
                    // Regla especial para odómetros: 
                    // Si la parte decimal tiene 1 dígito, es decimal válido
                    // Si tiene más dígitos, probablemente sea un separador
                    if (parts[1].Length == 1 && char.IsDigit(parts[1][0]))
                    {
                        return input; // Mantener como decimal
                    }
                    return input.Replace(".", ""); // Eliminar como separador
                }
            }

            return input;
        }

        private string FormatOdometerResult(string cleanedValue, double parsedValue)
        {
            // Para odómetros, normalmente queremos:
            // - Sin decimales si no los tiene
            // - Máximo 1 decimal si es .0 o .5 (común en odómetros)
            // - Eliminar .0 pero mantener .5 u otros valores decimales

            var decimalPart = cleanedValue.Contains('.')
                ? cleanedValue.Split('.')[1]
                : string.Empty;

            if (decimalPart.Length == 1)
            {
                // Caso especial para valores como 1500.8
                return parsedValue.ToString("0.0", CultureInfo.InvariantCulture);
            }
            else if (decimalPart == "0" || string.IsNullOrEmpty(decimalPart))
            {
                // Eliminar decimales cero (1500.0 → 1500)
                return parsedValue.ToString("0", CultureInfo.InvariantCulture);
            }

            // Otros casos (ej. 1234.56 aunque no sea común en odómetros)
            return parsedValue.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
