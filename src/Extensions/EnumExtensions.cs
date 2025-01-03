using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

public static class EnumExtensions
{
    public static T GetEnumValueFromEnumMember<T>(string value) where T : Enum
    {
        foreach (var field in typeof(T).GetFields())
        {
            var attribute = field.GetCustomAttribute<EnumMemberAttribute>();
            if (attribute != null && attribute.Value == value)
            {
                return (T)field.GetValue(null);
            }
        }

        throw new ArgumentException($"No matching enum value found for {value}");
    }
}