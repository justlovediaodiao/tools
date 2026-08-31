using Avalonia.Data.Converters;

namespace RawV.Converters;

public class Converters
{
    public static readonly IValueConverter SelectedBorderThicknessConverter = new FuncValueConverter<bool, object>(
        isSelected => isSelected ? new Avalonia.Thickness(2) : new Avalonia.Thickness(0));

    public static readonly IValueConverter SidebarWidthConverter = new FuncValueConverter<bool, object>(
        isVisible => isVisible ? new Avalonia.Controls.GridLength(180) : new Avalonia.Controls.GridLength(0));
}
