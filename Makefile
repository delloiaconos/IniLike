CONFIGURATION ?= Release
PYTHON ?= python3
MSBUILD ?= msbuild
XBUILD ?= xbuild

# Without an explicit MSBUILD override, let the test runner detect the toolchain.
ifneq ($(origin MSBUILD),file)
TEST_BUILD_ARGS = --msbuild "$(MSBUILD)"
endif

.PHONY: x-build ms-build clean test test-examples test-python

ms-build:
	"$(MSBUILD)" IniLike.sln /target:Build /p:Configuration=$(CONFIGURATION)

x-build:
	"$(XBUILD)" IniLike.sln /target:Build /p:Configuration=$(CONFIGURATION)

clean:
	rm -rf -- ConfigurationFilesReader/bin ConfigurationFilesReader/obj ConfigurationValidator/bin ConfigurationValidator/obj

test:
	$(PYTHON) tests/run.py --configuration $(CONFIGURATION) $(TEST_BUILD_ARGS) --xbuild "$(XBUILD)"

test-examples:
	$(PYTHON) tests/run.py --examples-only --configuration $(CONFIGURATION) $(TEST_BUILD_ARGS) --xbuild "$(XBUILD)"

# Python-only tests; cross-language checks run when mcs and mono are available.
test-python:
	$(PYTHON) -B -m unittest discover -s ConfigurationFileReader.Python/tests -v
