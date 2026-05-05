# Resx Generator

A tool to automatically generate, update, export and import .resx files. The tool uses Roslyn to search the project's source code for references to specific symbols, collect the strings and group them by resource type.

- [Resx Generator](#resx-generator)
  - [Installation](#installation)
  - [How does it work](#how-does-it-work)
    - [Searched Symbols](#searched-symbols)
    - [Resource Type Resolution](#resource-type-resolution)
    - [Resx location](#resx-location)
  - [Configuration file](#configuration-file)
    - [DefaultResourceName](#defaultresourcename)
    - [Languages](#languages)
    - [WriteKeyAsValue](#writekeyasvalue)
    - [ValidationComment](#validationcomment)
    - [OverwriteTranslations](#overwritetranslations)
    - [Translator](#translator)
    - [ChatGPT](#chatgpt)
    - [Google Translate](#google-translate)
  - [Commands](#commands)
    - [Generate](#generate)
    - [Export to Excel](#export-to-excel)
    - [Import from Excel](#import-from-excel)
    - [Add ChatGPT Configuration](#add-chatgpt-configuration)
  - [Excel Format](#excel-format)
  - [Monitoring](#monitoring)

## Installation

You can install the extension directly from the [Visual Studio marketplace](https://marketplace.visualstudio.com/items?itemName=Onit.ResxGenerator).

Alternatively, you can download the package and install it manually. Download the zip with the latest version from the [Releases](https://github.com/onitgroup/ResxGenerator/releases) section and unzip it, then double click the file "ResxGenerator.VSExtension.vsix". The Visual Studio installation process will start.

> [!NOTE]
> Visual Studio version >= 17.7 is required.

## How does it work

> [!IMPORTANT]
> The tool will operate **ONLY** on the active project and its locally referenced projects.
>
> The active project depends on the currently open file.

### Searched Symbols

The tool only considers the following symbols, overloads are included:

- `Microsoft.AspNetCore.Mvc.Localization.IHtmlLocalizer[]`
- `Microsoft.Extensions.Localization.IStringLocalizer[]`
  - `@SharedLocalizer["Login"]`
  - `_sharedLocalizer["Parameter {0} is required", variable]`
- `LocalizerExtensions.HtmlLocalizerExtensions`
- `LocalizerExtensions.StringLocalizerExtensions`
  - `@SharedLocalizer.Pluralize(...)`
  - `@SharedLocalizer.Genderize(...)`
- `System.ComponentModel.DataAnnotations.DisplayAttribute` (all string parameters)
  - `[Display(Name = "Search")]`
- `System.ComponentModel.DataAnnotations.RequiredAttribute` (all string parameters)
  - `[Required(ErrorMessage = "The field is required")]`
- `System.ComponentModel.DescriptionAttribute` (all string parameters)
  - `[Description("Deleted")]`

> [!IMPORTANT]
> Only hardcoded strings are taken into account, not variables.

### Resource Type Resolution

Strings are automatically grouped by resource type.

The type is inferred from the generic type argument of `IStringLocalizer<T>` or `IHtmlLocalizer<T>`

```csharp
// Strings go to SharedResource.{lang}.resx
private readonly IStringLocalizer<SharedResource> _sharedLocalizer;

// Strings go to MyResource.{lang}.resx
private readonly IStringLocalizer<MyResource> _myLocalizer;
```

Or resolved directly from the attribute's `ResourceType` or `ErrorMessageResourceType` parameter:

```csharp
[Display(Name = "Name", ResourceType = typeof(AppResource))]
[Required(ErrorMessageResourceName = "The field is required", ErrorMessageResourceType = typeof(AppResource))]
public string Name { get; set; }
```

When is **not possible** to infer the resource type the string falls back to `DefaultResourceName`:

```csharp
[Description("Some description")]
```

### Resx location

When the resource type is known the .resx files are created **next to the corresponding .cs class file**:

```
MainWebApp/
└── Resources/
    ├── AppResource.cs
    ├── AppResource.it-IT.resx
    └── AppResource.en-US.resx

SharedResources/
├── SharedResource.cs
├── SharedResource.it-IT.resx
└── SharedResource.en-US.resx
```

Otherwise, for `DefaultResourceName`, the resx are created under the active project root.

```
MyProject/
├── DefaultResourceName.it-IT.resx
└── DefaultResourceName.en-US.resx
```

## Configuration file

The tool looks for a `resx-generator.json` file in the active project's root directory. If not found, it will be created automatically on the first run.

```json
{
  "DefaultResourceName": "SharedResource",
  "ValidationComment": "To be validated",
  "WriteKeyAsValue": true,
  "Languages": ["en-US", "fr-FR"],
  "Translator": "ChatGPT | GoogleTranslate",
  "OverwriteTranslations": false,
  "ChatGPT": {
    "Token": "*AUTHENTICATION TOKEN*",
    "Model": "gpt-3.5-turbo",
    "Prompt": "Translate the values of this JSON object from this locale {sourceLanguage} to this locale {targetLanguage} preserving its keys"
  }
}
```

### DefaultResourceName

The fallback resource name used for strings that cannot be attributed to a specific resource type, see [Resource Type Resolution](#resource-type-resolution) above.

> [!IMPORTANT]
> This parameter is **REQUIRED**

### Languages

The list of target languages in ISO format, e.g. `["en-US", "it-IT", "fr-FR"]`.

> [!NOTE]
> The project's _NeutralLanguage_ is used as the source language for translators. If it appears in this list, it will be ignored.

### WriteKeyAsValue

_(optional)_ If `true`, when a new entry is added the value will be set equal to the key. If `false`, the value will be left empty. Defaults to `false`.

### ValidationComment

_(optional)_ If set, it will be added as a comment to every new or modified entry.

### OverwriteTranslations

_(optional)_ If `false`, keys that already have a translation will not be translated again. If `true`, they will be retranslated. Defaults to `false`.

> [!NOTE]
> Keys with an empty value are always translated regardless of this setting.

### Translator

_(optional)_ The translation service to use. If not set, strings will not be translated. Accepted values: `ChatGPT`, `GoogleTranslate`.

> [!NOTE]
> When a value is translate a comment is added 'Generated by {translator}'

### ChatGPT

```json
"ChatGPT": {
  "Token": "*AUTHENTICATION TOKEN*",
  "Model": "gpt-3.5-turbo",
  "Prompt": "Translate every value of the following JSON object from this locale {sourceLanguage} to this locale {targetLanguage}, do not translate symbols"
}
```

- **Token**: the authentication token for the ChatGPT API;
- **Model**: the ChatGPT model to use;
- **Prompt**: the translation instruction sent to ChatGPT. Two placeholders are available: `{sourceLanguage}` and `{targetLanguage}`. A JSON object with the strings to translate will be appended automatically;

> [!CAUTION]
> Modifying the prompt too much can completely break the integration.

> [!NOTE]
> Since the integration is based on ChatGPT, there may be unexpected behaviors. If any problems occur, try rerunning the tool.

### Google Translate

No additional configuration is required. It uses a free Google Translate API.

> [!WARNING]
> Since it is a free API, translations may not be perfect. Careful verification is advised.

## Commands

In the Visual Studio top menu, under _Extensions_, a submenu _Resx Generator_ is available with the following commands.

### Generate

Analyzes the source code, collects all localized strings grouped by resource type and generates or updates the corresponding .resx files.

The strings are translate if a [translator](#translator) is set.

### Export to Excel

Exports both the .resx entires as well as the codes strings to an Excel file. Each row represents a key and includes the translated values and comments for each language. See [Excel Format](#excel-format) for details.

### Import from Excel

Reads a previously exported Excel file and updates the corresponding .resx files. The resource type is resolved from the _Resource Name_ column using the same rules as the Generate command.

> [!NOTE]
> Importing **always overwrites** existing translations.

> [!IMPORTANT]
> The Excel file must follow the format produced by the Export command. Do not rename or reorder columns.

### Add ChatGPT Configuration

Adds the default [ChatGPT](#chatgpt) configuration block to the `resx-generator.json` file.

## Excel Format

The exported Excel file contains a single sheet named _Translations_ with the following columns:

| Column               | Description                                                                                                             |
| -------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| **Resource Name**    | The fully qualified name of the resource type (e.g. `MyApp.Resources.SharedResource`)                                   |
| **Key**              | The resource key                                                                                                        |
| **Occurrences**      | Number of times the key appears in the source code. `0` means the key exists in the .resx but is not referenced in code |
| **{lang} - Value**   | The translated value for the language (one pair of columns per language)                                                |
| **{lang} - Comment** | The comment for the language                                                                                            |

## Monitoring

A detailed log of all operations can be found in _View_ → _Output_. Select **Resx Generator** from the list of sources.
