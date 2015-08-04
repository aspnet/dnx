// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include "version.h"
#include "utils.h"
#include <string>
#include <fstream>
#include <algorithm>
#include "trace_writer.h"

namespace
{
    const char* runtime_moniker =
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

    std::wstring find_runtime_replacement(std::ifstream& input, dnx::trace_writer& trace_writer)
    {
        const std::string runtime_qualifier = std::string{ "dnx|" } + runtime_moniker + "|" + ProductVersionStr + "=";

        trace_writer.write(
            std::wstring(L"Looking for redirections for runtime ")
                .append(dnx::utils::to_wstring(runtime_qualifier).substr(0, runtime_qualifier.length() - 1)), true);

        std::wstring runtime_replacement;
        for (std::string line; std::getline(input, line); )
        {
            if (line.compare(0, runtime_qualifier.length(), runtime_qualifier) == 0)
            {
                runtime_replacement = dnx::utils::to_wstring(line.substr(runtime_qualifier.length()));
            }
        }

        if (!input.eof())
        {
            trace_writer.write(L"Error occured while reading contents of servicing index file.", false);
        }

        return runtime_replacement;
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
        std::wstring get_runtime_path(const std::wstring& servicing_root_parent, bool is_default_servicing_location, dnx::trace_writer& trace_writer)
        {
            auto servicing_root = is_default_servicing_location
                ? utils::path_combine(servicing_root_parent, std::wstring(L"Microsoft DNX\\Servicing"))
                : servicing_root_parent;

            if (is_default_servicing_location && !utils::directory_exists(servicing_root))
            {
                trace_writer.write(L"The default servicing root does not exist.", true);
                return std::wstring{};
            }

            auto servicing_manifest_path = utils::path_combine(servicing_root, std::wstring(L"index.txt"));

            if (!utils::file_exists(servicing_manifest_path))
            {
                throw std::runtime_error(std::string("The servicing index file at: ")
                    .append(dnx::utils::to_string(servicing_manifest_path))
                    .append(" does not exist or is not accessible."));
            }

            // index.txt is ASCII
            std::ifstream servicing_manifest;
            servicing_manifest.open(servicing_manifest_path, std::ifstream::in);
            if (servicing_manifest.is_open())
            {
                trace_writer.write(std::wstring(L"Found servicing index file at: ").append(servicing_manifest_path), true);

                auto runtime_replacement_path = find_runtime_replacement(servicing_manifest, trace_writer);

                servicing_manifest.close();

                if (runtime_replacement_path.length() > 0)
                {
                    return get_full_replacement_path(servicing_root, runtime_replacement_path);
                }

                trace_writer.write(L"No runtime redirections found.", true);

                return std::wstring{};
            }

            throw std::runtime_error(std::string("The servicing index file at: ")
                .append(dnx::utils::to_string(servicing_manifest_path))
                .append(" could not be opened."));
        }
    }
}