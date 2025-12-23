namespace HttpJsonRpc
{
    public class OpenRpcContentDescriptor
    {
        public string Name { get; set; }
        public string Summary { get; set; }
        public string Description { get; set; }
        public bool? Required { get; set; }
        public OpenRpcSchema Schema { get; set; }
        public bool? Deprecated { get; set; }
    }
}