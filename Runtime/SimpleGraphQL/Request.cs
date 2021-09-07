using System;
using System.Text;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace SimpleGraphQL
{
    [Serializable]
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class Request
    {
        public string Query { get; set; }

        [CanBeNull]
        public string OperationName { get; set; }

        public object Variables { get; set; }

        public override string ToString()
        {
            return $"GraphQL Request:\n{this.ToJson(true)}";
        }
    }



    [PublicAPI]
    public static class RequestExtensions
    {
        public static byte[] ToBytes(this Request request)
        {
            return Encoding.UTF8.GetBytes(request.ToJson());
        }

        public static string ToJson(this Request request, bool prettyPrint = false)
        {
            // JsonConverter[] converters = new JsonConverter[] {
            // new Vec2Conv(),
            // new Vec2NullableConv(),
            // new Vec3Conv(),
            // new Vec3NullableConv(),
            // new Vec4Conv(),
            // new ColorNullableConv()
            // };

            // return JsonConvert.SerializeObject
            // (request, prettyPrint ? Formatting.Indented : Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Converters = converters });

             return JsonConvert.SerializeObject
            (request, prettyPrint ? Formatting.Indented : Formatting.None);
        }
    }
}