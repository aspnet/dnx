void GetNativeBootstrapperDirectory(LPTSTR szPath);
void WaitForDebuggerToAttach();
bool IsTracingEnabled();
BOOL GetAppBasePathFromEnvironment(LPTSTR szPath);
BOOL GetFullPath(LPCTSTR szPath, LPTSTR szFullPath);
HMODULE LoadNativeHost(LPCTSTR szHostModuleName);
BOOL FreeNativeHost(HMODULE hHost);
FARPROC GetEntryPointFromHost(HMODULE hModule, LPCSTR lpProcName);

#ifndef SetEnvironmentVariable
BOOL SetEnvironmentVariable(LPCTSTR lpName, LPCTSTR lpValue);
#endif //SetEnvironmentVariable
