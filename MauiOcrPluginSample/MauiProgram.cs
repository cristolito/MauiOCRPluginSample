﻿using Camera.MAUI;
using Plugin.Maui.OCR;

namespace MauiOcrPluginSample
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseOcr()
                .UseMauiCameraView()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Use with dependency injection
            // builder.Services.AddSingleton<IOcrService>(OcrPlugin.Default);

            return builder.Build();
        }
    }
}
