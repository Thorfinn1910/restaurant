using System.Globalization;

namespace QuanLyNhaHang.Utils
{
    public static class MoneyFormatter
    {
        private static readonly CultureInfo VndDisplayCulture = CultureInfo.GetCultureInfo("en-US");

        public static string FormatVnd(decimal amount)
        {
            return amount.ToString("#,##0", VndDisplayCulture) + " VND";
        }

        public static string FormatVndRaw(decimal amount)
        {
            return amount.ToString("#,##0", VndDisplayCulture);
        }

        public static string FormatPlainAmount(decimal amount)
        {
            return amount.ToString("0", VndDisplayCulture);
        }
    }
}
