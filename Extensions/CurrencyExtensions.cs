using System;

namespace UniCP.Extensions
{
    public static class CurrencyExtensions
    {
        public static string ToKMB(this decimal value)
        {
            if (value >= 1000000)
                return (value / 1000000).ToString("N1") + "M";
            if (value >= 1000)
                return (value / 1000).ToString("N0") + "K";
            
            return value.ToString("N0");
        }

        public static string ToKMB(this decimal? value)
        {
            return (value ?? 0).ToKMB();
        }
    }
}
