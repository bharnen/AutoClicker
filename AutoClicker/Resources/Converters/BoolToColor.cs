using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AutoClicker.Resources.Converters
{
    public class BoolToColor : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isClicking)
                return isClicking ? Colors.Black : Colors.Transparent;
            return Colors.Transparent;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }



    public class InverseBoolToColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isClicking)
                return !isClicking ? Colors.Black : Colors.Transparent; 
            return Colors.Transparent;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

}
