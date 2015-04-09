// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma once

#include "xplat.h"

class TraceWriter
{
public:
    TraceWriter(bool verbose) : m_verbose(verbose)
    {}

    void Write(const dnx::char_t* entry, bool verbose)
    {
        if (!verbose || m_verbose)
        {
            xout << entry << std::endl;
        }
    }

    void Write(const dnx::xstring_t& entry, bool verbose)
    {
        Write(entry.c_str(), verbose);
    }

private:
    bool m_verbose;
};
