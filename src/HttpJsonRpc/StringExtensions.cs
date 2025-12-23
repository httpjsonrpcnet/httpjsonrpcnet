namespace HttpJsonRpc
{
    internal static class StringExtensions
    {
        public static string ToLowerFirstChar(this string value)
        {
            switch ((value?.Length ?? 0))
            {
                case 0:
                    return value;
                case 1:
                    return value.ToLower();
                default:
                    return $"{char.ToLower(value[0])}{value.Substring(1)}";
            }
        }
    }
}
