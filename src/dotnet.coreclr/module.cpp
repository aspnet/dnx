// Simple CoreCLR host that runs on CoreSystem.
// Original ccrun code From Jan Kotas (CLR)
// Updates: jhawk

// module.cpp : Defines the exported functions for the DLL application.

#include "stdafx.h"

// Functions - Helpers - Dll Entrypoints

//Returns a pointer to a new object instance of the Firmware class
bool STDAPICALLTYPE FirmwareInit(Firmware **ppFirmware)
{
    bool fSuccess = true;
    Firmware *pFirmware = nullptr;

    if (ppFirmware == nullptr)
    {
        fSuccess = false;
        goto Finished;
    }

    //Set out param
    *ppFirmware = nullptr;

    pFirmware = new Firmware();
    if (pFirmware == nullptr)
    {
        fSuccess = false;
        goto Finished;
    }

    fSuccess = pFirmware->Init();
    if (!fSuccess) 
    {
        fSuccess = false;
        goto Finished;
    }

    *ppFirmware = pFirmware;

Finished:

    return fSuccess;
}

wchar_t* STDAPICALLTYPE FirmwareGetNamedValue(Firmware *pFirmware, wchar_t pwszKey)
{
    return pFirmware->GetNamedValue(pwszKey);
}

bool STDAPICALLTYPE FirmwareSetNamedValue(Firmware *pFirmware,wchar_t pwszKey, wchar_t pwszValue)
{
    return pFirmware->SetNamedValue(pwszKey, pwszValue);
}

bool STDAPICALLTYPE FirmwareStartup(Firmware *pFirmware)
{
    bool fSuccess = true;

    fSuccess = pFirmware->Startup();
    if (!fSuccess) 
    {
        fSuccess = false;
        goto Finished;
    }

Finished:

    return fSuccess;
}

bool STDAPICALLTYPE FirmwareExecute(Firmware *pFirmware)
{
    bool fSuccess = true;

    fSuccess = pFirmware->Execute();
    if (!fSuccess) 
    {
        fSuccess = false;
        goto Finished;
    }

Finished:

    return fSuccess;
}

bool STDAPICALLTYPE FirmwareShutdown(Firmware *pFirmware)
{
    bool fSuccess = true;
    int exitCode = 0;

    fSuccess = pFirmware->Shutdown();
    if (!fSuccess) 
    {
        exitCode = 1;
        pFirmware->SetExitCode(exitCode);
        goto Finished;
    }

Finished:    
    if (pFirmware != nullptr)
    {
        delete pFirmware;
        pFirmware = nullptr;
    }

    return fSuccess;
}

//Default Process Main. Can be replaced
bool STDAPICALLTYPE FirmwareProcessMain(const int argc, const wchar_t* argv[], int &exitCode)
{
    bool fSuccess = true;
    Firmware *pFirmware = nullptr;

    //Firmware
    fSuccess = FirmwareInit(&pFirmware);
    if (!fSuccess) 
    {
        goto Finished;
    }

    //CommandLine
    fSuccess = pFirmware->ProcessCommmandLine(argc, argv);
    if (!fSuccess) 
    {
        goto Finished;
    }

    //Call Managed HostStartup Delegate

    fSuccess = FirmwareStartup(pFirmware);
    //Call Managed HostStartup Delegate
    if (!fSuccess) 
    {
        goto Finished;
    }

    //Call Execute as this is ProcessMain launch
    fSuccess = FirmwareExecute(pFirmware);
    //Call Managed HostMain Delegate
    if (!fSuccess) 
    {
        goto Finished;
    }

    //Call Managed HostShutdown Delegate
    fSuccess = FirmwareShutdown(pFirmware);
    if (!fSuccess) 
    {
        goto Finished;
    }
    exitCode = pFirmware->GetExitCode();

Finished:  
    if (!fSuccess)
    {
    }

    if (pFirmware != nullptr)
    {
        pFirmware = nullptr;
    }

    return fSuccess;
}

//eof