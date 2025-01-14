using System;
using JetBrains.Annotations;
using UnityEngine;

namespace SimpleGraphQL
{
    [PublicAPI]
    [Serializable]
    public class Fragment
    {
        /// <summary>
        /// The name of the fragment.
        /// </summary>
        [CanBeNull]
        public string Name;

        /// <summary>
        /// The type the fragment is selecting from.
        /// </summary>
        public string TypeCondition;

        /// <summary>
        /// The actual fragment itself.
        /// </summary>
        [TextArea]
        public string Source;

        public override string ToString()
        {
            return $"fragment {Name} on {TypeCondition}";
        }
        
        protected bool Equals(Fragment other)
        {
            return Name == other.Name;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Fragment)obj);
        }

        public override int GetHashCode()
        {
            return (Name != null ? Name.GetHashCode() : 0);
        }
    }

}