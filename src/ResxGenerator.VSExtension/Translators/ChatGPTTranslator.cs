using Microsoft;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Documents;
using ResxGenerator.VSExtension.Infrastructure;
using ResxGenerator.VSExtension.Services;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ResxGenerator.VSExtension.Translators
{
#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW

    public class ChatGPTTranslator : ITranslator
    {
        private const int CHUNK_SIZE = 50;
        public const string SOURCE_PLACEHOLDER = "{sourceLanguage}";
        public const string TARGET_PLACEHOLDER = "{targetLanguage}";
        private readonly VisualStudioExtensibility _extensibility;
        private readonly Task _initializationTask; // probably not needed
        private OutputWindow? _output;

        private readonly JsonSerializerOptions _camelCaseOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public ChatGPTTranslator(VisualStudioExtensibility extensibility)
        {
            _extensibility = Requires.NotNull(extensibility, nameof(extensibility));
            _initializationTask = Task.Run(InitializeAsync);
        }

        private async Task InitializeAsync()
        {
            _output = await Utilities.GetOutputWindowAsync(_extensibility);
            Assumes.NotNull(_output);
        }

        public async Task<Dictionary<string, string?>> TranslateAsync(ITranslatorSettings? settingsInterface, CultureInfo source, CultureInfo target, IEnumerable<string> values)
        {
            if (settingsInterface is null) throw new ArgumentNullException(nameof(settingsInterface));
            var settings = (Settings)settingsInterface;

            var res = new Dictionary<string, string?>();
            var baseUrl = $"{settings.Prompt}: "
                .Replace(SOURCE_PLACEHOLDER, source.Name)
                .Replace(TARGET_PLACEHOLDER, target.Name);

            try
            {
                int c = 0;
                while (c < values.Count()) // iter in chunk to avoid long urls, the parameters are all in query string
                {
                    await _output.WriteToOutputAsync($"Translating: {(c + 1) / (decimal)values.Count() * 100:0.00}%");
                    var sub = values.Skip(c).Take(CHUNK_SIZE);

                    var request = new TranslateRequest
                    {
                        Model = settings.Model,
                        Messages = [
                            new Message{
                                Role = "user",
                                Content = baseUrl + JsonSerializer.Serialize(sub.ToDictionary(k => k, v => v))
                            }
                        ]
                    };

                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.Token);
                        var content = new StringContent(JsonSerializer.Serialize(request, _camelCaseOptions));
                        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                        var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
                        response.EnsureSuccessStatusCode();
                        var data = await JsonSerializer.DeserializeAsync<TranslateResponse>(await response.Content.ReadAsStreamAsync(), _camelCaseOptions);
                        if (data is null || data.Choices.Count() == 0)
                        {
                            throw new NullReferenceException("Unable to get a valid response from ChatGPT");
                        }

                        var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(data.Choices.First().Message.Content);

                        if (translations is not null)
                        {
                            foreach (var entry in translations)
                            {
                                res[entry.Key] = entry.Value;
                            }
                        }
                    }

                    c += CHUNK_SIZE;
                }

                await _output.WriteToOutputAsync("Translations done.");
            }
            catch (Exception ex)
            {
                await _output.WriteToOutputAsync("Unable to get the translations, empty values will be used.");
            }

            return res;
        }

        public class Settings : ITranslatorSettings
        {
            public string Token { get; set; } = string.Empty;

            public string Model { get; set; } = string.Empty;

            public string Prompt { get; set; } = string.Empty;
        }

        public class TranslateRequest
        {
            public string Model { get; set; } = string.Empty;

            public IEnumerable<Message> Messages { get; set; } = [];
        }

        public class TranslateResponse
        {
            public string Id { get; set; } = string.Empty;

            public string Object { get; set; } = string.Empty;

            public long Created { get; set; }

            public string Model { get; set; } = string.Empty;

            public IEnumerable<Choice> Choices { get; set; } = [];

            public class Choice
            {
                public int Index { get; set; }

                public Message Message { get; set; } = null!;
            }
        }

        public class Message
        {
            public string Role { get; set; } = string.Empty;

            public string Content { get; set; } = string.Empty;
        }
    }

#pragma warning restore VSEXTPREVIEW_OUTPUTWINDOW
}