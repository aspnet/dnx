// klr.net45.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"

#include "KatanaManager.h"
#include "..\klr\klr.h"

IKatanaManagerPtr g_katanaManager;


extern "C" __declspec(dllexport) HRESULT __stdcall CallApplicationMain(PCALL_APPLICATION_MAIN_DATA data)
{
    HRESULT hr = S_OK;
    
    IKatanaManagerPtr manager = new ComObject<KatanaManager, IKatanaManager>();

    g_katanaManager = manager;
    _HR(manager->InitializeRuntime(data->applicationBase));
    g_katanaManager = NULL;

    _HR(manager->CallApplicationMain(data->argc, data->argv));

    data->exitcode = hr;

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
