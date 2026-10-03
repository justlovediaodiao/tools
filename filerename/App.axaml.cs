using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using SukiUI;
using SukiUI.Models;
using filerename.Views;

namespace filerename;

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
            "Indigo",
            Color.Parse(dark ? "#818CF8" : "#6366F1"),
            Color.Parse(dark ? "#A5B4FC" : "#4F46E5")));
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}