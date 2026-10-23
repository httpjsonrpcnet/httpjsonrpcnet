using System.ComponentModel;

namespace HttpJsonRpc.Tests.Fixtures.OpenRpcDescriptions
{
    [Description("Not API documentation.")]
    public class UiOnlyType
    {
        [Description("Not API property documentation.")]
        public string Value { get; set; }
    }
}
