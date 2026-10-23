using System;

namespace HttpJsonRpc
{
    /// <summary>Describes a type in generated OpenRPC schemas.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum, AllowMultiple = false, Inherited = true)]
    public class JsonRpcSchemaAttribute : Attribute
    {
        public string Description { get; set; }
    }
}
