CONFIG=Debug
CURDIR=`pwd`

all: compile

prepare-output:
	mkdir -p bin/mono/$(CONFIG)

compile: prepare-output
	mcs src/klr.mono.managed/EntryPoint.cs src/klr.hosting.shared/RuntimeBootstrapper.cs src/klr.hosting.shared/LoaderEngine.cs src/Microsoft.Net.CommandLineUtils/CommandLine/CommandLineParser.cs src/Microsoft.Net.CommandLineUtils/CommandLine/CommandOptions.cs src/Microsoft.Net.CommandLineUtils/CommandLine/CommandOptionType.cs /target:exe /unsafe /out:bin/mono/$(CONFIG)/klr.mono.managed.dll /r:"System;System.Core" /define:NET45
