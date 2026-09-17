using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace HttpJsonRpc.Tests
{
    public partial class HttpTests
    {
        [Test]
        public async Task ErrorCustomizationFailuresStillProduceValidError()
        {
            var old = JsonRpc.Options.ErrorFactory;
            try
            {
                JsonRpc.Options.ErrorFactory = new BrokenErrorFactory();
                var response = await Call(Request("api.fail"));
                Assert.That(response["error"]["code"].GetValue<int>(), Is.EqualTo(-32603));
            }
            finally { JsonRpc.Options.ErrorFactory = old; }
        }
        [Test]
        public async Task FailedNotificationsDoNotProduceResponses()
        {
            using (var response = await Post("{\"jsonrpc\":\"2.0\",\"method\":\"api.fail\"}"))
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
                Assert.That(await response.Content.ReadAsStringAsync(), Is.Empty);
            }
            await WaitFor(() => Finished.ContainsKey("none"));
        }
        [Test]
        public async Task MixedBatchKeepsErrorsAndSuccesses()
        {
            var response = await Call("[" + Request("api.fail") + "," + Request("api.add", "[3]", "2") + "]");
            Assert.That(response[0]["error"]["code"].GetValue<int>(), Is.EqualTo(-32603));
            Assert.That(response[1]["result"].GetValue<int>(), Is.EqualTo(4));
        }
        [Test]
        public async Task StreamInsideBatchProducesPerItemError()
        {
            var response = await Call("[" + Request("api.stream") + "," + Request("api.nil", "[]", "2") + "]");
            Assert.That(response.AsArray().Count, Is.EqualTo(2));
            Assert.That(response[0]["error"]["code"].GetValue<int>(), Is.EqualTo(-32603));
        }
        [Test]
        public async Task ExplicitAsyncNameIsCallable()
        {
            var response = await Call(Request("unit.runAsyncJob"));
            Assert.That(response.AsObject().ContainsKey("result"), Is.True);
            Assert.That(response.AsObject().ContainsKey("error"), Is.False);
        }
        [Test]
        public async Task ValueTaskWorksOverHttp()
        {
            var response = await Call(Request("unit.value"));
            Assert.That(response["result"].GetValue<int>(), Is.EqualTo(42));
        }
        [Test]
        public async Task NoMiddlewareCallbackPreservesCalls()
        {
            await JsonRpc.StopAsync(); JsonRpc.Options.ConfigureApplication = null; JsonRpc.Start();
            _Client.BaseAddress = new Uri(JsonRpc.ListeningAddresses.Single());
            var response = await Call(Request("api.add", "[3]"));
            Assert.That(response["result"].GetValue<int>(), Is.EqualTo(4));
        }
        [Test]
        public async Task OpenRpcCanBeDisabledOnRestart()
        {
            await JsonRpc.StopAsync(); JsonRpc.Options.OpenRpc.IsEnabled = false; JsonRpc.Start();
            _Client.BaseAddress = new Uri(JsonRpc.ListeningAddresses.Single());
            var response = await Call(Request("rpc.discover"));
            Assert.That(response["error"]["code"].GetValue<int>(), Is.EqualTo(-32601));
        }
        [Test]
        public async Task BinaryUploadBodyRemainsReadable()
        {
            JsonRpc.Options.StrictProtocol = false;
            using (var content = new ByteArrayContent(new byte[] { 0, 128, 255, 7 }))
            using (var response = await _Client.PostAsync("/?method=extended.upload&id=1", content))
                Assert.That(JsonNode.Parse(await response.Content.ReadAsStringAsync())["result"].GetValue<string>(), Is.EqualTo("AID/Bw=="));
        }
        [Test]
        public async Task ClientCancellationReachesMethodAndCleanup()
        {
            ExtendedApi.CancelObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ExtendedApi.Started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (var cts = new CancellationTokenSource())
            using (var request = new HttpRequestMessage(HttpMethod.Post, "/") { Content = new StringContent(Request("extended.wait", "[]", "987"), Encoding.UTF8, "application/json") })
            {
                var response = _Client.SendAsync(request, cts.Token);
                Assert.That(await Task.WhenAny(ExtendedApi.Started.Task, Task.Delay(3000)), Is.SameAs(ExtendedApi.Started.Task));
                cts.Cancel();
                try { using (await response) { } } catch (OperationCanceledException) { }
                Assert.That(await Task.WhenAny(ExtendedApi.CancelObserved.Task, Task.Delay(3000)), Is.SameAs(ExtendedApi.CancelObserved.Task));
                await WaitFor(() => Finished.ContainsKey("987"));
            }
        }
        [Test]
        public async Task ObjectParametersMatchDiscoveryNames()
        {
            var response = await Call(Request("extended.dto", "{\"UPPER_NAME\":\"patient\"}"));
            Assert.That(response["result"].GetValue<string>(), Is.EqualTo("patient"));
            var discovery = await Call(Request("rpc.discover"));
            var method = discovery["result"]["methods"].AsArray().Single(x => x["name"].GetValue<string>() == "extended.dto");
            Assert.That(method["params"][0]["name"].GetValue<string>(), Is.EqualTo("UPPER_NAME"));
        }
        public class BrokenErrorFactory : JsonRpcErrorFactory { public override JsonRpcError CreateError(CreateErrorArgs args) => throw new Exception("broken customization"); }
        [JsonRpcClass("extended")]
        public static class ExtendedApi
        {
            public static TaskCompletionSource<bool> Started;
            public static TaskCompletionSource<bool> CancelObserved;
            [JsonRpcMethod] public static string Dto([JsonRpcParams] UnitTests.Contract input) => input.Name;
            [JsonRpcMethod]
            public static async Task<string> Upload()
            {
                using (var buffer = new MemoryStream())
                {
                    await JsonRpcContext.Current.HttpContext.Request.Body.CopyToAsync(buffer);
                    return Convert.ToBase64String(buffer.ToArray());
                }
            }
            [JsonRpcMethod]
            public static async Task Wait(CancellationToken cancellationToken)
            {
                Started.TrySetResult(true);
                try { await Task.Delay(Timeout.Infinite, cancellationToken); }
                catch (OperationCanceledException) { CancelObserved.TrySetResult(true); throw; }
            }
        }
    }
}
