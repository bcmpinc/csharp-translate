public static class Extensions
{
    public static string Repeat(this string str, int count) {
        return string.Concat(Enumerable.Repeat(str, count));
    }
    public static string Truncate(this string str, int max_length) {
        if (str.Length <= max_length) return str;
        return str.Substring(0,max_length-1) + '…';
    }
}
