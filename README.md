# Phonexis - TXT to YT Multisearch

Questa è l'applicazione Phonexis, che permette di cercare video su YouTube basati su titoli di canzoni da un file di testo.

## Caratteristiche

- **Ricerca su YouTube**: Cerca video su YouTube utilizzando l'API di YouTube Data v3
- **Caching**: Memorizza nella cache i risultati di ricerca e le miniature per ridurre l'utilizzo dell'API
- **Gestione dello Stato**: Salva e ripristina lo stato dell'applicazione
- **Internazionalizzazione**: Supporto per più lingue
- **Modalità Test**: Testa l'applicazione senza effettuare chiamate API reali
- **Modalità Solo Cache**: Utilizza solo i risultati già presenti nella cache locale

## Requisiti

- Windows 10/11
- .NET 6.0 Runtime o superiore
- Connessione Internet (per le ricerche su YouTube)
- Chiave API di YouTube Data v3

## Installazione

1. Scarica l'ultima versione dal repository
2. Estrai il file ZIP in una directory a tua scelta
3. Esegui `Phonexis - TXT to YT Multisearch.exe`

## Utilizzo

1. Avvia l'applicazione
2. Clicca su "Browse .txt File" per selezionare un file di testo contenente titoli di canzoni (uno per riga)
3. Imposta la tua chiave API di YouTube tramite il pulsante "Set API Key"
4. Clicca su "Start YouTube Search" per iniziare la ricerca
5. Per ogni canzone, seleziona un video dai risultati o clicca "Skip" per saltarla
6. Al termine, i link selezionati verranno salvati in un file

## Sviluppo

### Prerequisiti

- Visual Studio 2022 (Community Edition è sufficiente)
- .NET 6.0 SDK
- Conoscenza di C# e WPF

### Compilazione

1. Apri la soluzione in Visual Studio
2. Ripristina i pacchetti NuGet
3. Compila la soluzione

### Struttura del Progetto

- **Models**: Contiene i modelli di dati
- **ViewModels**: Contiene i ViewModels per il pattern MVVM
- **Views**: Contiene le viste WPF
- **Services**: Contiene i servizi per l'accesso ai dati e le API
- **Helpers**: Contiene classi di utilità
- **Resources**: Contiene risorse come immagini e file di localizzazione

## Migrazione dai Dati Esistenti

Se hai utilizzato la versione Python dell'applicazione, puoi migrare i tuoi dati esistenti:

1. Copia il file `youtube_cache.db` nella directory `%LOCALAPPDATA%\Phonexis`
2. Copia il file `session_state.json.gz` nella stessa directory

## Licenza

Questo progetto è rilasciato sotto la licenza MIT.