using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using VSCodex.Models;

namespace VSCodex.Views;

public sealed class RoleBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not CodexMessageRole role) return Brushes.LightGray;
        return role switch
        {
            CodexMessageRole.User => Brushes.DeepSkyBlue,
            CodexMessageRole.Assistant => Brushes.MediumSeaGreen,
            CodexMessageRole.Error => Brushes.IndianRed,
            CodexMessageRole.Memory => Brushes.Khaki,
            CodexMessageRole.Skill => Brushes.Plum,
            CodexMessageRole.Mcp => Brushes.Orange,
            _ => Brushes.LightGray,
        };
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
