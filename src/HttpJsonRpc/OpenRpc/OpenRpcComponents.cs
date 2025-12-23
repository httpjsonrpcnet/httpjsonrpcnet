using System.Collections.Generic;

namespace HttpJsonRpc
{
    public class OpenRpcComponents
    {
        public Dictionary<string, OpenRpcContentDescriptor> ContentDescriptors { get; set; }
        public Dictionary<string, OpenRpcSchema> Schemas { get; set; }
        public Dictionary<string, OpenRpcExample> Examples { get; set; }
        public Dictionary<string, OpenRpcLink> Links { get; set; }
        public Dictionary<string, OpenRpcError> Errors { get; set; }
        public Dictionary<string, OpenRpcExamplePairing> ExamplePairingObjects { get; set; }
        public Dictionary<string, OpenRpcTag> Tags { get; set; }
    }
}