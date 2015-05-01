// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma once

class CriticalSection : public CRITICAL_SECTION
{
public:
    CriticalSection()
    {
        InitializeCriticalSection(this);
    }
    ~CriticalSection()
    {
        DeleteCriticalSection(this);
    }
};

class Lock
{
    CRITICAL_SECTION* _criticalSection;
public:
    Lock(CRITICAL_SECTION* criticalSection)
    {
        _criticalSection = criticalSection;
        EnterCriticalSection(_criticalSection);
    }
    ~Lock()
    {
        LeaveCriticalSection(_criticalSection);
    }
};
