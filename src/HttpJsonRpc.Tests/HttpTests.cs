using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Json.Schema;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace HttpJsonRpc.Tests
{
    [TestFixture, NonParallelizable]
    public partial class HttpTests
    {
        private HttpClient _Client;
        private static readonly ConcurrentDictionary<string, int> Finished = new ConcurrentDictionary<string, int>();
        private static int Completed;
        [OneTimeSetUp]
        public void Register()
        {
            JsonRpc.RegisterClass(typeof(ExtendedApi)); JsonRpc.RegisterClass(typeof(Api)); JsonRpc.RegisterClass(typeof(Owned)); JsonRpc.RegisterClass(typeof(UnitTests.UnitApi));
            JsonRpc.OnReceivedHttpRequest(c =>
            {
                if (c.Request.Path == "/deny") { c.Response.StatusCode = 401; c.Response.ContentLength = 0; }
                if (c.Request.Path == "/handled") { JsonRpcContext.Current.Handled = true; c.Response.StatusCode = 204; }
            });
            JsonRpc.OnRequestFinished(c => Finished.AddOrUpdate(c.Request?.Id?.ToString() ?? "none", 1, (_, n) => n + 1));
            JsonRpc.OnCompletedRequest(c => Interlocked.Increment(ref Completed));
        }
        [SetUp]
        public void Start()
        {
            Finished.Clear(); Completed = 0; Owned.Disposals = 0;
            JsonRpc.Options.StrictProtocol = true; JsonRpc.Options.IncludeExceptionMessagesInErrors = false; JsonRpc.Options.IncludeStackTraceInErrors = false;
            JsonRpc.Options.MaxBatchSize = 100; JsonRpc.Options.OpenRpc.IsEnabled = true;
            JsonRpc.Options.OpenRpc.Info.Title = "Test API"; JsonRpc.Options.OpenRpc.Info.Version = "1.0";
            JsonRpc.SerializerOptions = new JsonRpcOptions().SerializerOptions;
            JsonRpc.CorsPolicy = p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            JsonRpc.ServerOptions = o => o.Listen(IPAddress.Loopback, 0);
            JsonRpc.Options.ConfigureApplication = app => app.Use(async (c, next) => { c.Response.Headers["X-Middleware"] = "yes"; await next(); });
            JsonRpc.Start();
            _Client = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri(JsonRpc.ListeningAddresses.Single()), Timeout = TimeSpan.FromSeconds(10) };
        }
        [TearDown] public async Task Stop() { _Client.Dispose(); await JsonRpc.StopAsync(); JsonRpc.Options.StrictProtocol = false; }
        private Task<HttpResponseMessage> Post(string body, string path = "/") => _Client.PostAsync(path, new StringContent(body, Encoding.UTF8, "application/json"));
        private async Task<JsonNode> Call(string body, string path = "/") { using (var r = await Post(body, path)) return JsonNode.Parse(await r.Content.ReadAsStringAsync()); }
        private static string Request(string method, string parameters = "[]", string id = "1") => "{\"jsonrpc\":\"2.0\",\"method\":\"" + method + "\",\"params\":" + parameters + ",\"id\":" + id + "}";
        [Test] public async Task StandardCallAndMiddleware() { using (var r = await Post(Request("api.add", "[4]"))) { Assert.That(r.Headers.Contains("X-Middleware"), Is.True); var n = JsonNode.Parse(await r.Content.ReadAsStringAsync()); Assert.That(n["result"].GetValue<int>(), Is.EqualTo(5)); Assert.That(n.AsObject().ContainsKey("error"), Is.False); } }
        [Test] public async Task MiddlewareRunsBeforeCors() { using (var request = new HttpRequestMessage(HttpMethod.Options, "/")) { request.Headers.Add("Origin", "https://example.com"); request.Headers.Add("Access-Control-Request-Method", "POST"); using (var r = await _Client.SendAsync(request)) { Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.NoContent)); Assert.That(r.Headers.Contains("X-Middleware"), Is.True); } } }
        [TestCase("/deny", 401), TestCase("/handled", 204)] public async Task RejectionNeverExecutes(string path, int status) { var before = Api.Executions; using (var r = await Post(Request("api.add", "[4]"), path)) Assert.That((int)r.StatusCode, Is.EqualTo(status)); Assert.That(Api.Executions, Is.EqualTo(before)); }
        [TestCase("{", -32700), TestCase("{}", -32600), TestCase("{\"jsonrpc\":\"2.0\",\"method\":1}", -32600), TestCase("{\"jsonrpc\":\"2.0\",\"method\":\"missing.x\",\"id\":1}", -32601)]
        public async Task ProtocolErrors(string json, int code) { var n = await Call(json); Assert.That(n["error"]["code"].GetValue<int>(), Is.EqualTo(code)); Assert.That(n.AsObject().ContainsKey("result"), Is.False); }
        [TestCase("[]"), TestCase("[\"bad\"]"), TestCase("[1,2]")]
        public async Task InvalidParameters(string parameters) { var n = await Call(Request("api.add", parameters)); Assert.That(n["error"]["code"].GetValue<int>(), Is.EqualTo(-32602)); }
        [Test] public async Task NullResultsRetainEnvelope() { JsonRpc.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault; var n = await Call(Request("api.nil")); Assert.That(n.AsObject().ContainsKey("result"), Is.True); Assert.That(n["result"], Is.Null); }
        [Test] public async Task NotificationHasNoBody() { using (var r = await Post("{\"jsonrpc\":\"2.0\",\"method\":\"api.add\",\"params\":[2]}")) { Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.NoContent)); Assert.That(await r.Content.ReadAsStringAsync(), Is.Empty); } }
        [Test] public async Task BatchCorrelatesResultsAndOmitsNotifications() { var n = await Call("[" + Request("api.add", "[1]") + ",{\"jsonrpc\":\"2.0\",\"method\":\"api.nil\"}," + Request("api.add", "[2]", "2") + "]"); Assert.That(n.AsArray().Count, Is.EqualTo(2)); Assert.That(n[1]["id"].GetValue<int>(), Is.EqualTo(2)); }
        [Test] public async Task AllNotificationBatchHasNoBody() { using (var r = await Post("[{\"jsonrpc\":\"2.0\",\"method\":\"api.nil\"}]")) Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.NoContent)); }
        [TestCase("[]"), TestCase("[1]")]
        public async Task InvalidBatchItemsHaveErrors(string json) { var n = await Call(json); var e = n is JsonArray ? n[0] : n; Assert.That(e["error"]["code"].GetValue<int>(), Is.EqualTo(-32600)); }
        [Test] public async Task BatchLimitIsEnforced() { JsonRpc.Options.MaxBatchSize = 1; var n = await Call("[" + Request("api.nil") + "," + Request("api.nil") + "]"); Assert.That(n["error"]["code"].GetValue<int>(), Is.EqualTo(-32600)); }
        [Test] public async Task LegacyModeKeepsIdlessCalls() { JsonRpc.Options.StrictProtocol = false; var n = await Call("{\"method\":\"api.add\",\"params\":[3]}"); Assert.That(n["result"].GetValue<int>(), Is.EqualTo(4)); }
        [Test] public async Task QueryDoesNotDiscardPositionalBody() { var n = await Call(Request("api.add", "[4]"), "/?trace=1"); Assert.That(n["result"].GetValue<int>(), Is.EqualTo(5)); }
        [Test] public async Task LegacyGetWorks() { JsonRpc.Options.StrictProtocol = false; var n = JsonNode.Parse(await _Client.GetStringAsync("/?method=api.add&x=4&id=1")); Assert.That(n["result"].GetValue<int>(), Is.EqualTo(5)); }
        [Test] public async Task StandardMultipartWorks() { using (var f = new MultipartFormDataContent()) { f.Add(new StringContent(Request("api.add", "[4]"), Encoding.UTF8, "application/json"), "request"); using (var r = await _Client.PostAsync("/", f)) Assert.That(JsonNode.Parse(await r.Content.ReadAsStringAsync())["result"].GetValue<int>(), Is.EqualTo(5)); } }
        [TestCase("api.stream"), TestCase("api.wrapped")]
        public async Task PositionedStreamsHaveRemainingLength(string method) { using (var r = await Post(Request(method))) { Assert.That(r.Content.Headers.ContentLength, Is.EqualTo(4)); Assert.That(await r.Content.ReadAsStringAsync(), Is.EqualTo("cdef")); } }
        [Test] public async Task FinishedRunsOnFailureWithoutSuccessCallback() { var n = await Call(Request("api.fail")); Assert.That(n["error"]["message"].GetValue<string>(), Is.EqualTo("Internal error")); await WaitFor(() => Finished.ContainsKey("1")); Assert.That(Finished["1"], Is.EqualTo(1)); Assert.That(Completed, Is.Zero); }
        [Test] public async Task InstancesCreatedByLibraryAreDisposed() { await Call(Request("owned.ping")); await WaitFor(() => Owned.Disposals == 1); Assert.That(Owned.Disposals, Is.EqualTo(1)); }
        [Test] public async Task RequestContextsAreIsolated() { var results = await Task.WhenAll(Enumerable.Range(1, 30).Select(async n => { var response = await Call(Request("api.context", "[]", n.ToString())); return response["result"].GetValue<string>() == n.ToString(); })); Assert.That(results.All(x => x), Is.True); }
        [Test] public async Task OpenRpcRestartsWithoutDuplicateRegistration() { await JsonRpc.StopAsync(); JsonRpc.Start(); Assert.That(JsonRpc.ListeningAddresses.Length, Is.EqualTo(1)); Assert.Throws<InvalidOperationException>(() => JsonRpc.Start()); }
        [Test]
        public async Task DiscoveryValidatesAgainstOfficialMetaSchema()
        {
            var n = await Call(Request("rpc.discover"));
            var schema = JsonSchema.FromFile(Path.Combine(TestContext.CurrentContext.TestDirectory, "Schemas", "openrpc.json"));
            SchemaRegistry.RegisterNewSpecVersion(new Uri("https://meta.json-schema.tools/"), SpecVersion.Draft7);
            SchemaRegistry.Global.Register(new Uri("https://meta.json-schema.tools/"), JsonSchema.FromFile(Path.Combine(TestContext.CurrentContext.TestDirectory, "Schemas", "json-schema.json")));
            var result = schema.Evaluate(n["result"], new EvaluationOptions { OutputFormat = OutputFormat.List });
            Assert.That(result.IsValid, Is.True, JsonSerializer.Serialize(result));
        }
        private static async Task WaitFor(Func<bool> condition) { for (var i = 0; i < 100 && !condition(); i++) await Task.Delay(10); Assert.That(condition(), Is.True); }
        [JsonRpcClass("api")]
        public static class Api
        {
            public static int Executions;
            [JsonRpcMethod] public static int Add(int x) { Interlocked.Increment(ref Executions); return x + 1; }
            [JsonRpcMethod] public static object Nil() => null;
            [JsonRpcMethod] public static int Fail() => throw new Exception("private diagnostic");
            [JsonRpcMethod] public static async Task<string> Context(CancellationToken cancellationToken) { await Task.Delay(10, cancellationToken); return JsonRpcContext.Current.Request.Id.ToString(); }
            [JsonRpcMethod] public static Stream Stream() { var s = new MemoryStream(Encoding.UTF8.GetBytes("abcdef")); s.Position = 2; return s; }
            [JsonRpcMethod] public static JsonRpcStreamResult Wrapped() => new JsonRpcStreamResult(Stream(), "text/plain");
        }
        [JsonRpcClass("owned")] public class Owned : IDisposable { public static int Disposals; [JsonRpcMethod] public int Ping() => 1; public void Dispose() => Interlocked.Increment(ref Disposals); }
    }
}



