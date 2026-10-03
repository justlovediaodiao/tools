using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using SukiUI;
using SukiUI.Models;
using RawV.ViewModels;
using RawV.Views;

namespace RawV;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ApplyColorTheme();
        ActualThemeVariantChanged += (_, _) => ApplyColorTheme();
    }

    private void ApplyColorTheme()
    {
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        SukiTheme.GetInstance().ChangeColorTheme(new SukiColorTheme(
            "Jade",
            Color.Parse(dark ? "#5EEAD4" : "#0F766E"),
            Color.Parse(dark ? "#99F6E4" : "#115E59")));
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
