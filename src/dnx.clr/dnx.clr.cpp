// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// dnx.clr.cpp : Defines the exported functions for the DLL application.

#include "stdafx.h"

#include "ClrBootstrapper.h"
#include "app_main.h"

IClrBootstrapperPtr g_clrBootstrapper;

extern "C" __declspec(dllexport) HRESULT __stdcall CallApplicationMain(PCALL_APPLICATION_MAIN_DATA data)
{
    HRESULT hr = S_OK;

    IClrBootstrapperPtr bootstrapper = new ComObject<ClrBootstrapper, IClrBootstrapper>();

    g_clrBootstrapper = bootstrapper;

    auto framework = dnx::utils::get_option_value(data->argc, const_cast<wchar_t**>(data->argv), L"--framework");

    hr = bootstrapper->InitializeRuntime(data->runtimeDirectory, data->applicationBase, framework);
    if (SUCCEEDED(hr))
    {
        g_clrBootstrapper = NULL;
        data->exitcode = bootstrapper->CallApplicationMain(data->argc, data->argv);
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
    IClrBootstrapperPtr bootstrapper = g_clrBootstrapper;

    _HR(bootstrapper->BindApplicationMain(pInfo));
    return hr;
}
