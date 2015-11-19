// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma once

#include "xplat.h"

namespace dnx
{
    class trace_writer
    {
    public:
        trace_writer(bool verbose) : m_verbose(verbose)
        {}

        void write(const dnx::char_t* entry, bool verbose)
        {
            if (!verbose || m_verbose)
            {
                xout << entry << std::endl;
            }
        }

        void write(const dnx::xstring_t& entry, bool verbose)
        {
            write(entry.c_str(), verbose);
        }

    private:
        bool m_verbose;
    };
}