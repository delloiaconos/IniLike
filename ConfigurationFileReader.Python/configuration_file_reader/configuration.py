"""Python implementation of IniLike's ConfigurationFile API."""

import base64
import os
from pathlib import Path

from ._parsing import Parsing
from ._persistence import Persistence, _UNSET
from ._support import NameMap, NullParameterValueException, convertValue, formatDefault, nameKey, ordinalKey, valueType, zeroValue


class ConfigurationFileReader(Parsing, Persistence):
    """Read INI-like blocks from a filename, pathlib path, or borrowed stream."""

    def __init__(self, Source=None, FileName=_UNSET, CaseSensitive=True):
        # Mirror the C# path+bool and empty-registry bool overloads.
        if isinstance(FileName, bool):
            CaseSensitive, FileName = FileName, _UNSET
        if isinstance(Source, bool):
            if FileName is not _UNSET:
                raise TypeError("An empty registry cannot have a filename.")
            CaseSensitive, Source = Source, None
        if not isinstance(CaseSensitive, bool):
            raise TypeError("CaseSensitive must be a bool.")
        self._caseSensitive = CaseSensitive
        self._comments = ["##"]
        self._separators = ["="]
        self.autoUpdateRegistry = False
        self.autoSaveRegistry = False
        self._path = None
        self._loading = set()
        self._blocks = {kind: NameMap(CaseSensitive) for kind in
                        ("section", "table", "list", "dictionary", "text", "encoded")}
        if Source is None:
            if FileName is not _UNSET:
                raise TypeError("A base directory is required with FileName.")
            return
        if hasattr(Source, "read"):
            if FileName is not _UNSET:
                raise TypeError("A stream cannot be combined with FileName.")
            source_name = getattr(Source, "name", None)
            label = os.path.abspath(source_name) if isinstance(source_name, (str, os.PathLike)) else "<stream>"
            self._loadReader(Source, label)
        else:
            if FileName is None:
                raise TypeError("FileName cannot be None.")
            if not os.fspath(Source):
                raise ValueError("The source path cannot be empty.")
            path = Path(Source) if FileName is _UNSET else Path(Source) / FileName
            self._path = Path(os.path.abspath(path))
            self._loadFile(self._path)

    @property
    def CaseSensitive(self):
        return self._caseSensitive

    @property
    def parComment(self):
        return self._comments

    @property
    def parSeparator(self):
        return self._separators

    def _listNames(self, Kind):
        return sorted(self._blocks[Kind], key=ordinalKey)

    def _tryBlock(self, Kind, Name):
        blocks = self._blocks[Kind]
        if Name not in blocks:
            return False, None
        value = blocks[Name]
        if Kind == "section":
            value = value.copy()
        elif Kind == "text":
            value = "\n".join(value)
        elif Kind == "encoded":
            value = bytes(value)
        return True, value

    def checkSection(self, SecName):
        return self.containsSection(SecName)

    def containsParameter(self, Section, Parameter):
        return self.tryGetParameter(Section, Parameter)[0]

    def tryGetParameter(self, Section, Parameter, ValueType=str):
        """Return (found/converted, value); typed stored nulls raise explicitly."""
        nameKey(Section, self.CaseSensitive)
        nameKey(Parameter, self.CaseSensitive)
        kind = valueType(ValueType)
        sections = self._blocks["section"]
        if Section not in sections or Parameter not in sections[Section]:
            return False, zeroValue(kind)
        value = sections[Section][Parameter]
        if kind != "string" and value is None:
            raise NullParameterValueException(Section, Parameter)
        return convertValue(value, kind, Strict=True)

    def getParameter(self, SecName, ParName, DefaultVal, ValueType=None):
        """Legacy getter, inferring the C# overload from the Python default type."""
        nameKey(SecName, self.CaseSensitive)
        nameKey(ParName, self.CaseSensitive)
        kind = valueType(ValueType if ValueType is not None else str if DefaultVal is None else type(DefaultVal))
        sections = self._blocks["section"]
        if SecName in sections and ParName in sections[SecName]:
            value = sections[SecName][ParName]
        else:
            value = formatDefault(DefaultVal, kind)
            if self.autoUpdateRegistry:
                self.addParameter(SecName, ParName, value)
        if kind == "string":
            return value
        if value is None:
            return DefaultVal
        success, converted = convertValue(value, kind, Strict=False)
        return converted if success else DefaultVal

    def setParameter(self, SecName, ParName, value):
        nameKey(SecName, self.CaseSensitive)
        nameKey(ParName, self.CaseSensitive)
        sections = self._blocks["section"]
        if SecName in sections and ParName in sections[SecName]:
            sections[SecName][ParName] = value
        elif self.autoUpdateRegistry:
            self.addParameter(SecName, ParName, value)

    def addParameter(self, SecName, ParName, DefaultVal):
        nameKey(SecName, self.CaseSensitive)
        nameKey(ParName, self.CaseSensitive)
        if not self.autoUpdateRegistry:
            return
        sections = self._blocks["section"]
        new_section = SecName not in sections
        if not new_section and ParName in sections[SecName]:
            return
        if self.autoSaveRegistry and self._path is None:
            raise ValueError("Automatic saving requires an original configuration file.")
        # Validate before attaching a new section, including Python-only type errors.
        if DefaultVal is not None and not isinstance(DefaultVal, str):
            raise TypeError("Parameter values must be strings or None.")
        if new_section:
            sections[SecName] = NameMap(self.CaseSensitive, Strings=True)
        parameters = sections[SecName]
        parameters.add(ParName, DefaultVal)
        if self.autoSaveRegistry:
            try:
                self.save()
            except Exception:
                del parameters[ParName]
                if new_section:
                    del sections[SecName]
                raise

    def listParameters(self, SecName=_UNSET):
        sections = self._blocks["section"]
        if SecName is not _UNSET:
            return sorted(sections[SecName], key=ordinalKey) if SecName in sections else []
        names = NameMap(self.CaseSensitive)
        for section in sections.values():
            for name in section:
                names[name] = None
        return sorted(names, key=ordinalKey)

    def listSections(self):
        return self._listNames("section")

    def containsSection(self, Name):
        return Name in self._blocks["section"]

    def tryGetSection(self, Name):
        return self._tryBlock("section", Name)

    def listTables(self):
        return self._listNames("table")

    def containsTable(self, Name):
        return Name in self._blocks["table"]

    def tryGetTable(self, Name):
        return self._tryBlock("table", Name)

    def getTable(self, Name):
        found, value = self.tryGetTable(Name)
        return value if found else []

    def listLists(self):
        return self._listNames("list")

    def containsList(self, Name):
        return Name in self._blocks["list"]

    def tryGetList(self, Name):
        return self._tryBlock("list", Name)

    def getList(self, Name):
        found, value = self.tryGetList(Name)
        return value if found else []

    def listDictionaries(self):
        return self._listNames("dictionary")

    def containsDictionary(self, Name):
        return Name in self._blocks["dictionary"]

    def tryGetDictionary(self, Name):
        return self._tryBlock("dictionary", Name)

    def getDictionary(self, Name):
        found, value = self.tryGetDictionary(Name)
        return value if found else NameMap(self.CaseSensitive, Strings=True)

    def listTexts(self):
        return self._listNames("text")

    def containsText(self, Name):
        return Name in self._blocks["text"]

    def tryGetText(self, Name):
        return self._tryBlock("text", Name)

    def getText(self, Name):
        found, value = self.tryGetText(Name)
        return value if found else ""

    def listEncoded(self):
        return self._listNames("encoded")

    def containsEncoded(self, Name):
        return Name in self._blocks["encoded"]

    def tryGetEncoded(self, Name, Encoded=False):
        found, data = self._tryBlock("encoded", Name)
        return found, base64.b64encode(data) if found and Encoded else data

    def getEncoded(self, BlockName, encoded=False):
        found, data = self.tryGetEncoded(BlockName, encoded)
        return data if found else b""


# Preserve the established C# entry point as an equivalent Python spelling.
ConfigurationFile = ConfigurationFileReader
