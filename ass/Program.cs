using Avalonia;
using Ass;

AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace()
    .StartWithClassicDesktopLifetime(args);
