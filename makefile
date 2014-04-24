CURDIR=`pwd`

all: compile

prepare-output:
	mkdir -p artifacts/build/klr.mono.managed

compile: prepare-output
	mcs src/klr.mono.managed/EntryPoint.cs src/klr.hosting.shared/RuntimeBootstrapper.cs src/klr.hosting.shared/LoaderEngine.cs src/Microsoft.Net.CommandLineUtils/CommandLine/CommandLineParser.cs src/Microsoft.Net.CommandLineUtils/CommandLine/CommandOptions.cs src/Microsoft.Net.CommandLineUtils/CommandLine/CommandOptionType.cs /target:exe /unsafe /out:artifacts/build/klr.mono.managed/klr.mono.managed.dll /r:"System;System.Core" /define:NET45
