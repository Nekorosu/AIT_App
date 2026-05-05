using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AIT_App.Services
{
    // Конвертер: значение оценки -> цвет фона бейджа.
    // Используется в DataGridTemplateColumn для ячеек с оценкой.
    // 5     -> зелёный
    // 4 / 3 -> жёлтый
    // 2     -> красный
    // Н     -> серый
    // null/пусто/неизвестное -> прозрачный (бейдж не виден)
    public class GradeBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string s = (value?.ToString() ?? "").Trim();
            if (string.IsNullOrEmpty(s)) return Brushes.Transparent;

            // Берём кисть из ресурсов приложения, чтобы цвета были централизованы.
            string? key = s switch
            {
                "5" => "Grade5BgBrush",
                "4" => "Grade4BgBrush",
                "3" => "Grade3BgBrush",
                "2" => "Grade2BgBrush",
                "Н" => "GradeNBgBrush",
                "н" => "GradeNBgBrush",
                "H" => "GradeNBgBrush", // на случай латинской H
                "h" => "GradeNBgBrush",
                _   => null
            };

            if (key == null) return Brushes.Transparent;

            if (Application.Current != null &&
                Application.Current.Resources.TryGetResource(key, null, out var brush) &&
                brush is IBrush b)
                return b;

            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // Конвертер: значение оценки -> цвет текста бейджа.
    public class GradeForegroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string s = (value?.ToString() ?? "").Trim();
            if (string.IsNullOrEmpty(s)) return Brushes.Transparent;

            string? key = s switch
            {
                "5" => "Grade5FgBrush",
                "4" => "Grade4FgBrush",
                "3" => "Grade3FgBrush",
                "2" => "Grade2FgBrush",
                "Н" => "GradeNFgBrush",
                "н" => "GradeNFgBrush",
                "H" => "GradeNFgBrush",
                "h" => "GradeNFgBrush",
                _   => null
            };

            if (key == null) return Brushes.Transparent;

            if (Application.Current != null &&
                Application.Current.Resources.TryGetResource(key, null, out var brush) &&
                brush is IBrush b)
                return b;

            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
