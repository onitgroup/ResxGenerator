using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResxGenerator.VSExtension.Translators
{
    public interface ITranslatorSettings
    {
    }

    public interface ITranslator
    {
        public Task<Dictionary<string, string>> TranslateAsync(ITranslatorSettings? settingsInterface, CultureInfo source, CultureInfo target, IEnumerable<string> values);
    }
}
