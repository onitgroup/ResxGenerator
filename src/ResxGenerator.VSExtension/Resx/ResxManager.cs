using System.IO;
using System.Reflection;
using System.Xml.Linq;

namespace ResxGenerator.VSExtension.Resx
{
    /// <summary>
    /// Create and write to resx resource files
    /// </summary>
    public class ResxManager(string filePath)
    {
        private readonly string _filePath = filePath;
        private readonly XDocument _doc = XDocument.Load(filePath);

        public IEnumerable<ResxElement> EnumerateElements()
        {
            if (_doc.Root is null) throw new InvalidOperationException("XML root element is null");

            foreach (var element in _doc.Root.Descendants("data").Where(x => x.Attribute("name") is not null))
            {
                yield return new ResxElement(
                    element.Attribute("name")!.Value,
                    element.Descendants("value").FirstOrDefault()?.Value,
                    element.Descendants("comment").FirstOrDefault()?.Value
                );
            }
        }

        /// <summary>
        /// Add array of elements to the resource file, this method DOES NOT write the changes
        /// </summary>
        /// <param name="elements"></param>
        /// <returns></returns>
        public void AddRange(IEnumerable<ResxElement> elements, bool overwriteValues = false)
        {
            if (_doc.Root is null) throw new InvalidOperationException("XML root element is null");

            var existingKeys = _doc.Root
                .Descendants("data")
                .Where(x => x.Attribute("name") is not null)
                .Select(x => x.Attribute("name")!.Value)
                .OrderBy(x => x) // required for BinarySearch
                .ToList();

            foreach (var element in elements.Distinct())
            {
                if (string.IsNullOrWhiteSpace(element.Key))
                {
                    continue;
                }

                element.Value ??= string.Empty;

                if (existingKeys.BinarySearch(element.Key, StringComparer.InvariantCultureIgnoreCase) >= 0)
                {
                    // the key exists
                    var existingElement = _doc.Root
                            .Descendants("data")
                            .Where(x => string.Equals(element.Key, x.Attribute("name")?.Value, StringComparison.InvariantCultureIgnoreCase))
                            .First();

                    var value = existingElement.Descendants("value").FirstOrDefault();

                    // if overwriteValues is true or the key does not have a real value
                    if (overwriteValues || value is null || string.IsNullOrEmpty(value.Value))
                    {
                        var comment = existingElement.Descendants("comment").FirstOrDefault();
                        if (comment is null)
                        {
                            existingElement.Add(new XElement("comment", element.Comment));
                        }
                        else if (string.IsNullOrEmpty(element.Comment) == false)
                        {
                            comment.Value = element.Comment;
                        }

                        if (value is null)
                        {
                            existingElement.Add(new XElement("value", element.Value));
                        }
                        else
                        {
                            value.Value = element.Value;
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
                        new XAttribute("name", element.Key),
                        new XAttribute($"{XNamespace.Xml + "space"}", "preserve"),
                        new XElement("value", element.Value),
                        new XElement("comment", element.Comment)
                    );

                    _doc.Root.Add(xmlElement);
                }
            }
        }

        /// <summary>
        /// Save the resource file
        /// </summary>
        /// <returns>True if the operation was successfull, otherwise false</returns>
        public void Save()
        {
            _doc.Save(_filePath);
        }

        private static void CreateFromTemplate(string filePath)
        {
            // Create a copy of the template resx resource
            var resxTemplate = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("ResxGenerator.VSExtension.Resx.ResxTemplate.xml")
                ?? throw new FileNotFoundException("Unable to load the Resx template from assembly");

            using var file = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            resxTemplate.CopyTo(file);
        }

        public static ResxManager OpenOrCreate(string filePath)
        {
            if (!File.Exists(filePath))
            {
                CreateFromTemplate(filePath);
            }

            return new ResxManager(filePath);
        }
    }
}