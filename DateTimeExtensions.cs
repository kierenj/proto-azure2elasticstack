using System;

namespace azure2elasticstack
{
    public static class DateTimeExtensions
    {
        public static DateTime RoundMinutes(this DateTime time)
        {
            return new DateTime(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0, time.Kind);
        }
    }
}