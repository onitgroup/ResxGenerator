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
        bool TryGet(string projectDirectory, [NotNullWhen(true)] out Config? config);

        void AddDefault(string projectDirectory);

        void Update(string projectDirectory, Config config);

        bool TryGetNeutralCulture(string projectPath, [NotNullWhen(true)] out CultureInfo? neutralLanguage);

        public IEnumerable<(string FilePath, string Culture)> DefaultResourceFiles(string projectDirectory);

        public string DefaultResourceFile(string projectDirectory, string culture);
    }

    public class ConfigurationService : IConfigurationService
    {
        private const string CONFIG_FILE_NAME = "resx-generator.json";

        private readonly JsonSerializerOptions _indentedOptions = new()
        {
            WriteIndented = true,
        };

        private FileInfo GetConfigFileInfo(string projectDirectory)
        {
            return new FileInfo(Path.Combine(projectDirectory, CONFIG_FILE_NAME));
        }

        public void AddDefault(string projectDirectory)
        {
            var configFile = GetConfigFileInfo(projectDirectory);
            if (configFile.Exists)
            {
                throw new InvalidOperationException($"Config file already exists at {configFile.FullName}");
            }
            var config = Config.Default;
            using var stream = new FileStream(configFile.FullName, FileMode.CreateNew);
            JsonSerializer.Serialize(stream, config, _indentedOptions);
        }

        public bool TryGet(string projectDirectory, [NotNullWhen(true)] out Config? config)
        {
            var configFile = GetConfigFileInfo(projectDirectory);
            if (!configFile.Exists)
            {
                config = null;
                return false;
            }

            using var readStream = new FileStream(configFile.FullName, FileMode.Open);
            config = JsonSerializer.Deserialize<Config>(readStream);

            if (!string.IsNullOrEmpty(config.ResourceName))
            {
                throw new InvalidDataException($"The config '{nameof(Config.ResourceName)}' is obsolete, please change it to '{nameof(Config.DefaultResourceName)}' and relaunch the command.");
            }

            if (string.IsNullOrEmpty(config.DefaultResourceName))
            {
                throw new InvalidDataException($"Missing required config '{nameof(Config.DefaultResourceName)}'");
            }

            return config is not null;
        }

        public void Update(string projectDirectory, Config config)
        {
            var configFile = GetConfigFileInfo(projectDirectory);
            using var stream = new FileStream(configFile.FullName, FileMode.Create);
            JsonSerializer.Serialize(stream, config, _indentedOptions);
        }

        public bool TryGetNeutralCulture(string projectPath, [NotNullWhen(true)] out CultureInfo? neutralLanguage)
        {
            if (string.IsNullOrEmpty(projectPath))
            {
                neutralLanguage = null;
                return false;
            }

            var document = XDocument.Load(projectPath);
            neutralLanguage = document.Root?
                .Descendants("NeutralLanguage")
                .Select(x => new CultureInfo(x.Value))
                .FirstOrDefault();

            return neutralLanguage is not null;
        }

        public IEnumerable<(string FilePath, string Culture)> DefaultResourceFiles(string projectDirectory)
        {
            if (!TryGet(projectDirectory, out var config))
            {
                yield break;
            }

            foreach (var culture in config.Cultures)
            {
                yield return (Path.Combine(projectDirectory, $"{config.DefaultResourceName}.{culture.Name}.resx"), culture.Name);
            }
        }

        public string DefaultResourceFile(string projectDirectory, string culture)
        {
            if (!TryGet(projectDirectory, out var config))
            {
                throw new InvalidOperationException("Unable to get the configuration");
            }

            return Path.Combine(projectDirectory, $"{config.DefaultResourceName}.{culture}.resx");
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

        public string DefaultResourceName { get; set; } = string.Empty;

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

        public static string BuildResxFileName(string resourceName, string culture)
        {
            return $"{resourceName}.{culture}.resx";
        }

        public static string BuildCatchAllResxFileName(string resourceName)
        {
            return $"{resourceName}.*.resx";
        }

        public static bool TryParseCultureFromResxFileName(string fileName, [NotNullWhen(true)] out string? culture)
        {
            var parts = Path.GetFileNameWithoutExtension(fileName).Split('.'); // "SharedResource.it-IT"
            culture = parts.Length > 1
                ? parts.Last()
                : null;

            return !string.IsNullOrEmpty(culture);
        }
    }
}