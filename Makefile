CONFIGURATION ?= Release
PYTHON ?= python3
MSBUILD ?= msbuild
XBUILD ?= xbuild

# Without an explicit MSBUILD override, let the test runner detect the toolchain.
ifneq ($(origin MSBUILD),file)
TEST_BUILD_ARGS = --msbuild "$(MSBUILD)"
endif

.PHONY: x-build ms-build clean test

ms-build:
	"$(MSBUILD)" IniLike.sln /target:Build /p:Configuration=$(CONFIGURATION)

x-build:
	"$(XBUILD)" IniLike.sln /target:Build /p:Configuration=$(CONFIGURATION)

clean:
	rm -rf -- ConfigurationFilesReader/bin ConfigurationFilesReader/obj

test:
	$(PYTHON) tests/run.py --configuration $(CONFIGURATION) $(TEST_BUILD_ARGS) --xbuild "$(XBUILD)"
