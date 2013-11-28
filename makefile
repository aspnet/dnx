CC=g++
CFLAGS=-m32 `pkg-config --cflags mono-2`
LDFLAGS=`pkg-config --libs mono-2`
CONFIG=Debug
CURDIR=`pwd`

all: compile

prepare-output:
	mkdir -p bin/$(CONFIG)

build-host:
	xbuild src/klr.host/klr.host.csproj /p:OutputPath=$(CURDIR)/bin/$(CONFIG);Configuration=$(CONFIG)

compile-managed: prepare-output build-host
	mcs src/klr.mono.managed/EntryPoint.cs /target:exe /unsafe /out:bin/$(CONFIG)/klr.mono.managed.dll /r:"System;System.Core"

compile: compile-managed
	$(CC) $(CFLAGS) $(LDFLAGS) src/klr.mono/klr.cpp -o bin/$(CONFIG)/klr
