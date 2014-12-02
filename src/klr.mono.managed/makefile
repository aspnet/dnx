CURDIR=`pwd`

all: compile

compile: 
	mcs EntryPoint.cs ../klr.hosting.shared/RuntimeBootstrapper.cs ../klr.hosting.shared/LoaderEngine.cs ../Microsoft.Framework.CommandLineUtils/CommandLine/CommandArgument.cs ../Microsoft.Framework.CommandLineUtils/CommandLine/CommandLineApplication.cs ../Microsoft.Framework.CommandLineUtils/CommandLine/CommandOption.cs ../Microsoft.Framework.CommandLineUtils/CommandLine/CommandOptionType.cs /target:exe /unsafe /out:../../artifacts/build/KRE-Mono/bin/klr.mono.managed.dll /r:"System;System.Core" /define:ASPNET50
