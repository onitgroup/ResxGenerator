using System.ComponentModel;

namespace ResxGenerator.VSExtension.Infrastructure.Modernization
{
    public static class EnumExtensions
    {
        public static T? ParseWithDescription<T>(this string? description) where T : struct, Enum
        {
            if (description is null)
            {
                return null;
            }

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
            var member = typeof(T).GetMember(enumerationValue.ToString());
            if (member.Length != 0)
            {
                var customAttributes = member[0].GetCustomAttributes(typeof(DescriptionAttribute), inherit: false);
                if (customAttributes.Length != 0)
                {
                    return ((DescriptionAttribute)customAttributes[0]).Description;
                }
            }

            return enumerationValue.ToString();
        }
    }
}
