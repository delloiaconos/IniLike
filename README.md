# IniLike

IniLike is a standalone C# library for reading INI-like configuration files with
text tables. It targets .NET Framework 3.5. The namespace and assembly name are
**ConfigurationFilesReader**, and the public class is
`ConfigurationFilesReader.ConfigurationFile`.

## File format

```ini
## Full-line comment
[SERVER]
host = localhost;
port = 8080;
enabled = TRUE;

[DIRECTORIES]
data = ./data/;

[TABLE:ITEMS]
first; 10; active;
second; 20; inactive;
```

- `[SECTION]` contains `key = value` pairs.
- `[TABLE:NAME]` contains text rows. The library returns a `List<string>`;
  callers are responsible for splitting rows into columns.
- Section, key, and table names are **case-sensitive**. The `TABLE:` prefix
  must be uppercase. Sections and tables use separate dictionaries and may
  share a name.
- Blank lines and lines starting with `##` after trimming are ignored.
- Leading and trailing whitespace is trimmed from lines, section names, keys,
  and values. All trailing `;`, `,`, and `.` characters are then removed from
  values and table rows. Whitespace exposed by removing these delimiters is
  preserved.
- A key/value line must split into **exactly two parts** at `=`. Lines such as
  `expression = a=b;` are ignored.
- Quoting, escaping, multiline values, and inline comments are not supported.
  Quotes remain part of the value. A line such as `; comment` is treated as
  data inside a table.
- Duplicate sections, tables, or keys within a section cause an exception.
- Lines before the first section or table are ignored.
- Relative configuration file paths are resolved against the working directory.
  Paths stored as values are returned as text without resolution.

The trailing `.` delimiter can alter meaningful data: `value...` becomes
`value`, and a single `.` becomes an empty string. Use `./` to represent the
current directory. The parser supports the format described here rather than
a complete INI specification.

## Usage

Reference `ConfigurationFilesReader.dll` and use the public API:

```csharp
using ConfigurationFilesReader;
using System.Collections.Generic;

var config = new ConfigurationFile("config.ini");
string host = config.getParameter("SERVER", "host", "localhost");
int port = config.getParameter("SERVER", "port", 8080);
bool enabled = config.getParameter("SERVER", "enabled", false);
List<string> items = config.getTable("ITEMS");

// Create or update a parameter in memory. The source file is unchanged.
config.setParameter("DIRECTORIES", "data", "./other-data/");
```

To create a configuration entirely in memory:

```csharp
var config = new ConfigurationFile();
config.setParameter("SERVER", "port", "8080");
int port = config.getParameter("SERVER", "port", 80);
```

## API reference

Method names use camelCase: `checkSection`, `getParameter` (all overloads),
`addParameter`, `getTable`, and `setParameter`. This is a breaking API rename:
callers using PascalCase names (including `SetParameter`) must update their calls
and recompile.
The old method names are not retained as aliases. Parsing and in-memory behavior
are unchanged by this rename.

| Member | Behavior |
| --- | --- |
| `ConfigurationFile(string filename)` | Loads the file immediately. Missing files and parsing errors cause exceptions. |
| `ConfigurationFile()` | Creates an empty container without loading a file. |
| `checkSection(string)` | Checks for a section, excluding tables. Never creates sections or writes files. |
| `getParameter(section, key, string defaultValue)` | Returns the stored text, or the default if the section or key is missing. |
| `getParameter(..., bool)` | Only `TRUE`, ignoring case and surrounding whitespace, is true. Any other stored value is false, even when the default is true. |
| `getParameter(..., double/float)` | Parses using invariant culture after replacing commas with periods. Returns the default if parsing fails. |
| `getParameter(..., long/int)` | Parses an integer using invariant culture. Returns the default if parsing fails. The int overload parses as long and then performs an unchecked cast, so values outside the int range can wrap instead of returning the default. |
| `getTable(name)` | Returns the mutable internal list. For a missing table, returns a new empty list that is not attached to the container. |
| `setParameter(string sectionName, string parameterName, string value)` | Creates missing sections and keys or replaces an existing value. Stores text without parsing or trimming and preserves case-sensitive names. Never writes files. |
| `addParameter(section, key, defaultValue)` | Adds a missing parameter in memory, creating its section if needed. Preserves existing values, including null. Stores text unchanged and never writes files, regardless of `autoUpdateFile`. Null section/key names throw `ArgumentNullException`; empty names are allowed. |


## Limitations

Parser settings are exposed as public readonly fields:

```csharp
char[] separators = config.parSeparator;
char[] delimiters = config.parEndLineDelimiter;
bool automaticUpdates = config.autoUpdateFile;
```

The array references cannot be reassigned, but their elements remain mutable.
They default to `=` and `; , .`. Changing them after construction does not reload
or change already parsed values. There is no public reload method.
`autoUpdateFile` is initialized to false and cannot be reassigned by callers.
There are no accessor properties or getter/setter methods for these fields.

`setParameter` supports in-memory overrides. Subsequent `getParameter` calls
use the new values and the usual type conversions. Changes are not persisted
to disk.

`addParameter` adds defaults only when a parameter is absent; `setParameter`
also replaces existing values. Neither method writes files.

With the current constructors, `autoUpdateFile` is always false: getters return
missing defaults without storing them. Use `addParameter` or `setParameter` to
change data in memory. `checkSection` remains a pure existence check. No operation
saves changes to disk.

File readers are disposed through `using` blocks, including when parsing throws
an exception. Concurrent changes are not synchronized.

## Build

With GNU Make and Mono installed, use:

```sh
make build
make clean
make test
```

`build` is the default target. `clean` removes the library's entire `bin` and
`obj` directories, including Debug, Release, and stale build artifacts.
`test` runs the Python test runner, which builds its own temporary copy of the
library. Build and test default to Release; select Debug
with `CONFIGURATION=Debug`, for example `make test CONFIGURATION=Debug`.
Tests also require Python 3; override its command with `PYTHON=python3` if needed.

`IniLike.sln` contains the library project. Build it with a toolchain that
supports .NET Framework 3.5, for example Mono:

```sh
xbuild IniLike.sln /p:Configuration=Release
```

The resulting assembly is
`ConfigurationFilesReader/bin/Release/ConfigurationFilesReader.dll`.

## Automated tests

Prerequisites: Python 3 and Mono, with `xbuild`, `mcs`, and `mono` available on
`PATH`. No additional Python packages or test frameworks are required.

Run from the repository root:

```sh
python3 tests/run.py
python3 tests/run.py --configuration Debug
```

The runner builds the solution from source in a temporary directory and tests
the resulting assembly using `tests/ConfigurationFileTests.cs`. Each test runs
in an isolated directory that is removed afterward. Tests verify that the readonly `autoUpdateFile` flag
does not cause file writes. Build artifacts are kept outside the repository. The command
prints a result summary and exits with a nonzero status if a build or test fails.

The suite covers:

- Sections, tables, mutable lists, and transitions between sections and tables.
- Comments, whitespace, delimiters, Unicode, and case-sensitive names.
- Defaults for every getter overload, boolean conversion, and numeric conversion
  under three cultures, including integer boundaries and overflow behavior.
- Empty files, missing files, and duplicate sections, tables, and keys.
- In-memory overrides and source file preservation.
- The documented behavior of `addParameter`, the readonly `autoUpdateFile` flag, and mutable elements of readonly delimiter arrays.
