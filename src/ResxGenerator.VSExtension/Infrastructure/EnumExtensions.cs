using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ResxGenerator.VSExtension.Infrastructure
{
    public static class EnumExtensions
    {
        public static T? GetValueFromDescription<T>(this string? description) where T : struct, Enum
        {
            if (description is null) return null;
            foreach (var field in typeof(T).GetFields())
            {
                if (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attribute)
                {
                    if (attribute.Description == description)
                        return (T)field.GetValue(null);
                }
                else
                {
                    if (field.Name == description)
                        return (T)field.GetValue(null);
                }
            }

            return null;
        }

        public static string GetDescription<T>(this T enumerationValue) where T : struct, Enum
        {
            MemberInfo[] member = typeof(T).GetMember(enumerationValue.ToString());
            if (member.Length != 0)
            {
                object[] customAttributes = member[0].GetCustomAttributes(typeof(DescriptionAttribute), inherit: false);
                if (customAttributes.Length != 0)
                {
                    return ((DescriptionAttribute)customAttributes[0]).Description;
                }
            }

            return enumerationValue.ToString();
        }
    }
}
