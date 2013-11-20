
typedef bool (STDAPICALLTYPE *FnCallApplicationMain)(
    const int argc,             // Number of args in argv
    const wchar_t* argv[],      // Array of arguments
    int &exitCode               // Exit code from Managed Application
    );
