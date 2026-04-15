using System;

namespace MVVM.Binding.Interfaces
{
    public interface IValueConverter
    {
        object Convert(object value, Type targetType);
        object ConvertBack(object value, Type sourceType);
        bool CanConvert(Type sourceType, Type targetType);
    }
}