using System.Data;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AIT_App;

public sealed class DataRowColumnValueConverter : IValueConverter
{
    public static readonly DataRowColumnValueConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string columnName || string.IsNullOrWhiteSpace(columnName))
            return null;

        object? v = value switch
        {
            DataRowView drv => drv.Row.Table.Columns.Contains(columnName) ? drv.Row[columnName] : null,
            DataRow dr => dr.Table.Columns.Contains(columnName) ? dr[columnName] : null,
            _ => null
        };

        return v is null or DBNull ? string.Empty : v;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

