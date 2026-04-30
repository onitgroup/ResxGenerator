using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace ResxGenerator.VSExtension.Infrastructure
{
    public interface IExcelManager
    {
        public void WriteFile(string filePath, IEnumerable<ExcelModel> rows);
    }

    internal class ExcelManager : IExcelManager
    {
        private static SheetData InitializeWorkbook(SpreadsheetDocument spreadsheet, string sheetName)
        {
            var workbookPart = spreadsheet.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet();

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            var sheet = new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = sheetName
            };
            sheets.Append(sheet);

            return worksheetPart.Worksheet.AppendChild(new SheetData());
        }

        private static Cell CreateCell(string? value)
        {
            return new Cell
            {
                DataType = CellValues.InlineString,
                InlineString = new InlineString(new Text(value ?? string.Empty))
            };
        }

        private static Cell CreateCell(int value)
        {
            return new Cell
            {
                DataType = CellValues.Number,
                CellValue = new CellValue(value)
            };
        }

        public void WriteFile(string filePath, IEnumerable<ExcelModel> rows)
        {
            using var spreadsheet = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook);
            var sheetData = InitializeWorkbook(spreadsheet, "Translations");

            // Get all unique languages
            var languages = rows
                .SelectMany(x => x.Languages.Keys)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            // headers
            var headerRow = new Row
            {
                RowIndex = 1
            };
            headerRow.Append(CreateCell("Resource Path"));
            headerRow.Append(CreateCell("Key"));
            headerRow.Append(CreateCell("Occurrences"));

            foreach (var lang in languages)
            {
                headerRow.Append(CreateCell($"{lang} - Value"));
                headerRow.Append(CreateCell($"{lang} - Comment"));
            }

            sheetData.Append(headerRow);

            // data
            uint rowIndex = 2;
            foreach (var entry in rows)
            {
                var dataRow = new Row
                {
                    RowIndex = rowIndex
                };

                dataRow.Append(CreateCell(entry.ResourcePath));
                dataRow.Append(CreateCell(entry.Key));
                dataRow.Append(CreateCell(entry.Occurrences));

                foreach (var lang in languages)
                {
                    if (entry.Languages.TryGetValue(lang, out var langData))
                    {
                        dataRow.Append(CreateCell(langData.Value));
                        dataRow.Append(CreateCell(langData.Comment));
                    }
                    else
                    {
                        dataRow.Append(CreateCell(string.Empty));
                        dataRow.Append(CreateCell(string.Empty));
                    }
                }

                sheetData.Append(dataRow);
                rowIndex++;
            }

            spreadsheet.Save();
        }
    }

    /// <summary>
    /// Represents a complete resx entry with all language variants
    /// </summary>
    public record ExcelModel
    {
        public required string ResourcePath { get; init; }

        public required string Key { get; init; }

        public int Occurrences { get; init; }

        public Dictionary<string, (string? Value, string? Comment)> Languages { get; init; } = [];
    }
}