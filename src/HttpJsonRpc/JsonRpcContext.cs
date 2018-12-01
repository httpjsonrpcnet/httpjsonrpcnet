﻿using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace HttpJsonRpc
{
    public class JsonRpcContext
    {
        private static readonly AsyncLocal<JsonRpcContext> _Current = new AsyncLocal<JsonRpcContext>();
        public static JsonRpcContext Current
        {
            get => _Current.Value;
            set => _Current.Value = value;
        }

        public HttpListenerContext HttpContext { get; set; }
        public string RequestJson { get; set; }
        public JsonRpcRequest Request { get; set; }
        public JsonRpcMethod Method { get; set; }
        public List<object> RequestParameters { get; set; }
        public object Result { get; set; }

        public Dictionary<string, object> Values { get; } = new Dictionary<string, object>();
    }
}