using System.Globalization;
using UnityEngine;

namespace DoodleUp.Runtime
{
    public static class Du02LogFormat
    {
        public static string Float(float value) => value.ToString("F6", CultureInfo.InvariantCulture);

        public static string Vector(Vector3 value)
        {
            return $"({Float(value.x)},{Float(value.y)},{Float(value.z)})";
        }

        public static string Quaternion(Quaternion value)
        {
            return $"({Float(value.x)};{Float(value.y)};{Float(value.z)};{Float(value.w)})";
        }
    }
}
