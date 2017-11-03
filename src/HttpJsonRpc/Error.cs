namespace HttpJsonRpc
{
    public class Error
    {
        public int Code { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }
}