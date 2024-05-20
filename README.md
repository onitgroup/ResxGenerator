# Resx Generator
Un tool per generare ed aggiornare automaticamente i file .resx, il tool sfrutta Roslyn per cercare nel compilato del progetto le reference a determinati simboli e raccogliere le stringhe.

- [Installazione](#installazione)
- [Funzionamento](#funzionamento)
  - [Simboli ricercati](#simboli-ricercati)
  - [Esecuzione](#esecuzione)
  - [File di configurazione](#file-di-configurazione)
  - [Traduttori](#traduttori)
    - [ChatGPT](#chatgpt)
    - [Google Translate](#google-translate)
  - [Monitoraggio](#monitoraggio)

## Installazione
Per installare l'estensione basta scaricare dalla sezione [Releases](https://github.com/onitgroup/ResxGenerator/releases) lo zip con l'ultima versione, decomprimerlo ed aprire il file "ResxGenerator.VSExtension.vsix", partirà il solito processo di installazione di Visual Studio.

> [!NOTE]
> È necessaria avere una versione di Visual Studio >= 17.7

## Funzionamento
> [!IMPORTANT]
> Il tool genererà i .resx **SOLTANTO** per il progetto attivo.
> 
> Il progetto attivo dipende dal file attualmente aperto.

### Simboli ricercati
Il tool considera soltanto i seguenti simboli, gli overload sono inclusi:
- Microsoft.AspNetCore.Mvc.Localization.IHtmlLocalizer[]
- Microsoft.Extensions.Localization.IStringLocalizer[]
  - `@SharedLocalizer["Login"]`
  - `_sharedLocalizer["Parameter {0} is required", variable]`
- Onit.Infrastructure.AspNetCore.HtmlLocalizerExtensions
- Onit.Infrastructure.AspNetCore.StringLocalizerExtensions
  - `@SharedLocalizer.Pluralize(...)`
  - `@SharedLocalizer.Genderize(...)`
- System.ComponentModel.DataAnnotations.DisplayAttribute (tutti i parametri di tipo string)
  - `[Display(Name = "Search")]`
- System.ComponentModel.DataAnnotations.RequiredAttribute (tutti i parametri di tipo string)
  - `[Required(ErrorMessage = "The field is required")]`
- System.ComponentModel.DescriptionAttribute (tutti i parametri di tipo string)
  - `[Description("Deleted")]`

> [!IMPORTANT]
> Vengono considerate soltanto le stringhe a codice, non le variabili.

> [!WARNING]
> Se in un progetto viene creata una nuova estensione di queste classi o un nuovo overload di questi metodi verranno ignorati.

### Esecuzione
In alto nel menù _Extension_ è disponibile un nuovo sotto-menù:

![image](https://github.com/onitgroup/ResxGenerator/assets/114159788/da974afb-093b-4e0e-80ed-e8fb0a073bf5)

### File di configurazione
Di seguito un esempio del file di configurazione _resx-generator.json_, la configurazione è per progetto:
```json
{
  "ResourceName": "SharedResource",
  "ValidationComment": "Da validare",
  "WriteKeyAsValue": true,
  "Languages": [ "en-US", "fr-FR" ],
  "Translator": "ChatGPT | GoogleTranslate",
  "OverwriteTranslations": "false",
  "ChatGPT": {
    "Token": "*TOKEN DI AUTENTICAZIONE*",
    "Model": "gpt-3.5-turbo",
    "Prompt": "Translate the values of this JSON object from this locale {sourceLanguage} to this locale {targetLanguage} preserving its keys",
  }
}
```
- **ResourceName**: il nome base per il file delle risorse senza estensione, la lingua verrà aggiunta in automatico;
- **ValidationComment (opzionale)**: se valorizzato verrà messo come commento alle nuove entry o a quelle che subiscono modifiche;
- **WriteKeyAsValue (opzionale)**: se _true_ quando verrà aggiunta una nuova entry il valore verrà settato uguale alla chiave, se _false_ verrà lasciato vuoto;
- **Languages**: la lista dei linguaggi da considerare in codice ISO, esempio `["en-US", "it-IT", "fr-FR" ...]`;
- **Translator (opzionale)**: il servizio da utilizzare per le traduzioni, se non valorizzato le stringhe non saranno tradotte;
- **OverwriteTranslations (opzionale)**: se _false_ le chiavi che hanno già una traduzione non verranno tradotte di nuovo, se _true_ si;
- **ChatGPT**: vedere la [sezione dedicata](#chatgpt) di seguito;
  
> [!NOTE]
> Se una chiave esiste nel file .resx ma il valore è vuoto verrà tradotta anche se il parametro **OverwriteTranslations** è impostato a _false_.

> [!NOTE]
> Il tool usa il _NeutralLanguage_ come linguaggio di partenza per i traduttori. Inoltre, se è presente nei linguaggi da considerare lo esclude dalla lista.

### Traduttori
Di seguito la lista dei traduttori attualmente supportati:

#### ChatGPT
```json
  "ChatGPT": {
    "Token": "*TOKEN DI AUTENTICAZIONE*",
    "Model": "gpt-3.5-turbo",
    "Prompt": "Translate every value of the following JSON object from this locale {sourceLanguage} to this locale {targetLanguage}, do not translate symbols",
  }
```
- **Token**: il token di autenticazione per le api di ChatGPT
- **Model**: il modello di ChatGPT da utilizzare
- **Prompt**: l'incipit che verrà mandato a ChatGPT come comando per tradurre, è possibile personalizzarlo per fornire più contesto da usare nelle traduzioni. È bene tenere a mente che a questo prompt verrà aggiunto un oggetto JSON con le stringhe da tradurre. Ci sono due placeholder disponibili _{sourceLanguage}_ e _{targetLanguage}_. Il primo verrà sostituito con il _NeutralLanguage_, mentre il secondo con il linguaggio da tradurre;

Dato che l'integrazione si basa su ChatGPT potrebbero esserci dei comportamenti inaspettati.
In caso di problemi provare a rilanciare il tool. 

> [!CAUTION]
> Modificare troppo il prompt può rompere completamente l'integrazione.

#### Google Translate
Non richiede nessuna configurazione aggiuntiva. Si tratta di un API di GoogleTranslate gratuito.

> [!WARNING]
> Essendo un tool gratuito le traduzioni non sono perfette, è suggerita un attenta verifica.

### Monitoraggio
È possibile trovare un log dettagliato nella finestra _View_ -> _Output_ -> nell'elenco delle sorgenti selezionare _Resx Generator_.
