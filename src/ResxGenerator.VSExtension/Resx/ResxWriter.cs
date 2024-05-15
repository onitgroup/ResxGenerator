using System.IO;
using System.Reflection;
using System.Xml.Linq;

namespace ResxGenerator.VSExtension.Resx
{
    /// <summary>
    /// Create and write to resx resource files
    /// </summary>
    public class ResxWriter
    {
        private readonly XDocument _xd;
        private readonly string _resourceFilePath;

        public ResxWriter(string resourceFullPath)
        {
            _resourceFilePath = resourceFullPath;

            if (File.Exists(_resourceFilePath) == false)
                CreateNewResxFile();

            _xd = XDocument.Load(_resourceFilePath);
        }

        private void CreateNewResxFile()
        {
            // Create a copy of the template resx resource
            var resxTemplate = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("ResxGenerator.VSExtension.Resx.ResxTemplate.xml")
                ?? throw new FileNotFoundException("Unable to load the Resx template from assembly");

            using var file = File.Create(_resourceFilePath);
            resxTemplate.CopyTo(file);
        }

        /// <summary>
        /// Add array of elements to the resource file, this method DOES NOT write the changes
        /// </summary>
        /// <param name="elements"></param>
        /// <returns></returns>
        public void AddRange(IEnumerable<ResxElement> elements)
        {
            if (_xd.Root is null) throw new InvalidOperationException("XML root element is null");

            var existingKeys = _xd.Root
                .Descendants("data")
                .Where(x => x.Attribute("name") is not null)
                .Select(x => x.Attribute("name")!.Value)
                .OrderBy(x => x) // required to use binary search
                .ToList();

            foreach (var e in elements.Distinct())
            {
                if (string.IsNullOrWhiteSpace(e.Key))
                    continue;

                if (existingKeys.BinarySearch(e.Key, StringComparer.OrdinalIgnoreCase) >= 0)
                    continue;

                var xmlElement = new XElement("data",
                    new XAttribute("name", e.Key),
                    new XAttribute($"{XNamespace.Xml + "space"}", "preserve"),
                    new XElement("value", e.Value));

                _xd.Root.Add(xmlElement);
            }
        }

        /// <summary>
        /// Save the resource file
        /// </summary>
        /// <returns>True if the operation was successfull, otherwise false</returns>
        public void Save()
        {
            _xd.Save(_resourceFilePath);
        }
    }
}