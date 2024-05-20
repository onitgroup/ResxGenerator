using Microsoft;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Documents;
using ResxGenerator.VSExtension.Infrastructure;
using ResxGenerator.VSExtension.Services;
using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace ResxGenerator.VSExtension.Translators
{
#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW

    public class GoogleTranslateTranslator : ITranslator
    {
        private const int CHUNK_SIZE = 50;
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

        public async Task<Dictionary<string, string?>> TranslateAsync(ITranslatorSettings? _, CultureInfo source, CultureInfo target, IEnumerable<string> values)
        {
            var res = new Dictionary<string, string?>();

            foreach (var value in values)
            {
                res[value] = null;
            }

            var baseUrl = $"https://clients5.google.com/translate_a/t?client=dict-chrome-ex&sl={source.TwoLetterISOLanguageName}&tl={target.TwoLetterISOLanguageName}";

            try
            {
                int c = 0;
                while (c < values.Count()) // iter in chunk to avoid long urls, the parameters are all in query string
                {
                    await _output.WriteToOutputAsync($"Translating: {(c + 1) / (decimal)values.Count() * 100:0.00}%");
                    var sub = values.Skip(c).Take(CHUNK_SIZE);

                    var builder = new StringBuilder(baseUrl);

                    foreach (var value in sub)
                    {
                        builder.Append($"&q={value}");
                    }

                    using (var client = new HttpClient())
                    {
                        var response = await client.GetAsync(builder.ToString());
                        response.EnsureSuccessStatusCode();
                        var translations = await JsonSerializer.DeserializeAsync<List<string>>(await response.Content.ReadAsStreamAsync());

                        foreach (var (value, index) in sub.Select((x, i) => (x, i)))
                        {
                            res[value] = translations?.ElementAtOrDefault(index);
                        }
                    }

                    c += CHUNK_SIZE;
                }
                await _output.WriteToOutputAsync("Translations done.");
            }
            catch (Exception)
            {
                await _output.WriteToOutputAsync("Unable to get the translations, empty values will be used.");
            }

            if (res.ContainsKey(string.Empty))
            {
                res.Remove(string.Empty);
            }

            return res;
        }
    }

#pragma warning restore VSEXTPREVIEW_OUTPUTWINDOW
}