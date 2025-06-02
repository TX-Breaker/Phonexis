# Phonexis - TXT to YT Multisearch - Guida alla Distribuzione

Questo documento fornisce istruzioni dettagliate per la distribuzione, l'installazione e l'utilizzo dell'applicazione Phonexis - TXT to YT Multisearch.

## Preparazione per la Distribuzione

### 1. Compilazione dell'Eseguibile

Per creare un file eseguibile (.EXE) autonomo:

```powershell
# Naviga alla directory del progetto
cd path\to\Phonexis-TXT-to-YT-Multisearch

# Pubblica l'applicazione come singolo file eseguibile
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

L'eseguibile sarà disponibile nella cartella `bin\Release\net6.0-windows\win-x64\publish\`.

### 2. Creazione del Pacchetto di Distribuzione

Crea un archivio ZIP contenente:
- L'eseguibile (`Phonexis - TXT to YT Multisearch.exe`)
- File README.md
- File LICENSE
- Eventuali file di configurazione necessari

### 3. Distribuzione su GitHub

1. Crea un nuovo repository su GitHub
2. Carica il codice sorgente
3. Crea una nuova release:
   - Vai alla sezione "Releases"
   - Clicca su "Draft a new release"
   - Assegna un tag di versione (es. v1.0.0)
   - Carica il file ZIP creato nel passaggio precedente
   - Fornisci note di rilascio dettagliate

## Guida all'Installazione

### Requisiti di Sistema
- Windows 10 o superiore
- .NET 6.0 Runtime (incluso nell'eseguibile se pubblicato come self-contained)
- Connessione Internet (per le ricerche su YouTube)
- Chiave API di YouTube Data v3

### Procedura di Installazione
1. Scarica il file ZIP dalla pagina delle release su GitHub
2. Estrai il contenuto in una cartella a tua scelta
3. Esegui `Phonexis - TXT to YT Multisearch.exe`
4. Al primo avvio, configura la tua chiave API di YouTube

### Ottenere una Chiave API di YouTube
1. Visita la [Google Cloud Console](https://console.cloud.google.com/)
2. Crea un nuovo progetto
3. Abilita l'API YouTube Data v3
4. Crea una chiave API
5. Inserisci la chiave nell'applicazione tramite il pulsante "Set API Key"

## Disclaimer e Note Legali

### Limitazioni dell'API di YouTube
L'applicazione utilizza l'API YouTube Data v3, che è soggetta a quote e limitazioni imposte da Google. Ogni utente è responsabile del rispetto dei termini di servizio di Google e delle quote API assegnate.

### Responsabilità dell'Utente
- L'utente è responsabile dell'ottenimento e dell'utilizzo della propria chiave API di YouTube
- L'utente deve rispettare i termini di servizio di YouTube e Google
- L'applicazione non è affiliata a YouTube o Google

### Limitazione di Responsabilità
Il software è fornito "così com'è", senza garanzie di alcun tipo, esplicite o implicite. Gli sviluppatori non sono responsabili per eventuali danni diretti, indiretti, incidentali, speciali, esemplari o consequenziali derivanti dall'uso o dall'impossibilità di utilizzare il software.

### Privacy
L'applicazione memorizza localmente:
- Risultati di ricerca nella cache
- Stato della sessione
- Chiavi API (in modo sicuro)
- File di log per il debug

Nessun dato viene trasmesso agli sviluppatori dell'applicazione.

## Supporto e Contributi

### Segnalazione di Problemi
Per segnalare problemi o richiedere nuove funzionalità, utilizza la sezione "Issues" del repository GitHub.

### Contributi
I contributi al progetto sono benvenuti. Per contribuire:
1. Forka il repository
2. Crea un branch per la tua modifica
3. Invia una pull request

## Licenza
Questo software è distribuito sotto la licenza MIT. Vedi il file LICENSE per i dettagli.