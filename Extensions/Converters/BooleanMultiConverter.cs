using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace Extensions;

public class BooleanMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        bool logicReverse = false;
        bool reverse = false;
        string mode = "AND";

        if (parameter is string p)
        {
            foreach (string part in p.Split([ ',' ], StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.Equals("OR", StringComparison.OrdinalIgnoreCase))
                {
                    mode = "OR";
                }

                if (part.Equals("AND", StringComparison.OrdinalIgnoreCase))
                {
                    mode = "AND";
                }

                if (part.Equals("LogicReverse", StringComparison.OrdinalIgnoreCase))
                {
                    logicReverse = true;
                }

                if (part.Equals("Reverse", StringComparison.OrdinalIgnoreCase))
                {
                    reverse = true;
                }
            }
        }

        IEnumerable<bool> bools = values.OfType<bool>();

        if (logicReverse)
        {
            bools = bools.Select(b => !b);
        }

        bool result = mode == "OR" ? bools.Any(b => b) : bools.All(b => b);

        if (reverse)
        {
            result = !result;
        }

        return result;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

