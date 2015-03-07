CURDIR=`pwd`

all: compile

compile: 
	mcs EntryPoint.cs ../dnx.hosting.shared/RuntimeBootstrapper.cs ../dnx.hosting.shared/LoaderEngine.cs ../Microsoft.Framework.CommandLineUtils/CommandLine/CommandArgument.cs ../Microsoft.Framework.CommandLineUtils/CommandLine/CommandLineApplication.cs ../Microsoft.Framework.CommandLineUtils/CommandLine/CommandOption.cs ../Microsoft.Framework.CommandLineUtils/CommandLine/CommandOptionType.cs /target:exe /unsafe /out:../../artifacts/build/dnx-mono/bin/dnx.mono.managed.dll /r:"System;System.Core" /define:ASPNET50
