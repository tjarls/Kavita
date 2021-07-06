namespace Kavita.Common.Extensions
{
    public static class StringExtensions
    {
        public static string WrapInQuotes(this string text)
        {
            if (!text.Contains(" "))
            {
                return text;
            }

            return "\"" + text + "\"";
        }
    }
}