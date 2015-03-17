#pragma once

BOOL CreateTpaBase(LPCTSTR** ppNames, size_t* pcNames, bool bNative);
BOOL FreeTpaBase(const LPCTSTR* values, const size_t count);
