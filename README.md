# IniLike

Libreria C# per leggere un formato INI esteso con tabelle testuali. Il namespace
e il nome dell'assembly sono **ConfigurationFilesReader**; la classe pubblica è
`ConfigurationFilesReader.ConfigurationFile`.

Questo README descrive i sorgenti effettivamente presenti in questa cartella.
Alla verifica del 15 settembre 2026, `ConfigurationFile.cs` è **identico byte per
byte** al sorgente storico in `ELPT_V0/BG_ELPT_Software/ConfigurationFilesReader`.
Nei file disponibili non risultano quindi estensioni rispetto a quella versione.
La copia attuale di OperatorUI aggiunge invece il metodo `SetParameter`.

## Formato dei file

```ini
## Commento su una riga intera
[MYSQL]
server = localhost;
port = 3306;
enabled = TRUE;

[DIRECTORIES]
TestConfigDir = ./ricette/;

[TABLE:TEST_PHASES]
APERTURA; BLOCCHI_APERTURA; 2; preprocess.py;
CHIUSURA; BLOCCHI_CHIUSURA; 1; preprocess.py;
```

- `[SEZIONE]` contiene coppie `chiave = valore`.
- `[TABLE:NOME]` contiene righe opache: la libreria restituisce `List<string>` e
  lascia al chiamante la suddivisione delle colonne.
- Nomi di sezioni, chiavi e tabelle sono **case-sensitive**; il prefisso `TABLE:`
  deve essere maiuscolo. Sezioni e tabelle hanno dizionari distinti.
- Righe vuote e righe che iniziano con `##`, dopo il trim, vengono ignorate.
- Spazi iniziali/finali delle righe vengono rimossi. Da valori e righe di tabella
  vengono eliminati tutti i caratteri finali presenti in `;`, `,`, `.`.
- Una coppia chiave/valore deve produrre **esattamente due parti** dividendo per
  `=`. Una riga come `expression = a=b;` viene ignorata.
- Non ci sono quoting, escaping, valori multilinea o commenti inline. Le
  virgolette rimangono parte del valore. Una riga `; commento` non è un commento
  riconosciuto: in una tabella diventa una riga dati.
- Sezioni, tabelle o chiavi duplicate provocano un'eccezione.
- Un percorso relativo del file viene risolto rispetto alla directory di lavoro.
  I valori contenenti percorsi vengono restituiti come testo, senza risoluzione.

Il delimitatore finale `.` può alterare dati significativi: `value...` diventa
`value`; il solo `.` diventa una stringa vuota. Usare ad esempio `./` per la
cartella corrente. Il parser non interpreta il formato INI come uno standard completo.

## API

```csharp
using ConfigurationFilesReader;
using System.Collections.Generic;

var config = new ConfigurationFile("config.ini");
string host = config.getParameter("MYSQL", "server", "localhost");
int port = config.getParameter("MYSQL", "port", 3306);
bool enabled = config.getParameter("MYSQL", "enabled", false);
List<string> phases = config.getTable("TEST_PHASES");
```

| Membro | Comportamento |
| --- | --- |
| `ConfigurationFile(string filename)` | Carica subito il file; file assente o errori di parsing generano eccezioni. |
| `ConfigurationFile()` | Crea un contenitore vuoto, senza caricare file. |
| `checkSection(string)` | Verifica le sole sezioni, non le tabelle. |
| `getParameter(section, key, string defaultValue)` | Valore testuale o default se manca sezione/chiave. |
| `getParameter(..., bool)` | Solo `TRUE`, ignorando maiuscole e spazi, vale true. Un valore presente diverso da TRUE vale false, anche se il default è true. |
| `getParameter(..., double/float)` | Conversione con cultura invariabile; la virgola viene sostituita dal punto. Conversione fallita: default. |
| `getParameter(..., long/int)` | Conversione intera con cultura invariabile; default se il parsing fallisce. L'overload int converte prima in long e poi esegue un cast non controllato: valori fuori intervallo int possono andare in overflow senza usare il default. |
| `getTable(name)` | Restituisce la lista interna modificabile; tabella assente: nuova lista vuota non collegata al contenitore. |
| `addParameter(...)` | Metodo dichiarato ma **non implementato**: non modifica memoria né file. |
| `UpdateFile` | Campo pubblico, default false; vedere i limiti sotto. |
| `parSeparator`, `parEndLineDelimiter` | Array pubblici, default `=` e `; , .`. Il caricamento avviene nel costruttore, prima che il chiamante possa cambiarli; manca un metodo pubblico di reload. |

## Limiti della scrittura

Questa implementazione va usata come **lettore**. `UpdateFile=true` non offre
un salvataggio INI funzionante: se manca una sezione, `addSection` apre un file
con **il nome della sezione**, nella directory di lavoro, invece del file INI
originale. Non aggiorna il dizionario in memoria. `addParameter` è vuoto.
Lasciare `UpdateFile=false`; non usarlo per persistenza o override runtime.

Le risorse del lettore non sono protette da `using/finally`: errori durante il
parsing possono lasciare il file aperto fino alla raccolta del garbage collector.
Non è presente sincronizzazione per modifiche concorrenti.

