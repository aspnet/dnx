// stdafx.h : include file for standard system include files,
// or project specific include files that are used frequently, but
// are changed infrequently
//

#pragma once

#include "targetver.h"

#define _CRT_SECURE_NO_WARNINGS

// Windows Headers
// Exclude rarely-used stuff from Windows headers
#define WIN32_LEAN_AND_MEAN
#include <windows.h>

// C++ Headers
#include <string>
#include <list>
#include <map>
#include <utility>

// STL
using namespace std;

// CRT Headers
#include <stdio.h>
#include <strsafe.h>

// CLR Headers
#include <mscoree.h>

// Defines
#define MAX_STR MAX_PATH

// Tracing Headers
#define HOST_TRACE_MESSAGE_ENABLED
#include "Trace.h"

#pragma once

// class forward definitions
class Firmware;
class PackageLoader;
class ClrHostRuntimeModule;
class ClrDomainInstance;

#include "Firmware.h"
#include "PackageLoader.h"
#include "ClrDomainInstance.h"
#include "ClrHostRuntimeModule.h"