
#if defined(_WIN32)
#define EXPORTED_API __declspec(dllexport)
#else
#define EXPORTED_API
#endif

extern "C" EXPORTED_API int get_number()
{
    return 42;
}
