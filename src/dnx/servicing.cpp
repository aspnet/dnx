// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include "version.h"
#include "utils.h"
#include <string>
#include <fstream>
#include <algorithm>
#include <thread>
#include <chrono>
#include "trace_writer.h"

#if defined(ONECORE)
#include <Pathcch.h>
#else
#include <Shlwapi.h>
#endif

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

    std::wstring find_runtime_replacement(std::ifstream& input, dnx::trace_writer& trace_writer, bool& completed_successfully)
    {
        completed_successfully = true;
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
            completed_successfully = false;
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
        bool require_servicing()
        {
            wchar_t require_servicing_buffer[2] = { 0 , 0 };
            auto require_servicing_buffer_length = GetEnvironmentVariable(L"DNX_REQUIRE_SERVICING", require_servicing_buffer, 2);

            if (require_servicing_buffer_length > 1 ||
                (require_servicing_buffer_length == 1 && (require_servicing_buffer[0] != L'1' && require_servicing_buffer[0] != L'0')))
            {
                throw std::runtime_error("The value of the DNX_REQUIRE_SERVICING environment variable is invalid. Accepted values: '0' or '1'. The application will now exit.");
            }

            return require_servicing_buffer_length != 0 && require_servicing_buffer[0] == L'1';
        }

        bool is_network_path(const std::wstring& path)
        {
            wchar_t buff[MAX_PATH];
            return
#if defined(ONECORE)
                PathIsUNCEx(path.c_str(), nullptr)
#else
                PathIsUNC(path.c_str())
#endif
                || (GetVolumePathName(path.c_str(), buff, MAX_PATH) && GetDriveType(buff) == DRIVE_REMOTE);
        }

        std::wstring get_runtime_path(const std::wstring& servicing_root_parent, bool is_default_servicing_location, dnx::trace_writer& trace_writer)
        {
            auto servicing_root = is_default_servicing_location
                ? utils::path_combine(servicing_root_parent, std::wstring(L"Microsoft DNX\\Servicing"))
                : servicing_root_parent;

            auto servicing_root_exists = utils::directory_exists(servicing_root);

            if (!servicing_root_exists)
            {
                if (require_servicing())
                {
                    throw std::runtime_error("Servicing is required for the application to run but the servicing folder does not exist.");
                }

                if (is_default_servicing_location)
                {
                    trace_writer.write(L"The default servicing root does not exist.", true);
                    return std::wstring{};
                }
            }

            if (!is_default_servicing_location && is_network_path(servicing_root))
            {
                throw std::runtime_error("Network paths cannot be used as servicing roots.");
            }

            auto servicing_manifest_path = utils::path_combine(servicing_root, std::wstring(L"index.txt"));

            if (!utils::file_exists(servicing_manifest_path))
            {
                throw std::runtime_error("The servicing index does not exist or is not accessible.");
            }

            for (auto i = 0; i < 3; i++)
            {
                trace_writer.write(std::wstring(L"Reading servicing index file - attempt: ")
                    .append(dnx::utils::to_xstring_t(std::to_string(i + 1))).append(L" of 3"), true);

                // index.txt is ASCII
                std::ifstream servicing_manifest;
                servicing_manifest.open(servicing_manifest_path, std::ifstream::in);
                if (servicing_manifest.is_open())
                {
                    trace_writer.write(std::wstring(L"Found servicing index file at: ").append(servicing_manifest_path), true);

                    auto reading_successful = true;
                    auto runtime_replacement_path = find_runtime_replacement(servicing_manifest, trace_writer, reading_successful);

                    if (reading_successful)
                    {
                        if (runtime_replacement_path.length() > 0)
                        {
                            return get_full_replacement_path(servicing_root, runtime_replacement_path);
                        }

                        trace_writer.write(L"No runtime redirections found.", true);

                        return std::wstring{};
                    }

                    std::this_thread::sleep_for(std::chrono::milliseconds(100));
                }
            }

            throw std::runtime_error("The servicing index file could not be opened or read.");
        }
    }
}