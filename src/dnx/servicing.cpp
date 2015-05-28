// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include "version.h"
#include "utils.h"
#include <string>
#include <fstream>
#include <algorithm>
#include "TraceWriter.h"

namespace
{
    const std::string runtime_moniker =
#if defined(CORECLR_WIN)
        "coreclr"
#else
        "clr"
#endif
        "-win-"
#if defined(AMD64)
        "x64"
#elif defined(ARM)
        "arm"
#else
        "x86"
#endif
        ;

    std::wstring find_runtime_replacement(std::ifstream& input, TraceWriter& trace_writer)
    {
        const std::string runtime_qualifier = std::string{ "dnx|" } + runtime_moniker + "|" + ProductVersionStr + "=";

        for (std::string line; std::getline(input, line); )
        {
            if (line.compare(0, runtime_qualifier.length(), runtime_qualifier) == 0)
            {
                return dnx::utils::to_wstring(line.substr(runtime_qualifier.length()));
            }
        }

        if (!input.eof())
        {
            trace_writer.Write(L"Error occured while reading contents of servicing index file.", false);
        }

        return L"";
    }

    std::wstring get_full_replacement_path(const std::wstring& servicing_root, const std::wstring& runtime_replacement_path)
    {
        std::wstring full_replacement_path = dnx::utils::path_combine(servicing_root, runtime_replacement_path);
        std::replace(full_replacement_path.begin(), full_replacement_path.end(), L'/', L'\\');
        return full_replacement_path;
    }
}

namespace dnx
{
    namespace servicing
    {
        std::wstring get_runtime_path(const std::wstring& servicing_root_parent, TraceWriter& trace_writer)
        {
            auto servicing_root = utils::path_combine(servicing_root_parent, std::wstring(L"Microsoft DNX"));
            auto servicing_manifest_path = utils::path_combine(servicing_root, std::wstring(L"Servicing\\index.txt"));

            // index.txt is ASCII
            std::ifstream servicing_manifest;
            servicing_manifest.open(servicing_manifest_path, std::ifstream::in);
            if (servicing_manifest.is_open())
            {
                trace_writer.Write(std::wstring(L"Found servicing index file at: ").append(servicing_manifest_path), true);

                auto runtime_replacement_path = find_runtime_replacement(servicing_manifest, trace_writer);
                if (runtime_replacement_path.length() > 0)
                {
                    return get_full_replacement_path(servicing_root, runtime_replacement_path);
                }

                trace_writer.Write(L"No runtime redirections found.", true);
            }
            else
            {
                trace_writer.Write(
                    std::wstring(L"The servicing index file at: ")
                    .append(servicing_manifest_path)
                    .append(L" does not exist or could not be opened."),
                    true);
            }

            return std::wstring{};
        }
    }
}