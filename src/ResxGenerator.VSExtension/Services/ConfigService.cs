using Microsoft.VisualStudio.Extensibility;
using Microsoft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility.Documents;
using ResxGenerator.VSExtension.Infrastructure;
using Microsoft.VisualStudio.ProjectSystem.Query;
using System.IO;
using System.Text.Json;
using System.Globalization;
using System.Windows.Media.Animation;
using System.ComponentModel;
using ResxGenerator.VSExtension.Translators;

namespace ResxGenerator.VSExtension.Services
{
#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW

    public class ConfigService
    {
        public const string CONFIG_FILE = "resx-generator.json";
        private readonly VisualStudioExtensibility _extensibility;
        private readonly Task _initializationTask; // probably not needed
        private OutputWindow? _output;

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
            await JsonSerializer.SerializeAsync(writeStream, config, new JsonSerializerOptions
            {
                WriteIndented = true
            });
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
            var translator = EnumExtensions.GetValueFromDescription<TranslatorService>(config.TranslatorService);
            return translator switch
            {
                TranslatorService.ChatGPT => config.ChatGPT ?? throw new NullReferenceException($"No settings found for translator {translator.Value.GetDescription()}"),
                TranslatorService.DeepL => config.DeepL ?? throw new NullReferenceException($"No settings found for translator {translator.Value.GetDescription()}"),
                TranslatorService.GoogleTranslate => null,
                _ => throw new InvalidOperationException("No translator service found"),
            };
        }
    }

    public enum TranslatorService
    {
        [Description("ChatGPT")]
        ChatGPT = 0,

        [Description("DeepL")]
        DeepL = 1,

        [Description("GoogleTranslate")]
        GoogleTranslate = 2
    }

    public class Config
    {
        public string? ResourceName { get; set; }

        public bool WriteKeyAsValue { get; set; }

        public IEnumerable<string> Languages { get; set; } = [];

        internal IEnumerable<CultureInfo> Cultures => Languages.Select(x => new CultureInfo(x));

        public string? TranslatorService { get; set; }

        public ChatGPTTranslator.Settings? ChatGPT { get; set; }

        public DeepLTranslator.Settings? DeepL { get; set; }

        public static Config Default => new()
        {
            ResourceName = "SharedResource",
            WriteKeyAsValue = true,
            Languages = [],
            TranslatorService = string.Empty,
        };
    }

#pragma warning restore VSEXTPREVIEW_OUTPUTWINDOW
}