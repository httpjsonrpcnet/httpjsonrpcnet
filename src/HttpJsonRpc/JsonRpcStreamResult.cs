using System.IO;

namespace HttpJsonRpc
{
    public class JsonRpcStreamResult
    {
        public Stream Stream { get; set; }
        public string ContentType { get; set; }

        public JsonRpcStreamResult()
        {
        }

        public JsonRpcStreamResult(Stream stream, string contentType)
        {
            Stream = stream;
            ContentType = contentType;
        }
    }
}