using UnityEngine;

namespace Hairibar.Ragdoll
{
    /// <summary>
    /// Wrapper around a string that holds the name of a bone.
    /// </summary>
    [System.Serializable]
    public struct BoneName : System.IEquatable<BoneName>
    {
        [SerializeField]
        string name;


        public static implicit operator string(BoneName boneName)
        {
            return boneName.name;
        }

        public static implicit operator BoneName(string str)
        {
            return new BoneName(str);
        }

        public BoneName(string name)
        {
            this.name = name;
        }


        public static bool operator ==(BoneName a, BoneName b)
        {
            return a.name == b.name;
        }

        public static bool operator !=(BoneName a, BoneName b)
        {
            return !(a == b);
        }

        public bool Equals(BoneName other)
        {
            return this == other;
        }

        public override bool Equals(object obj)
        {
            return obj is BoneName other && Equals(other);
        }

        public override int GetHashCode()
        {
            return name != null ? name.GetHashCode() : 0;
        }

        public override string ToString()
        {
            return name;
        }
    }
}
