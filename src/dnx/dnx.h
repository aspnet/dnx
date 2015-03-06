
typedef struct CALL_APPLICATION_MAIN_DATA
{
    LPCTSTR applicationBase; // application base of managed domain
    LPCTSTR runtimeDirectory; // path to runtime helper directory
    int argc; // Number of args in argv
    LPCTSTR* argv; // Array of arguments
    int exitcode; // Exit code from Managed Application
} *PCALL_APPLICATION_MAIN_DATA;

typedef HRESULT (STDAPICALLTYPE *FnCallApplicationMain)(
    PCALL_APPLICATION_MAIN_DATA pCallApplicationMainData
    );
