using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Data;
using System.Linq;
using System.Globalization;

namespace PERSReport.Utilities
{
    // GLOBAL USE FUNCTIONS

    class G
    {
        public static void AddNoteToDataRow(DataRow row, string note)
        {
            Debug.Assert(row.Table.Columns.Contains("Note") == true);
            if (row.Table.Columns.Contains("Note"))
            {
                string tmp = G.Field(row, "Note");
                row["Note"] = tmp + "  " + note;
            }
        }

        public static void DisplayWarning(string warning)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(warning);
            Console.ResetColor();
        }

        public static void DisplayError(string error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(error);
            Console.ResetColor();
        }

        public static void DisplayNotification(string info)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(info);
            Console.ResetColor();
        }

        public static void Display(string info)
        {
            //Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(info);
            //Console.ResetColor();
        }

        public static decimal Decimal(DataRow row, string field, bool rounding = false, int places = 4)
        {
            if ((row != null) && (row.Table.Columns.Contains(field)))
            {
                if ((row[field] != null) && (row[field] != System.DBNull.Value) && (row[field] is decimal))
                {
                   if (!rounding)
                        return row.Field<decimal>(field);
                   else
                        return Math.Round(row.Field<decimal>(field), places, MidpointRounding.ToEven);
                }
                else if ((row[field] != null) && (row[field] != System.DBNull.Value) && (row[field] is string))
                {
                    decimal result;
                    if (decimal.TryParse(G.Field(row, field), out result) == true)
                    {
                        if (!rounding)
                            return result;
                        else
                            return Math.Round(result, places, MidpointRounding.ToEven);
                    }
                }
            }
            return 0.0m;
        }

        public static double Double(DataRow row, string field, bool rounding = false, int places = 4)
        {
            if ((row != null) && (row.Table.Columns.Contains(field)))
            {
                if ((row[field] != null) && (row[field] != System.DBNull.Value) && (row[field] is double))
                {
                    if (!rounding)
                        return row.Field<double>(field);
                    else
                        return Math.Round(row.Field<double>(field), places, MidpointRounding.ToEven);

                }
                else if ((row[field] != null) && (row[field] != System.DBNull.Value) && (row[field] is string))
                {
                    double result;
                    if (double.TryParse(G.Field(row, field), out result) == true)
                    {
                        if (!rounding)
                            return result;
                        else
                            return Math.Round(result, places, MidpointRounding.ToEven);
                    }
                }
            }
            return 0.0;
        }


        public static string R1(decimal dec)
        {
            return (string.Format("{0:00.0}", Math.Round(dec, 1)));
        }

        public static string AR1(decimal dec)
        {
            return (string.Format("{0:00.0}", Math.Round(Math.Abs(dec), 1)));
        }

        public static string R2(decimal dec)
        {
            return (string.Format("{0:00.00}", Math.Round(dec, 2, MidpointRounding.ToEven)));
        }

        public static string AR2(decimal dec)
        {
            return (string.Format("{0:00.00}", Math.Round(Math.Abs(dec), 2, MidpointRounding.ToEven)));
        }

        public static string R3(decimal dec)
        {
            return (string.Format("{0:00.000}", Math.Round(dec, 3, MidpointRounding.ToEven)));
        }

        public static string R4(decimal dec)
        {
            return (string.Format("{0:00.0000}", Math.Round(dec, 4, MidpointRounding.ToEven)));
        }

        public static string R1(DataRow row, string field)
        {
            return (string.Format("{0:00.0}", Math.Round(Decimal(row, field), 1)));
        }

        public static string R2(DataRow row, string field)
        {
            return (string.Format("{0:00.00}", Math.Round(Decimal(row, field), 2, MidpointRounding.ToEven)));
        }

        public static string R3(DataRow row, string field)
        {
            return (string.Format("{0:00.000}", Math.Round(Decimal(row, field), 3, MidpointRounding.ToEven)));
        }

        public static string R4(DataRow row, string field)
        {
            return (string.Format("{0:00.0000}", Math.Round(Decimal(row, field), 4, MidpointRounding.ToEven)));
        }


        public static string Rd1(double dbl)
        {
            return (string.Format("{0:00.0}", Math.Round(dbl, 1)));
        }

        public static string ARd1(double dbl)
        {
            return (string.Format("{0:00.0}", Math.Round(Math.Abs(dbl), 1)));
        }

        public static string Rd2(double dbl)
        {
            return (string.Format("{0:00.00}", Math.Round(dbl, 2, MidpointRounding.ToEven)));
        }

        public static string Rd3(double dbl)
        {
            return (string.Format("{0:00.000}", Math.Round(dbl, 3, MidpointRounding.ToEven)));
        }

        public static string Rd1(DataRow row, string field)
        {
            return (string.Format("{0:00.0}", Math.Round(Double(row, field), 1)));
        }

        public static string Rd2(DataRow row, string field)
        {
            return (string.Format("{0:00.00}", Math.Round(Double(row, field), 2, MidpointRounding.ToEven)));
        }

        public static string Rd3(DataRow row, string field)
        {
            return (string.Format("{0:00.000}", Math.Round(Double(row, field), 3, MidpointRounding.ToEven)));
        }


        public static string Truncate(string source, int length)
        {
            if (source.Length > length)
                source = source.Substring(0, length);
            return source;
        }

        public static string ConvertDataRowToXML(DataRow dr)
        {
            Debug.Assert(dr != null);
            StringBuilder stringBuilder = new StringBuilder();
            dr.Table.Columns.Cast<DataColumn>().ToList().ForEach(column =>
            {
                string tag = column.ColumnName.Replace(" ", "_x0020_");
                stringBuilder.AppendFormat("<{0}>{1}</{2}>", tag, dr[column], tag);
            });
            return stringBuilder.ToString();
        }

        public static string FromDBVal(object obj)
        {
            if (obj == null || obj == System.DBNull.Value)
            {
                return string.Empty;
            }
            if (obj is System.DateTime)
            {
                // ex:  "2014-01-01 00:00:00"
                System.DateTime dt = (System.DateTime)obj;
                DateTimeFormatInfo fmt = (new CultureInfo("en-US")).DateTimeFormat;
                return (dt.ToString(fmt.SortableDateTimePattern));
            }
            else
            {
                return obj.ToString();
            }
        }

        public static string Field(DataRow row, string field)
        {
            if ((row != null) && (row.Table.Columns.Contains(field)))
            {
                return G.FromDBVal(row[field]);
            }
            else
                return string.Empty;
        }

        public static bool IsDiff(string current, string proposed, StringComparison rule = StringComparison.OrdinalIgnoreCase)
        {
            return (!string.Equals(current.Trim(), proposed.Trim(), rule));
        }

        public static bool IsSame(string current, string proposed, StringComparison rule = StringComparison.OrdinalIgnoreCase)
        {
            return (string.Equals(current.Trim(), proposed.Trim(), rule));
        }

        public static bool Contains(string current, string substring)
        {
            return (current.Contains(substring));
        }

        public static string TrimLeadingZeros(string s)
        {
            char[] trimchars = new char[] { '0', ' ' };
            return (s.TrimStart(trimchars));
        }

        public static bool HasMinimalDifference(double value1, double value2, int units)
        {
            long lValue1 = BitConverter.DoubleToInt64Bits(value1);
            long lValue2 = BitConverter.DoubleToInt64Bits(value2);

            // If the signs are different, return false except for +0 and -0.
            if ((lValue1 >> 63) != (lValue2 >> 63))
            {
                if (value1 == value2)
                    return true;

                return false;
            }

            long diff = Math.Abs(lValue1 - lValue2);

            if (diff <= (long)units)
                return true;

            return false;
        }

        public static bool HasMinimalDifference(decimal value1, decimal value2)
        {
            Int32 compare = decimal.Compare(value1, value2);

            if (compare == 0)
                return true;

            decimal diff = Math.Abs(value1 - value2);

            if (diff <= 0.0001m)
                return true;

            return false;
        }

        public static bool HasSignificantDifference(decimal value1, decimal value2, decimal significance = 0.05m)
        {
            Int32 compare = decimal.Compare(value1, value2);

            if (compare == 0)
                return false;

            decimal diff = Math.Abs(value1 - value2);

            if (diff > (significance * (value1 + value2)))
                return true;

            return false;
        }
    }


    public static class StringExtensions
    {
        public static string Left(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            maxLength = Math.Abs(maxLength);

            return (value.Length <= maxLength
                   ? value
                   : value.Substring(0, maxLength)
                   );
        }
    }

}
