using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace SimpleGraphQL
{
    public class GraphQLResponse
    {
        public int statusCode;
        public string requestName;
        public string requestURL;
        public string requestAimlabsID;
        public string responseContent;
        public long totalTime;
    }

    [PublicAPI]
    public static class HttpUtils
    {
        private static ClientWebSocket _webSocket;

        /// <summary>
        /// Called when the websocket receives subscription data.
        /// </summary>
        public static event Action<string> SubscriptionDataReceived;
        public static Dictionary<string, Action<string>> SubscriptionDataReceivedPerChannel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void PreInit()
        {
            _webSocket?.Dispose();
            SubscriptionDataReceived = null;
            SubscriptionDataReceivedPerChannel = new Dictionary<string, Action<string>>();
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
        public static async Task<GraphQLResponse> PostRequest(
            string url,
            Request request,
            JsonSerializerSettings serializerSettings = null,
            Dictionary<string, string> headers = null,
            string authToken = null,
            string authScheme = null,
            bool debug = false,
            bool silent = false
        )
        {
            var uri = new Uri(url);
            
            string payloadString = await Task.Run(() => request.ToJson(false, serializerSettings));
            byte[] payload = Encoding.UTF8.GetBytes(payloadString);
            
            string payloadStringDisplayable = payloadString.Replace("\\r\\n", "\n");

            using (UnityWebRequest webRequest = new UnityWebRequest(uri, "POST"))
            { 
                webRequest.uploadHandler = new UploadHandlerRaw(payload);
                webRequest.downloadHandler = new DownloadHandlerBuffer();

                // ADDING HEADERS ----------------------------------------------
                var webRequestHeaders = new Dictionary<string, string>();
                if (authToken != null)
                {
                    webRequestHeaders.Add("Authorization", $"{authScheme} {authToken}");
                }
                webRequestHeaders.Add("Content-Type", "application/json");
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        webRequestHeaders.Add(header.Key, header.Value);
                    }
                }
                foreach (var header in webRequestHeaders)
                {
                    webRequest.SetRequestHeader(header.Key, header.Value);
                }

                //Send the request then wait here until it returns
                try
                {
                    if (debug && !silent)
                    {
                        Debug.Log($"Firing SimpleGraphQL POST Request {request.OperationName}" +
                                  $"\n\nThread: {Thread.CurrentThread.ManagedThreadId}" +
                                  "\n\nURL: \n " + uri.ToString() +
                                  "\n\nHeaders: \n " + webRequestHeaders.PrintDictionary() +
                                  $"\n\nThread: {Thread.CurrentThread.ManagedThreadId}" +
                                  "\n\nContent: \n" + payloadStringDisplayable);
                    }

                    var startTime = DateTime.Now;
                    var operation = webRequest.SendWebRequest();

                    TaskCompletionSource<bool> requestCompleted = new();
                    operation.completed += _ => requestCompleted.TrySetResult(true);

                    await requestCompleted.Task;

                    if (webRequest.result == UnityWebRequest.Result.ConnectionError)
                    {
                        Debug.LogError("Error While Sending: " + webRequest.error);
                        throw new UnityWebRequestException(webRequest);
                    }

                    var executionTime = DateTime.Now - startTime;
                    var responseContent = webRequest.downloadHandler.text;
                    var aimlabsRequestHeader = webRequest.GetResponseHeaders().FirstOrDefault(header => header.Key.StartsWith("Aimlabs-Request-Id"));
                    var aimlabsRequestID = (aimlabsRequestHeader.Value != null) ? aimlabsRequestHeader.Value : "(ID NOT FOUND)";

                    if (debug && !silent)
                    {
                        Debug.Log($"Received SimpleGraphQL POST Response {request.OperationName}" +
                                  $"\n\nThread: {Thread.CurrentThread.ManagedThreadId}" +
                                  "\n\nTime in ms: \n " + executionTime.Milliseconds +
                                  "\n\nHeaders: \n " + webRequest.GetResponseHeaders().ToString() +
                                  "\n\nContent: \n" + responseContent +
                                  "\n\nRequest URL: \n " + uri.ToString() +
                                  "\n\nRequest Headers: \n " + webRequestHeaders.PrintDictionary() +
                                  "\n\nRequest Content: \n" + payloadStringDisplayable);
                    }
                    else if (!silent && request?.OperationName != null && webRequest.GetResponseHeaders() != null)
                    {
                        Debug.Log($"Received GraphQL Response for {request.OperationName}, id: {aimlabsRequestID}");
                    }

                    return new GraphQLResponse()
                           {
                               statusCode = (int)webRequest.responseCode,
                               requestName = request.OperationName,
                               requestURL = uri.ToString(),
                               requestAimlabsID = aimlabsRequestID,
                               responseContent = responseContent,
                               totalTime = executionTime.Milliseconds
                           };

                }
                catch (Exception e)
                {
#if UNITY_EDITOR
                    Debug.LogError("[SimpleGraphQL] " + e);
#endif
                    throw new UnityWebRequestException(webRequest);
                }
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

            var payload = new Dictionary<string, string>();

            if(protocol == "graphql-transport-ws") {
              payload["content-type"] = "application/json";
            } else {
              _webSocket.Options.SetRequestHeader("Content-Type", "application/json");
            }

            if (authToken != null) {
                if(protocol == "graphql-transport-ws") {
                    // set Authorization as payload
                    payload["Authorization"] = $"{authScheme} {authToken}";
                } else {
                    _webSocket.Options.SetRequestHeader("Authorization", $"{authScheme} {authToken}");
                }
            }


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

                var json = JsonConvert.SerializeObject(
                    new
                    {
                        type = "connection_init",
                        payload = payload
                    },
                    Formatting.None,
                    new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    }
                );

                Debug.Log("Websocket is starting");
                // Initialize the socket at the server side
                await _webSocket.SendAsync(
                    new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)),
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
                    type = _webSocket.SubProtocol == "graphql-transport-ws" ? "subscribe" : "start",
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

            var type = _webSocket.SubProtocol == "graphql-transport-ws" ? "complete" : "stop";

            await _webSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes($@"{{""type"":""{type}"",""id"":""{id}""}}")),
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
                var id = (string)jsonObj["id"];
                switch (msgType)
                {
                    case "connection_error":
                        {
                            throw new WebSocketException("Connection error. Error: " + jsonResult);
                        }
                    case "connection_ack":
                    {
                        Debug.Log($"Websocket connection acknowledged ({id}).");
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
        
        public static string PrintDictionary<TKey, TValue>(this Dictionary<TKey, TValue> dict)
        {
            return string.Join("\n", dict.Select(pair => string.Format("{0} - {1}", pair.Key, pair.Value)));
        }
    }
}
