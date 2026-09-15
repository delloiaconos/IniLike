CONFIGURATION ?= Release
PYTHON ?= python3

.PHONY: build clean test

build:
	xbuild IniLike.sln /target:Build /p:Configuration=$(CONFIGURATION)

clean:
	rm -rf -- ConfigurationFilesReader/bin ConfigurationFilesReader/obj

test:
	$(PYTHON) tests/run.py --configuration $(CONFIGURATION)
