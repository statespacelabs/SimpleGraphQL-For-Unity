using System;
using System.Runtime.Serialization;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Scripting;

namespace SimpleGraphQL
{
    [PublicAPI]
    [Serializable]
    public class Response<T>
    {
        [SerializeField]
        private T data;

        [CanBeNull] 
        [SerializeField]
        private Error[] errors;

        [DataMember(Name = "data")]
        public T Data
        {
            get => data;
            set => data = value;
        }

        [DataMember(Name = "errors")]
        [CanBeNull]
        public Error[] Errors
        {
            get => errors;
            set => errors = value;
        }

        [Preserve] // Ensures it survives code-stripping
        public Response()
        {
        }
    }

    [PublicAPI]
    [Serializable]
    public class Error
    {
        [DataMember(Name = "message")]
        public string Message { get; set; }

        [DataMember(Name = "locations")]
        [CanBeNull]
        public Location[] Locations { get; set; }

        [DataMember(Name = "path")]
        [CanBeNull]
        public object[] Path { get; set; } // Path objects can be either integers or strings

        [Preserve] // Ensures it survives code-stripping
        public Error()
        {
        }
    }

    [PublicAPI]
    [Serializable]
    public class Location
    {
        [DataMember(Name = "line")]
        public int Line { get; set; }

        [DataMember(Name = "column")]
        public int Column { get; set; }

        [Preserve] // Ensures it survives code-stripping
        public Location()
        {
        }
    }
}