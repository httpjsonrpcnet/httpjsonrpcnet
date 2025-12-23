namespace HttpJsonRpc
{
    public class OpenRpcMethod
    {
        public string Name { get; set; }
        public object[] Tags { get; set; }
        public string Summary { get; set; }
        public string Description { get; set; }
        public OpenRpcExternalDocumentation ExternalDocs { get; set; }
        public object[] Params { get; set; }
        public object Result { get; set; }
        public bool? Deprecated{ get; set; }
        public OpenRpcServer[] Servers { get; set; }
        public object[] Errors { get; set; }
        public object[] Links { get; set; }
        public string ParamStructure { get; set; }
        public object[] Examples { get; set; }
    }
}
