# IniLike

Libreria C# per leggere un formato INI esteso con tabelle testuali. Il namespace
e il nome dell'assembly sono **ConfigurationFilesReader**; la classe pubblica è
`ConfigurationFilesReader.ConfigurationFile`.

IniLike mantiene il parser storico e aggiunge `SetParameter`, compatibile con
il metodo già utilizzato in OperatorUI, per impostare parametri in memoria.
OperatorUI continua a compilare la propria copia della libreria: questo
aggiornamento non modifica i riferimenti del suo progetto.

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

// Crea o aggiorna il parametro in memoria; config.ini resta invariato.
config.SetParameter("DIRECTORIES", "TestConfigDir", "./altre-ricette/");
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
| `SetParameter(string sectionName, string parameterName, string value)` | Crea la sezione e la chiave se mancanti; sovrascrive il valore se presente. Memorizza il testo senza parsing o trim, rispettando maiuscole/minuscole dei nomi. Non scrive file, anche con `UpdateFile=true`. |
| `addParameter(...)` | Metodo dichiarato ma **non implementato**: non modifica memoria né file. |
| `UpdateFile` | Campo pubblico, default false; vedere i limiti sotto. |
| `parSeparator`, `parEndLineDelimiter` | Array pubblici, default `=` e `; , .`. Il caricamento avviene nel costruttore, prima che il chiamante possa cambiarli; manca un metodo pubblico di reload. |

## Limiti della scrittura

`SetParameter` permette override in memoria, anche su un contenitore creato con
`ConfigurationFile()`. Le successive letture con `getParameter` utilizzano il
nuovo valore e le consuete conversioni di tipo. Non è prevista persistenza
delle modifiche su disco.

`UpdateFile=true` non offre
un salvataggio INI funzionante: se manca una sezione, `addSection` apre un file
con **il nome della sezione**, nella directory di lavoro, invece del file INI
originale. Non aggiorna il dizionario in memoria. `addParameter` è vuoto.
Lasciare `UpdateFile=false`; per gli override runtime usare `SetParameter`.

Le risorse del lettore non sono protette da `using/finally`: errori durante il
parsing possono lasciare il file aperto fino alla raccolta del garbage collector.
Non è presente sincronizzazione per modifiche concorrenti.

## Compilazione e test automatici

`IniLike.sln` contiene il solo progetto della libreria .NET Framework 3.5.
Il vecchio progetto dimostrativo `ConfiguratioFiles_Tester` è sostituito dalla
suite di regressione in `tests`.

Prerequisiti per la suite: Python 3 e Mono con `xbuild`, `mcs` e `mono` nel PATH.
Non sono necessari pacchetti Python o framework di test aggiuntivi.

```sh
python3 tests/run.py
python3 tests/run.py --configuration Debug
```

Il runner compila la soluzione dai sorgenti in una directory temporanea e testa
l'assembly risultante tramite `ConfigurationFileTests.cs`. Ogni caso usa una
cartella isolata, eliminata al termine; anche i file creati dal comportamento
storico di `UpdateFile` restano in questa cartella. Gli artefatti di compilazione
non vengono scritti nel repository. Il comando restituisce un codice diverso
da zero se la compilazione o un test fallisce e stampa un riepilogo dei risultati.

La suite copre sezioni, tabelle e liste modificabili, commenti, delimitatori,
Unicode, sensibilità alle maiuscole, default di tutti gli overload, conversioni
numeriche in tre culture, limiti interi, booleani, file mancanti, duplicati,
override in memoria e assenza di persistenza. I test fissano anche i limiti
storici descritti sopra, compresi il cast int e gli effetti di `UpdateFile`.

La verifica aggiuntiva di compatibilità con una build di OperatorUI dotata di
`SetParameter` è opzionale:

```sh
python3 tests/run.py --operator-ui /percorso/OperatorUI.exe
```

Questo comando esegue anche `tests/CompatibilityProbe.cs` confrontando le due
implementazioni. La suite ordinaria non richiede OperatorUI.
