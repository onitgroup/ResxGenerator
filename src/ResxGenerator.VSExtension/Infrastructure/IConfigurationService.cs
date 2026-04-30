using Microsoft;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Documents;
using Microsoft.VisualStudio.ProjectSystem.Query;
using ResxGenerator.VSExtension.Translators;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace ResxGenerator.VSExtension.Infrastructure
{
    public interface IConfigurationService
    {
        bool TryGet(IProjectSnapshot snapshot, [NotNullWhen(true)] out Config? config);

        void AddDefault(IProjectSnapshot snapshot);

        void Update(IProjectSnapshot snapshot, Config config);

        bool TryGetNeutralCulture(IProjectSnapshot snapshot, [NotNullWhen(true)] out CultureInfo? neutralLanguage);
    }

    public class ConfigurationService : IConfigurationService
    {
        private const string CONFIG_FILE_NAME = "resx-generator.json";

        private readonly JsonSerializerOptions _indentedOptions = new()
        {
            WriteIndented = true,
        };

        private FileInfo GetConfigFileInfo(IProjectSnapshot snapshot)
        {
            return new FileInfo(Path.Combine(Path.GetDirectoryName(snapshot.Path)!, CONFIG_FILE_NAME));
        }

        public void AddDefault(IProjectSnapshot snapshot)
        {
            var configFile = GetConfigFileInfo(snapshot);
            if (configFile.Exists)
            {
                throw new InvalidOperationException($"Config file already exists at {configFile.FullName}");
            }
            var config = Config.Default;
            using var writeStream = new FileStream(configFile.FullName, FileMode.OpenOrCreate);
            JsonSerializer.Serialize(writeStream, config, _indentedOptions);
        }

        public bool TryGet(IProjectSnapshot snapshot, [NotNullWhen(true)] out Config? config)
        {
            var configFile = GetConfigFileInfo(snapshot);
            if (!configFile.Exists)
            {
                config = null;
                return false;
            }

            using var readStream = new FileStream(configFile.FullName, FileMode.Open);
            config = JsonSerializer.Deserialize<Config>(readStream);
            return config is not null;
        }

        public void Update(IProjectSnapshot snapshot, Config config)
        {
            var configFile = GetConfigFileInfo(snapshot);
            using var writeStream = new FileStream(configFile.FullName, FileMode.OpenOrCreate);
            JsonSerializer.Serialize(writeStream, config, _indentedOptions);
        }

        public bool TryGetNeutralCulture(IProjectSnapshot snapshot, [NotNullWhen(true)] out CultureInfo? neutralLanguage)
        {
            if (string.IsNullOrEmpty(snapshot.Path))
            {
                neutralLanguage = null;
                return false;
            }

            var document = XDocument.Load(snapshot.Path);
            neutralLanguage = document.Root?
                .Descendants("NeutralLanguage")
                .Select(x => new CultureInfo(x.Value))
                .FirstOrDefault();

            return neutralLanguage is not null;
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
        public const string DEFAULT_RESOURCE_NAME = "SharedResource";

        [Obsolete("ResourceName is deprecated, use DefaultResourceName instead")]
        public string? ResourceName { get; set; }

        public required string DefaultResourceName { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? ValidationComment { get; set; }

        public bool WriteKeyAsValue { get; set; }

        public IEnumerable<string> Languages { get; set; } = [];

        [JsonIgnore]
        internal IEnumerable<CultureInfo> Cultures => Languages.Select(x => new CultureInfo(x));

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Translator { get; set; }

        [JsonIgnore]
        internal TranslatorService? TranslatorService => Translator.ParseWithDescription<TranslatorService>();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool OverwriteTranslations { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public ChatGPTTranslator.Settings? ChatGPT { get; set; }

        public ITranslatorSettings? GetTranslatorConfig()
        {
            return TranslatorService switch
            {
                Infrastructure.TranslatorService.ChatGPT => ChatGPT ?? throw new NullReferenceException($"No settings found for translator {Translator}"),
                Infrastructure.TranslatorService.GoogleTranslate => null,
                _ => throw new InvalidOperationException("No translator service found"),
            };
        }

        public static Config Default => new()
        {
            ResourceName = DEFAULT_RESOURCE_NAME,
            DefaultResourceName = DEFAULT_RESOURCE_NAME,
            WriteKeyAsValue = true,
            Languages = [],
            Translator = string.Empty,
        };
    }
}