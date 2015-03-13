#include "stdafx.h"
#include "errno.h"

int _tprintf_s(LPCTSTR format, ...)
{
    va_list args;
    va_start(args, format);
    int ret = vprintf(format, args);
    va_end(args);

    return ret;
}

int _tcscpy_s(LPTSTR strDestination, size_t numberOfElements, LPCTSTR strSrc)
{
    if (strDestination == NULL || strSrc == NULL)
    {
        return EINVAL;
    }

    if (numberOfElements == 0)
    {
        return ERANGE;
    }

    strncpy(strDestination, strSrc, numberOfElements);


    // strncpy will write null bytes to fill up strDestination if there
    // was extra space.
    if(strDestination[numberOfElements - 1] != '\0')
    {
        strDestination[0] = '\0';
        return ERANGE;
    }

    return 0;
}
