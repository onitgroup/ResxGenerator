using Microsoft;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Documents;
using ResxGenerator.VSExtension.Infrastructure;
using ResxGenerator.VSExtension.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ResxGenerator.VSExtension.Translators
{
#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW

    public class GoogleTranslateTranslator : ITranslator
    {
        private readonly ConfigService _config;
        private readonly VisualStudioExtensibility _extensibility;
        private readonly Task _initializationTask; // probably not needed
        private OutputWindow? _output;

        public GoogleTranslateTranslator(ConfigService config, VisualStudioExtensibility extensibility)
        {
            _config = config;
            _extensibility = Requires.NotNull(extensibility, nameof(extensibility));
            _initializationTask = Task.Run(InitializeAsync);
        }

        private async Task InitializeAsync()
        {
            _output = await Utilities.GetOutputWindowAsync(_extensibility);
            Assumes.NotNull(_output);
        }

        public async Task<Dictionary<string, string>> TranslateAsync(ITranslatorSettings? _, CultureInfo source, CultureInfo target, IEnumerable<string> values)
        {
            var res = new Dictionary<string, string>();
            var builder = new StringBuilder();
            builder.Append("https://clients5.google.com/translate_a/t");
            builder.Append("?client=dict-chrome-ex");
            builder.Append($"&sl={source.TwoLetterISOLanguageName}");
            builder.Append($"&tl={target.TwoLetterISOLanguageName}");

            using var client = new HttpClient();
            var partialUrl = builder.ToString();
            foreach (var (value, index) in values.Select((x, i) => (x, i + 1)))
            {
                await _output.WriteToOutputAsync($"Translating: {(index) / (decimal)values.Count() * 100:.00}%");
                var response = await client.GetAsync(partialUrl + $"&q={value}");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                res[value] = content.Substring(2, content.Length - 4);
            }

            await _output.WriteToOutputAsync("Translations done.");

            return res;
        }
    }

#pragma warning restore VSEXTPREVIEW_OUTPUTWINDOW
}