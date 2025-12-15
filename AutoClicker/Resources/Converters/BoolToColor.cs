using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AutoClicker.Resources.Converters
{
    public class BoolToColor : IValueConverter
    {
        private static readonly Color ActiveColor = Colors.Black;
        private static readonly Color InactiveColor = Color.FromArgb("#313022");

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isClicking)
                return isClicking ? ActiveColor : InactiveColor;
            return Colors.Transparent;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class InverseBoolToColorConverter : IValueConverter
    {
        private static readonly Color ActiveColor = Colors.Black;
        private static readonly Color InactiveColor = Color.FromArgb("#313022");

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isClicking)
                return !isClicking ? ActiveColor : InactiveColor; 
            return Colors.Transparent;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class BoolToBorderWidthConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isClicking)
                return isClicking ? 3 : 1;
            return 1;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class InverseBoolToBorderWidthConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isClicking)
                return isClicking ? 1 : 3;
            return 3;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
