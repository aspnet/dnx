// klr.net45.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"

#include "KatanaManager.h"

IKatanaManagerPtr g_katanaManager;


extern "C" __declspec(dllexport) bool __stdcall CallApplicationMain(int argc, PCWSTR* argv, int& retval)
{
    HRESULT hr = S_OK;
    
    IKatanaManagerPtr manager = new ComObject<KatanaManager, IKatanaManager>();

    g_katanaManager = manager;
    _HR(manager->InitializeRuntime());
    g_katanaManager = NULL;

    _HR(manager->CallApplicationMain(argc, argv));

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
