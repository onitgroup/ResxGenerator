using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;

namespace LocalizerExtensions
{
    public static partial class HtmlLocalizerExtensions
    {
        public static LocalizedHtmlString Pluralize(this IHtmlLocalizer localizer, string singularOrPlural, int count, params object[] arguments)
        {
            var splitted = singularOrPlural.Split('|');
            if (splitted.Length != 2)
                throw new ArgumentException(nameof(singularOrPlural));

            bool isSingular = count == 1;
            if (isSingular)
            {
                return localizer[splitted[0], arguments];
            }
            else
            {
                return localizer[splitted[1], arguments];
            }
        }

        public static LocalizedHtmlString Pluralize(this IHtmlLocalizer localizer, string singular, string plural, int count, params object[] arguments)
        {
            bool isSingular = count == 1;
            if (isSingular)
            {
                return localizer[singular, arguments];
            }
            else
            {
                return localizer[plural, arguments];
            }
        }

        public static LocalizedHtmlString Genderize(this IHtmlLocalizer localizer, string maleOrFemale, bool isMale, params object[] arguments)
        {
            var splitted = maleOrFemale.Split('|');
            if (splitted.Length != 2)
                throw new ArgumentException(nameof(maleOrFemale));

            if (isMale)
            {
                return localizer[splitted[0], arguments];
            }
            else
            {
                return localizer[splitted[1], arguments];
            }
        }

        public static LocalizedHtmlString Genderize(this IHtmlLocalizer localizer, string male, string female, bool isMale, params object[] arguments)
        {
            if (isMale)
            {
                return localizer[male, arguments];
            }
            else
            {
                return localizer[female, arguments];
            }
        }
    }

    public static partial class StringLocalizerExtensions
    {
        public static LocalizedString Pluralize(this IStringLocalizer localizer, string singularOrPlural, int count, params object[] arguments)
        {
            var splitted = singularOrPlural.Split('|');
            if (splitted.Length != 2)
                throw new ArgumentException(nameof(singularOrPlural));

            bool isSingular = count == 1;
            if (isSingular)
            {
                return localizer[splitted[0], arguments];
            }
            else
            {
                return localizer[splitted[1], arguments];
            }
        }

        public static LocalizedString Pluralize(this IStringLocalizer localizer, string singular, string plural, int count, params object[] arguments)
        {
            bool isSingular = count == 1;
            if (isSingular)
            {
                return localizer[singular, arguments];
            }
            else
            {
                return localizer[plural, arguments];
            }
        }

        public static LocalizedString Genderize(this IStringLocalizer localizer, string maleOrFemale, bool isMale, params object[] arguments)
        {
            var splitted = maleOrFemale.Split('|');
            if (splitted.Length != 2)
                throw new ArgumentException(nameof(maleOrFemale));

            if (isMale)
            {
                return localizer[splitted[0], arguments];
            }
            else
            {
                return localizer[splitted[1], arguments];
            }
        }

        public static LocalizedString Genderize(this IStringLocalizer localizer, string male, string female, bool isMale, params object[] arguments)
        {
            if (isMale)
            {
                return localizer[male, arguments];
            }
            else
            {
                return localizer[female, arguments];
            }
        }
    }
}