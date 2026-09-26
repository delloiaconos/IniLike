CONFIGURATION ?= Release
PYTHON ?= python3
MSBUILD ?= msbuild
XBUILD ?= xbuild


.PHONY: x-build ms-build clean test

ms-build:
	"$(MSBUILD)" IniLike.sln /target:Build /p:Configuration=$(CONFIGURATION)

x-build:
	"$(XBUILD)" IniLike.sln /target:Build /p:Configuration=$(CONFIGURATION)

clean:
	rm -rf -- ConfigurationFilesReader/bin ConfigurationFilesReader/obj

test:
	$(PYTHON) tests/run.py --configuration $(CONFIGURATION)
