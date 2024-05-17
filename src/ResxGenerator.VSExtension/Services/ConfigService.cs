using Microsoft;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Documents;
using Microsoft.VisualStudio.ProjectSystem.Query;
using ResxGenerator.VSExtension.Infrastructure;
using ResxGenerator.VSExtension.Translators;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResxGenerator.VSExtension.Services
{
#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW

    public class ConfigService
    {
        public const string CONFIG_FILE = "resx-generator.json";
        private readonly VisualStudioExtensibility _extensibility;
        private readonly Task _initializationTask; // probably not needed
        private OutputWindow? _output;

        private readonly JsonSerializerOptions _indentedOptions = new()
        {
            WriteIndented = true,
        };

        public ConfigService(VisualStudioExtensibility extensibility)
        {
            _extensibility = Requires.NotNull(extensibility, nameof(extensibility));
            _initializationTask = Task.Run(InitializeAsync);
        }

        private async Task InitializeAsync()
        {
            _output = await Utilities.GetOutputWindowAsync(_extensibility);
            Assumes.NotNull(_output);
        }

        private FileInfo GetConfigFilePath(IProjectSnapshot snapshot)
        {
            return new FileInfo(Path.Combine(Path.GetDirectoryName(snapshot.Path)!, CONFIG_FILE));
        }

        public bool Exists(IProjectSnapshot snapshot)
        {
            return GetConfigFilePath(snapshot).Exists;
        }

        public async Task AddDefaultConfigFileAsync(IProjectSnapshot snapshot)
        {
            var configFile = GetConfigFilePath(snapshot);
            if (configFile.Exists)
            {
                await _output.WriteToOutputAsync("Configuration file already exists.");
                return;
            }
            var config = Config.Default;
            using var writeStream = new FileStream(configFile.FullName, FileMode.OpenOrCreate);
            await JsonSerializer.SerializeAsync(writeStream, config, _indentedOptions);
        }

        public async Task<Config> GetAsync(IProjectSnapshot snapshot)
        {
            var configFile = GetConfigFilePath(snapshot);
            if (configFile.Exists == false)
            {
                await _output.WriteToOutputAsync("No configuration file found.");
                return Config.Default;
            }

            Config? config;
            try
            {
                using var readStream = new FileStream(configFile.FullName, FileMode.Open);
                config = await JsonSerializer.DeserializeAsync<Config>(readStream);
                if (config is null)
                {
                    await _output.WriteToOutputAsync("Unable to read the configuration file, the default will be used");
                    config = Config.Default;
                }
            }
            catch (Exception)
            {
                await _output.WriteToOutputAsync("Error while attempting to read the configuration file, the default will be used");
                config = Config.Default;
            }

            return config;
        }

        public async Task<ITranslatorSettings?> GetTranslatorConfigAsync(IProjectSnapshot snapshot)
        {
            var config = await GetAsync(snapshot);

            return config.TranslatorService switch
            {
                TranslatorService.ChatGPT => config.ChatGPT ?? throw new NullReferenceException($"No settings found for translator {config.Translator}"),
                TranslatorService.GoogleTranslate => null,
                _ => throw new InvalidOperationException("No translator service found"),
            };
        }

        public async Task UpdateAsync(IProjectSnapshot snapshot, Config config)
        {
            var configFile = GetConfigFilePath(snapshot);
            using var writeStream = new FileStream(configFile.FullName, FileMode.OpenOrCreate);
            await JsonSerializer.SerializeAsync(writeStream, config, _indentedOptions);
        }
    }

    public enum TranslatorService
    {
        [Description("ChatGPT")]
        ChatGPT = 0,

        [Description("GoogleTranslate")]
        GoogleTranslate = 1
    }

    public class Config
    {
        public string? ResourceName { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? ValidationComment { get; set; }

        public bool WriteKeyAsValue { get; set; }

        public IEnumerable<string> Languages { get; set; } = [];

        [JsonIgnore]
        internal IEnumerable<CultureInfo> Cultures => Languages.Select(x => new CultureInfo(x));

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Translator { get; set; }

        [JsonIgnore]
        internal TranslatorService? TranslatorService => Translator.GetValueFromDescription<TranslatorService>();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool OverwriteTranslations { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public ChatGPTTranslator.Settings? ChatGPT { get; set; }

        public static Config Default => new()
        {
            ResourceName = "SharedResource",
            WriteKeyAsValue = true,
            Languages = [],
            Translator = string.Empty,
        };
    }

#pragma warning restore VSEXTPREVIEW_OUTPUTWINDOW
}