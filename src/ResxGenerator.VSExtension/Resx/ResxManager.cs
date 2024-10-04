using System.IO;
using System.Reflection;
using System.Xml.Linq;

namespace ResxGenerator.VSExtension.Resx
{
    /// <summary>
    /// Create and write to resx resource files
    /// </summary>
    public class ResxManager
    {
        private readonly XDocument _xd;
        private readonly string _resourceFilePath;

        public ResxManager(string resourceFullPath)
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

        public IEnumerable<string> GetKeysWithValues()
        {
            if (_xd.Root is null) throw new InvalidOperationException("XML root element is null");

            return _xd.Root
                .Descendants("data")
                .Where(x =>
                {
                    var value = x.Descendants("value").FirstOrDefault();
                    return x.Attribute("name") is not null &&
                           value is not null &&
                           string.IsNullOrEmpty(value.Value) == false;
                })
                .Select(x => x.Attribute("name")!.Value)
                .ToList();
        }

        /// <summary>
        /// Add array of elements to the resource file, this method DOES NOT write the changes
        /// </summary>
        /// <param name="elements"></param>
        /// <returns></returns>
        public void AddRange(IEnumerable<ResxElement> elements, bool overwriteValues = false)
        {
            if (_xd.Root is null) throw new InvalidOperationException("XML root element is null");

            var existingKeys = _xd.Root
                .Descendants("data")
                .Where(x => x.Attribute("name") is not null)
                .Select(x => x.Attribute("name")!.Value)
                .OrderBy(x => x) // required for BinarySearch
                .ToList();

            foreach (var e in elements.Distinct())
            {
                if (string.IsNullOrWhiteSpace(e.Key))
                    continue;

                e.Value ??= string.Empty;

                if (existingKeys.BinarySearch(e.Key, StringComparer.InvariantCultureIgnoreCase) >= 0)
                {
                    // the key exists

                    var existingElement = _xd.Root
                            .Descendants("data")
                            .Where(x => string.Equals(e.Key, x.Attribute("name")?.Value, StringComparison.InvariantCultureIgnoreCase))
                            .FirstOrDefault();

                    var value = existingElement.Descendants("value").FirstOrDefault();

                    // if overwriteValues is true or the key does not have a real value
                    if (overwriteValues || value is null || string.IsNullOrEmpty(value.Value))
                    {
                        var comment = existingElement.Descendants("comment").FirstOrDefault();
                        if (comment is null)
                        {
                            existingElement.Add(new XElement("comment", e.Comment));
                        }
                        else if(string.IsNullOrEmpty(e.Comment) == false)
                        {
                            comment.Value = e.Comment;
                        }

                        if (value is null)
                        {
                            existingElement.Add(new XElement("value", e.Value));
                        }
                        else
                        {
                            value.Value = e.Value;
                        }
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    // new key

                    var xmlElement = new XElement("data",
                        new XAttribute("name", e.Key),
                        new XAttribute($"{XNamespace.Xml + "space"}", "preserve"),
                        new XElement("value", e.Value),
                        new XElement("comment", e.Comment)
                    );

                    _xd.Root.Add(xmlElement);
                }
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