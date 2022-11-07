using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SimpleGraphQL
{
    [PublicAPI]
    public static class HttpUtils
    {
        private static ClientWebSocket _webSocket;

        /// <summary>
        /// Called when the websocket receives subscription data.
        /// </summary>
        public static event Action<string> SubscriptionDataReceived;

        public static HttpClient httpClient;
        private static Stopwatch _stopwatch;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void PreInit()
        {
            _webSocket?.Dispose();
            SubscriptionDataReceived = null;
            // Create a New HttpClient object.

            httpClient = new HttpClient();

        }

        /// <summary>
        /// For when the WebSocket needs to be disposed and reset.
        /// </summary>
        public static void Dispose()
        {
            _webSocket?.Dispose();
            _webSocket = null;
        }

        /// <summary>
        /// POST a query to the given endpoint url.
        /// </summary>
        /// <param name="url">The endpoint url.</param>
        /// <param name="request">The GraphQL request</param>
        /// <param name="authScheme">The authentication scheme to be used.</param>
        /// <param name="authToken">The actual auth token.</param>
        /// <param name="headers">Any headers that should be passed in</param>
        /// <param name="debug">Prints Debug information on request/response</param>
        /// <returns></returns>
        public static async Task<string> PostRequest(
            string url,
            Request request,
            Dictionary<string, string> headers = null,
            string authToken = null,
            string authScheme = null,
            bool debug = false
        )
        {
            var uri = new Uri(url);

            string payload = request.ToJson();
            
            var requestMessage = new HttpRequestMessage();

            if (authToken != null)
            {
                requestMessage.Headers.Add("Authorization", $"{authScheme} {authToken}");
            }

            requestMessage.Method = HttpMethod.Post;
            
            if (headers != null)
            {
                foreach (KeyValuePair<string, string> header in headers)
                {
                    requestMessage.Headers.Add(header.Key, header.Value);
                }
            }

            requestMessage.Content = new StringContent(payload,  Encoding.UTF8, "application/json");
            requestMessage.RequestUri = uri;

            try
            {
                if (debug)
                {
                    _stopwatch.Reset();
                    _stopwatch.Start();

                    Debug.Log("Firing SimpleGraphQL POST Request." +
                              "\n\nURL: \n " + requestMessage.RequestUri.ToString() + 
                              "\n\nHeaders: \n " + requestMessage.Headers.ToString() + 
                              "\n\nContent: \n" + payload);
                }
                
                var response = await httpClient.SendAsync(requestMessage);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (debug)
                {
                    _stopwatch.Stop();
                    Debug.Log("Received SimpleGraphQL POST Response." +
                              "\n\nTime in ms: \n " + _stopwatch.ElapsedMilliseconds + 
                              "\n\nHeaders: \n " + response.Headers.ToString() + 
                              "\n\nContent: \n" + responseContent);
                }

                return responseContent;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public static bool IsWebSocketReady() =>
            _webSocket?.State == WebSocketState.Connecting || _webSocket?.State == WebSocketState.Open;

        /// <summary>
        /// Connect to the GraphQL server. Call is necessary in order to send subscription queries via WebSocket.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="authScheme"></param>
        /// <param name="authToken"></param>
        /// <param name="headers"></param>
        /// <param name="protocol"></param>
        /// <returns></returns>
        public static async Task WebSocketConnect(
            string url,
            Dictionary<string, string> headers = null,
            string authToken = null,
            string authScheme = null,
            string protocol = "graphql-ws"
        )
        {
            url = url.Replace("http", "ws");

            var uri = new Uri(url);
            _webSocket = new ClientWebSocket();
            _webSocket.Options.AddSubProtocol(protocol);

            if (authToken != null)
            {
                _webSocket.Options.SetRequestHeader("Authorization", $"{authScheme} {authToken}");
            }

            _webSocket.Options.SetRequestHeader("Content-Type", "application/json");

            if (headers != null)
            {
                foreach (KeyValuePair<string, string> header in headers)
                {
                    _webSocket.Options.SetRequestHeader(header.Key, header.Value);
                }
            }

            try
            {
                Debug.Log("Websocket is connecting");
                await _webSocket.ConnectAsync(uri, CancellationToken.None);

                Debug.Log("Websocket is starting");
                // Initialize the socket at the server side
                await _webSocket.SendAsync(
                    new ArraySegment<byte>(
                        Encoding.UTF8.GetBytes(@"{""type"":""connection_init"",""payload"": {}}")
                    ),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );

                Debug.Log("Websocket is updating");
                // Start listening to the websocket for data.
                WebSocketUpdate();
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
        }

        /// <summary>
        /// Disconnect the websocket.
        /// </summary>
        /// <returns></returns>
        public static async Task WebSocketDisconnect()
        {
            if (_webSocket?.State != WebSocketState.Open)
            {
                Debug.LogError("Attempted to disconnect from a socket that was not open!");
                return;
            }

            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Socket closed.", CancellationToken.None);
        }

        /// <summary>
        /// Subscribe to a query.
        /// </summary>
        /// <param name="id">Used to identify the subscription. Must be unique per query.</param>
        /// <param name="request">The subscription query.</param>
        /// <returns>true if successful</returns>
        public static async Task<bool> WebSocketSubscribe(string id, Request request)
        {
            if (!IsWebSocketReady())
            {
                Debug.LogError("Attempted to subscribe to a query without connecting to a WebSocket first!");
                return false;
            }

            string json = JsonConvert.SerializeObject(
                new
                {
                    id,
                    type = "start",
                    payload = new
                    {
                        query = request.Query,
                        variables = request.Variables,
                        operationName = request.OperationName
                    }
                },
                Formatting.None,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                }
            );

            await _webSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );

            return true;
        }

        /// <summary>
        /// Unsubscribe from this query.
        /// </summary>
        /// <param name="id">Used to identify the subscription. Must be unique per query.</param>
        /// <returns></returns>
        public static async Task WebSocketUnsubscribe(string id)
        {
            if (!IsWebSocketReady())
            {
                Debug.LogError("Attempted to unsubscribe to a query without connecting to a WebSocket first!");
                return;
            }

            await _webSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes($@"{{""type"":""stop"",""id"":""{id}""}}")),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
        }

        private static async void WebSocketUpdate()
        {
            while (true)
            {
                ArraySegment<byte> buffer;
                buffer = WebSocket.CreateClientBuffer(1024, 1024);

                if (buffer.Array == null)
                {
                    throw new WebSocketException("Buffer array is null!");
                }

                WebSocketReceiveResult wsReceiveResult;
                var jsonBuild = new StringBuilder();

                do
                {
                    wsReceiveResult = await _webSocket.ReceiveAsync(buffer, CancellationToken.None);

                    jsonBuild.Append(Encoding.UTF8.GetString(buffer.Array, buffer.Offset, wsReceiveResult.Count));
                } while (!wsReceiveResult.EndOfMessage);

                var jsonResult = jsonBuild.ToString();
                if (jsonResult.IsNullOrEmpty()) return;

                JObject jsonObj;
                try
                {
                    jsonObj = JObject.Parse(jsonResult);
                }
                catch (JsonReaderException e)
                {
                    throw new ApplicationException(e.Message);
                }

                var msgType = (string)jsonObj["type"];
                switch (msgType)
                {
                    case "connection_error":
                        {
                            throw new WebSocketException("Connection error. Error: " + jsonResult);
                        }
                    case "connection_ack":
                        {
                            Debug.Log("Websocket connection acknowledged.");
                            continue;
                        }
                    case "data":
                    case "next":
                        {
                            JToken jToken = jsonObj["payload"];

                            if (jToken != null)
                            {
                                throw new WebSocketException("Connection error. Error: " + jsonResult);
                            }
                            continue;
                        }
                    case "error":
                        {
                            throw new WebSocketException("Handshake error. Error: " + jsonResult);
                        }
                    case "complete":
                        {
                            Debug.Log("Server sent complete, it's done sending data.");
                            break;
                        }
                    case "ka":
                        {
                            // stayin' alive, stayin' alive
                            continue;
                        }
                    case "subscription_fail":
                        {
                            throw new WebSocketException("Subscription failed. Error: " + jsonResult);
                        }
                    case "ping":
                        {
                            await _webSocket.SendAsync(
                                new ArraySegment<byte>(Encoding.UTF8.GetBytes($@"{{""type"":""pong""}}")),
                                WebSocketMessageType.Text,
                                true,
                                CancellationToken.None
                            );
                            continue;
                        }
                }

                break;
            }
        }
    }
}