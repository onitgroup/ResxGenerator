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

namespace ResxGenerator.VSExtension.Services
{
#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW

    public class ConfigService
    {
        public const string CONFIG_FILE = "resx-generator.json";
        private readonly VisualStudioExtensibility _extensibility;
        private OutputWindow? _output;
        private readonly Task _initializationTask; // probably not needed

        public ConfigService(VisualStudioExtensibility extensibility)
        {
            this._extensibility = Requires.NotNull(extensibility, nameof(extensibility));
            _initializationTask = Task.Run(InitializeAsync);
        }

        private async Task InitializeAsync()
        {
            _output = await Utilities.GetOutputWindowAsync(_extensibility);
            Assumes.NotNull(_output);
        }

        private async Task WriteToOutputAsync(string message)
        {
            if (_output is not null)
            {
                await _output.Writer.WriteLineAsync(message);
            }
        }

        private static async Task<Config> AddDefaultConfigFileAsync(string path)
        {
            var config = Config.Default;
            using var writeStream = new FileStream(path, FileMode.OpenOrCreate);
            await JsonSerializer.SerializeAsync(writeStream, config, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            return config;
        }

        public async Task<Config> GetOrCreateAsync(IProjectSnapshot snapshot)
        {
            var path = Path.Combine(Path.GetDirectoryName(snapshot.Path)!, CONFIG_FILE);

            // it's written weird because in the other forms it does not work
            var configFile = snapshot.Files
                .Select(x => x.Path)
                .Where(x => x == path)
                .FirstOrDefault();

            if (configFile is null)
            {
                await WriteToOutputAsync("No configuration file found, a new one will be created.");
                return await AddDefaultConfigFileAsync(path);
            }

            Config? config;
            try
            {
                using var readStream = new FileStream(configFile, FileMode.Open);
                config = await JsonSerializer.DeserializeAsync<Config>(readStream);
                if (config is null)
                {
                    await WriteToOutputAsync("Unable to read the configuration file, the default will be used");
                    config = Config.Default;
                }
            }
            catch (Exception)
            {
                await WriteToOutputAsync("Error while attempting to read the configuration file, the default will be used");
                config = Config.Default;
            }

            return config;
        }
    }

#pragma warning restore VSEXTPREVIEW_OUTPUTWINDOW

    public class Config
    {
        public static Config Default => new()
        {
            ResourceName = "SharedResource",
            WriteKeyAsValue = true,
            Languages = []
        };

        public string? ResourceName { get; set; }

        public bool WriteKeyAsValue { get; set; }

        public IEnumerable<string> Languages { get; set; } = [];

        internal IEnumerable<CultureInfo> Cultures => Languages.Select(x => new CultureInfo(x));
    }
}