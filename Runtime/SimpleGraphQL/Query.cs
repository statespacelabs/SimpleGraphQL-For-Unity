using System;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;

namespace SimpleGraphQL
{
    [PublicAPI]
    [Serializable]
    public class Query
    {
        /// <summary>
        /// The filename that this query is located in.
        /// This is mostly used for searching and identification purposes, and is not
        /// necessarily needed for dynamically created queries.
        /// </summary>
        [CanBeNull]
        public string FileName;

        /// <summary>
        /// The operation name of this query.
        /// It may be null, in which case it should be the only anonymous query in this file.
        /// </summary>
        [CanBeNull]
        public string OperationName;

        /// <summary>
        /// The type of query this is.
        /// </summary>
        public OperationType OperationType;

        /// <summary>
        /// The actual query itself.
        /// </summary>
        public string Source;

        /// <summary>
        /// Fragments used by this query
        /// </summary>
        public string[] Fragments;
        
        public override string ToString()
        {
            return $"{FileName}:{OperationName}:{OperationType}";
        }
    }

    [PublicAPI]
    public static class QueryExtensions
    {
        public static Request ToRequest(this Query query, object variables = null, IEnumerable<Fragment> fragments = null)
        {
            StringBuilder querySource = new StringBuilder(query.Source);

            if (fragments != null)
            {
                foreach (var fragment in fragments)
                {
                    querySource.AppendLine(fragment?.Source);
                }
            }
            
            return new Request
                   {
                       Query = querySource.ToString(),
                       Variables = variables,
                       OperationName = query.OperationName,
                   };
        }
    }

    [PublicAPI]
    [Serializable]
    public enum OperationType
    {
        Query,
        Mutation,
        Subscription
    }
}