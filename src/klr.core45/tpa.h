#pragma once

BOOL CreateTpaBase(LPWSTR** ppNames, size_t* pcNames, bool bNative);
BOOL FreeTpaBase(const LPWSTR* values, const size_t count);