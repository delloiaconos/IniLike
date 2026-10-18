"""IniLike configuration files for Python, under LGPL-3.0."""

from .configuration import ConfigurationFile, ConfigurationFileReader
from ._support import ConfigurationError, NullParameterValueException

__all__ = ["ConfigurationFile", "ConfigurationFileReader", "ConfigurationError", "NullParameterValueException"]
