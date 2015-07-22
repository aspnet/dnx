CURDIR=`pwd`

all: compile

compile: 
	mcs EntryPoint.cs ../dnx.hosting.shared/RuntimeBootstrapper.cs ../dnx.hosting.shared/LoaderEngine.cs ../Microsoft.Framework.CommandLineUtils.Sources/CommandLine/CommandArgument.cs ../Microsoft.Framework.CommandLineUtils.Sources/CommandLine/CommandLineApplication.cs ../Microsoft.Framework.CommandLineUtils.Sources/CommandLine/CommandOption.cs ../Microsoft.Framework.CommandLineUtils.Sources/CommandLine/CommandOptionType.cs /target:exe /unsafe /out:../../artifacts/build/dnx-mono/bin/dnx.mono.managed.dll /r:"System;System.Core" /define:ASPNET50
