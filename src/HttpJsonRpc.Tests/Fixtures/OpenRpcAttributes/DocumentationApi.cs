namespace HttpJsonRpc.Tests.Fixtures.OpenRpcAttributes
{
    [JsonRpcClass("documented")]
    public static class DocumentationApi
    {
        [JsonRpcMethod("echo"), OpenRpcMethod(Description = "Returns the supplied value.")]
        [OpenRpcResult(Description = "The supplied value.", Nullable = false, MinLength = 3)]
        public static string Echo(
            [JsonRpcParameter(Name = "text"), OpenRpcParameter(Description = "Text to return.", Required = true, Nullable = false, MinLength = 3)] string value = "fallback",
            [JsonRpcParameter(Ignore = true), OpenRpcParameter(Required = true)] string ignored = "unused") => value;

        [JsonRpcMethod]
        public static string Required([OpenRpcParameter(Required = false)] string value) => value;

        [JsonRpcMethod]
        public static DocumentationRequest Flatten([JsonRpcParams] DocumentationRequest values) => values;

        [JsonRpcMethod]
        public static DocumentationRequest Nested(DocumentationRequest values) => values;

        [JsonRpcMethod]
        public static string SerializerRequired([JsonRpcParams] RequiredRequest values) => values.Value;

        [OpenRpcMethod(Description = "An ordinary method.")]
        public static string Unregistered() => "ordinary";
    }
}
