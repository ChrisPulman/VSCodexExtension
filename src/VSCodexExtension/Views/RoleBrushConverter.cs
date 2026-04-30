using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using VSCodexExtension.Models;
namespace VSCodexExtension.Views
{
    public sealed class RoleBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is CodexMessageRole role)) return Brushes.LightGray;
            switch (role)
            {
                case CodexMessageRole.User: return Brushes.DeepSkyBlue;
                case CodexMessageRole.Assistant: return Brushes.MediumSeaGreen;
                case CodexMessageRole.Error: return Brushes.IndianRed;
                case CodexMessageRole.Memory: return Brushes.Khaki;
                case CodexMessageRole.Skill: return Brushes.Plum;
                case CodexMessageRole.Mcp: return Brushes.Orange;
                default: return Brushes.LightGray;
            }
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
