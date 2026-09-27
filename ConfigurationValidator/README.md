# ConfigurationValidator

[Back to IniLike](../README.md)

Run the commands below from the repository root. For prerequisites and build
overrides, see the [build instructions](../README.md#build) and
[test toolchain requirements](../README.md#automated-tests).

## Build and basic usage

`ConfigurationValidator` loads a file through `ConfigurationFile` and reports
whether the library accepts it. Build the solution with `make ms-build` (or
`make x-build` when only legacy xbuild is installed), then run:

```sh
mono ConfigurationValidator/bin/Release/ConfigurationValidator.exe examples/bench.ini
mono ConfigurationValidator/bin/Release/ConfigurationValidator.exe --help
```

On Windows, run `ConfigurationValidator.exe "C:\path with spaces\file.ini"`
directly. Keep `ConfigurationFilesReader.dll` and `ConfigurationValidator.exe.config`
alongside the executable; the project build copies them there. The executable
targets .NET Framework 3.5; its runtime configuration permits CLR 4 or CLR 2.

## Listing names

To inspect names in a configuration, add one or more listing options before or
after the file path:

```sh
mono ConfigurationValidator/bin/Release/ConfigurationValidator.exe examples/bench.ini --sections --parameters
```

| Option | Output |
| --- | --- |
| `--sections` | Section names, including plain, `SEC` and `SECTION` headers. |
| `--dictionaries` | Dictionary names (`DICT` or `DICTIONARY`). |
| `--tables` | Table names (`TBL` or `TABLE`). |
| `--lists` | List names (`LST` or `LIST`). |
| `--texts` | Text block names (`TEXT` or `TXT`). |
| `--parameters` | Distinct parameter names across all sections, excluding dictionary entries and table/list/text contents. |

Listing options replace the normal success summary with category headings and
one name per line, sorted ordinally and case-sensitively. Values are not printed;
parameters shared by multiple sections appear once. Empty categories print only
their heading. Combined options appear in the order shown above, and repeated
options print once. Loading and error reporting remain the same, with no partial
listing if loading fails. Unknown options return exit code 2. Use `--` before a
file path beginning with `-`; use `--help` or `-h` alone for help.

## Rewriting configurations

Use `--rewrite` to serialize the loaded configuration to a file or standard output:

```sh
mono ConfigurationValidator/bin/Release/ConfigurationValidator.exe input.ini --rewrite output.ini
mono ConfigurationValidator/bin/Release/ConfigurationValidator.exe input.ini --rewrite "backups/output.ini"
mono ConfigurationValidator/bin/Release/ConfigurationValidator.exe input.ini --rewrite
```

The first positional path is always the input; the optional second path is the
output. A filename without a directory is relative to the working directory.
Parent directories must already exist. With no output path, stdout contains only
the serialized configuration, suitable for piping. Writing to a file produces no
success output. `--rewrite` cannot be combined with listing options.

Rewriting uses the library's `dump` for stdout and `save` for file destinations.
It normalizes headers and ordering and discards comments and original formatting
outside raw text content, as described in the [library saving reference](../README.md#saving).
File output validates round-trip fidelity before
replacing an existing destination; specifying the input path as the destination
rewrites it in place. Without an explicit matching destination, the input is
unchanged. Loading or writing failures are reported on stderr with exit code 1.

## Diagnostics and exit codes

Without listing or rewrite options, successful loading prints section, table,
list, dictionary and text block counts to stdout. Failures print
the exception type and original library message (including inner exceptions) to
stderr, without a stack trace. The first loading error stops validation; the tool
does not save or modify the input file unless explicitly requested with `--rewrite`.

| Exit code | Meaning |
| --- | --- |
| `0` | File loaded/listed/rewritten successfully, or help displayed. |
| `1` | Loading or writing failed. |
| `2` | Invalid arguments, unknown options or incompatible options. |

Validation follows the existing parser's permissive rules. For example, lines
outside blocks or section lines with multiple `=` characters are ignored, not
reported as syntax errors. Success means the library can load the file; it does
not guarantee that every line was consumed or that application-specific values
are valid. Duplicates and mismatched `END` labels are reported as library errors.

## Validate all examples

Validate every `.ini` file directly inside `examples/` with one command:

```sh
make test-examples
```

This target builds the validator in a temporary directory, checks every example
even if another fails, and returns a nonzero status if any file fails (or none
are found). It does not modify the examples. Like `make test`, it supports
`CONFIGURATION`, `PYTHON`, `MSBUILD` and `XBUILD` and prefers MSBuild with xbuild
fallback. It runs only the example checks, not the full regression suite.
