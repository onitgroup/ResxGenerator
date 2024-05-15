# Resx Generator
Un tool per generare ed aggiornare automaticamente i file .resx, il tool sfrutta Roslyn per cercare nel compilato del progetto le reference a determinati simboli e raccogliere le stringhe.

- [Installazione](#installazione)
- [Funzionamento](#funzionamento)
  - [Simboli ricercati](#simboli-ricercati)
  - [Esecuzione](#esecuzione)
  - [File di configurazione](#file-di-configurazione)
  - [Monitoraggio](#monitoraggio)
- [Possibili errori](#possibili-errori)

## Installazione
Per installare l'estensione basta scaricare dalla sezione _Releases_ lo zip con l'ultima versione, decomprimerlo ed aprire il file "ResxGenerator.VSExtension.vsix", partirà il solito processo di installazione di Visual Studio.

> [!NOTE]
> È necessaria avere una versione di Visual Studio >= 17.9

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
> Vengono considerate soltanto le stringhe a codice non le variabili.

> [!WARNING]
> Se in un progetto viene creata una nuova estensione di queste classi o un nuovo overload di questi metodi verranno ignorati.

### Esecuzione
In alto nel menù _Extension_ è disponibile un nuovo pulsante con cui eseguire il tool:

![image](https://github.com/gamadori-osm/ResxGenerator/assets/114159788/9d6a3bdf-3d06-4fe3-ba98-1b6a2f89eb76)

### File di configurazione
Quando il tool viene lanciato se non presente verrà generato il file di configurazione _resx-generator.json_:
```json
{
  "ResourceName": "SharedResource",
  "WriteKeyAsValue": true,
  "Languages": []
}
```
- **ResourceName**: il nome base per i file di configurazione senza estensione, a questo verrà aggiunta la lingua,
- **WriteKeyAsValue**: se _true_ quando verrà aggiunta una nuova entry il valore verrà settato uguale alla chiave, se _false_ verrà lasciato vuoto,
- **Languages**: la lista dei linguaggi da considerare in codice ISO, esempio `["en", "it", "fr" ...]`

> [!NOTE]
> Il tool controlla il NeutralLanguage del progetto e se presente nei linguaggi da considerare lo esclude.

### Monitoraggio
È possibile trovare un log dettagliato nella finestra _View_ -> _Output_ -> nell'elenco delle sorgenti selezionare _Resx Generator_.

## Possibili errori
La prima volta che si esegue il tool su un progetto apparirà la dicitura _"No languages found in the config file, aborting."_, è normale dato che il file di configurazione di base non ha linguaggi da considerare.

Se quando si esegue il tool appare la dicitura _"The project type is not supported."_ o il tool sta venendo eseguito su un progetto non supportato (es Class Library) oppure è necessario aggiungere il tipo di progetto a quelli supportati. 
Nel secondo caso è necessario il `GUID` che viene stampato dopo il nome del progetto nella finestra di output:

`Project: {nomeDelProgetto}, TypeGuid: fae04ec0-301f-11d3-bf4b-00c04f79efbc`

