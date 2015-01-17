
typedef struct CALL_APPLICATION_MAIN_DATA
{
    LPCWSTR applicationBase; // application base of managed domain
    LPCWSTR runtimeDirectory; // path to runtime helper directory
    int argc; // Number of args in argv
    LPCWSTR* argv; // Array of arguments
    int exitcode; // Exit code from Managed Application
} *PCALL_APPLICATION_MAIN_DATA;

typedef HRESULT (STDAPICALLTYPE *FnCallApplicationMain)(
    PCALL_APPLICATION_MAIN_DATA pCallApplicationMainData
    );
