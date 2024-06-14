# Resx Generator
A tool to automatically generate and update .resx files. The tool uses Roslyn to search the project's compiled output for references to specific symbols and collect the strings.

- [Installation](#installation)
- [Functioning](#functioning)
  - [Searched Symbols](#searched-symbols)
  - [Execution](#execution)
  - [Configuration File](#configuration-file)
  - [Translators](#translators)
    - [ChatGPT](#chatgpt)
    - [Google Translate](#google-translate)
  - [Monitoring](#monitoring)

## Installation
You can install the extension directly from the [Visual Studio marketplace](https://marketplace.visualstudio.com/items?itemName=Onit.ResxGenerator).

Alternatively, you can download the package and install it manually. Download the zip with the latest version from the [Releases](https://github.com/onitgroup/ResxGenerator/releases) section and unzip it, then double click the file "ResxGenerator.VSExtension.vsix". The Visual Studio installation process will start.

> [!NOTE]
> Visual Studio version >= 17.7 is required.

## Functioning
> [!IMPORTANT]
> The tool will generate .resx files **ONLY** for the active project.
> 
> The active project depends on the currently open file.

### Searched Symbols
The tool only considers the following symbols, overloads are included:
- Microsoft.AspNetCore.Mvc.Localization.IHtmlLocalizer[]
- Microsoft.Extensions.Localization.IStringLocalizer[]
  - `@SharedLocalizer["Login"]`
  - `_sharedLocalizer["Parameter {0} is required", variable]`
- LocalizerExtensions.HtmlLocalizerExtensions
- LocalizerExtensions.StringLocalizerExtensions
  - `@SharedLocalizer.Pluralize(...)`
  - `@SharedLocalizer.Genderize(...)`
- System.ComponentModel.DataAnnotations.DisplayAttribute (all string parameters)
  - `[Display(Name = "Search")]`
- System.ComponentModel.DataAnnotations.RequiredAttribute (all string parameters)
  - `[Required(ErrorMessage = "The field is required")]`
- System.ComponentModel.DescriptionAttribute (all string parameters)
  - `[Description("Deleted")]`

> [!IMPORTANT]
> Only hardcoded strings are considered, not variables.

> [!WARNING]
> If a new extension of these classes or a new overload of these methods is created in a project, they will be ignored.

### Execution
In the top menu under _Extension_, a new submenu is available:

![image](https://github.com/onitgroup/ResxGenerator/assets/114159788/da974afb-093b-4e0e-80ed-e8fb0a073bf5)

- **Generate**: runs the tool;
- **Add ChatGPT configuration**: adds the default ChatGPT configuration to the configuration file;

### Configuration File
Below there's an example of the _resx-generator.json_ configuration file:
```json
{
  "ResourceName": "SharedResource",
  "ValidationComment": "To be validated",
  "WriteKeyAsValue": true,
  "Languages": [ "en-US", "fr-FR" ],
  "Translator": "ChatGPT | GoogleTranslate",
  "OverwriteTranslations": "false",
  "ChatGPT": {
    "Token": "*AUTHENTICATION TOKEN*",
    "Model": "gpt-3.5-turbo",
    "Prompt": "Translate the values of this JSON object from this locale {sourceLanguage} to this locale {targetLanguage} preserving its keys",
  }
}
```
- **ResourceName**: the base name for the resource file without the extension, the language will be added by the tool;
- **ValidationComment (optional)**: if set, it will be added as a comment to new or modified entries;
- **WriteKeyAsValue (optional)**: if _true_, when a new entry is added, the value will be set equal to the key. If _false_, it will be left empty;
- **Languages**: the list of languages to consider in ISO code, e.g., `["en-US", "it-IT", "fr-FR" ...]`;
- **Translator (optional)**: the service to use for translations. If not set, strings will not be translated;
- **OverwriteTranslations (optional)**: if _false_ keys that already have a translation will not be translated again. If _true_ they will be translated again;
- **ChatGPT**: see the [dedicated section](#chatgpt) below;

> [!NOTE]
> If a key exists in the .resx file but the value is empty, it will be translated even if the **OverwriteTranslations** parameter is set to _false_.

> [!NOTE]
> The tool utilizes the project's _NeutralLanguage_ as the source language for translators.

### Translators
Below there's the list of currently supported translators:

#### ChatGPT
```json
  "ChatGPT": {
    "Token": "*AUTHENTICATION TOKEN*",
    "Model": "gpt-3.5-turbo",
    "Prompt": "Translate every value of the following JSON object from this locale {sourceLanguage} to this locale {targetLanguage}, do not translate symbols",
  }
```
- **Token**: the authentication token for ChatGPT APIs
- **Model**: the ChatGPT model to use
- **Prompt**: the prompt that will be sent to ChatGPT as a command to translate. It can be customized to provide more context for translations. Please remember that a JSON object with the strings to be translated will be added to this prompt. Two placeholders are available: _{sourceLanguage}_ and _{targetLanguage}_. The first will be replaced with the _NeutralLanguage_, while the second will be replaced with the language to translate;

Since the integration is based on ChatGPT, there may be unexpected behaviors.
If any problems occur, try rerunning the tool.

> [!CAUTION]
> Modifying the prompt too much can completely break the integration.

#### Google Translate
No additional configuration is required. It is a free GoogleTranslate API.

> [!WARNING]
> Since it is a free tool, translations are not perfect. Careful verification is advised.

### Monitoring
A detailed log can be found in the _View_ -> _Output_ window. Select _Resx Generator_ from the list of sources.
