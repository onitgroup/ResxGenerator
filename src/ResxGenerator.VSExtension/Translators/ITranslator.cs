using System.Globalization;

namespace ResxGenerator.VSExtension.Translators
{
    public interface ITranslatorSettings;

    public interface ITranslator
    {
        public Task<Dictionary<string, string?>> TranslateAsync(ITranslatorSettings? settingsInterface, CultureInfo source, CultureInfo target, IEnumerable<string> values);
    }
}
