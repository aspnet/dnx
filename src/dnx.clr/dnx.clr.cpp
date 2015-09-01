// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// dnx.clr.cpp : Defines the exported functions for the DLL application.

#include "stdafx.h"

#include "KatanaManager.h"
#include "app_main.h"

IKatanaManagerPtr g_katanaManager;

extern "C" __declspec(dllexport) HRESULT __stdcall CallApplicationMain(PCALL_APPLICATION_MAIN_DATA data)
{
    HRESULT hr = S_OK;

    IKatanaManagerPtr manager = new ComObject<KatanaManager, IKatanaManager>();

    g_katanaManager = manager;

    hr = manager->InitializeRuntime(data->runtimeDirectory, data->applicationBase);
    if (SUCCEEDED(hr))
    {
        g_katanaManager = NULL;
        data->exitcode = manager->CallApplicationMain(data->argc, data->argv);
    }
    else
    {
        printf_s("Failed to initialize runtime (%x)", hr);
        data->exitcode = hr;
    }

    return hr;
}

extern "C" __declspec(dllexport) HRESULT __stdcall BindApplicationMain(ApplicationMainInfo* pInfo)
{
    HRESULT hr = S_OK;

    // LOCK g_
    IKatanaManagerPtr katanaManager = g_katanaManager;

    _HR(katanaManager->BindApplicationMain(pInfo));
    return hr;
}
