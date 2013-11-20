#pragma once

class PackageLoader
{
private:
    const wchar_t* niExtension;
    const wchar_t* ilExtension;

    Firmware* m_pFirmware;

    wchar_t** m_rgClrDefaultAssembliesList;
    wchar_t* m_rgClrDefaultAssemblies;
    wchar_t* m_pwszManifestAssemblyList;

    //List of package override rules
    map<wstring,wstring> m_mapPackageOverrideRules;

public:
    PackageLoader(void);
    ~PackageLoader(void);

    bool Init(Firmware* pFirmware);
    bool Dispose();

    const wchar_t* GetClrEngineAssemblyList();
    const wchar_t* GetPackagesAssemblyList();

    void ProbeAssembly(
        const wchar_t*  directoryPath, 
        const wchar_t*  assemblyName,
        std::wstring &result
        );

    bool UseLoadLibraryToGetFinalFilePath(
        const wchar_t* pwszFilename, 
        wchar_t* pwszFilePath, 
        size_t cchFilePath);

    bool FileExists(const wchar_t* pwszFilepath);

    bool DoesAssemblyExist(
        const wchar_t*  directoryPath, 
        const wchar_t*  assemblyName, 
        const wchar_t*  extension);

    bool ProbeForFileInPackages(
        wchar_t* pwszPackagename, 
        wchar_t* pwszFilename,
        wchar_t* pwszFilepathFound,
        size_t cchFilepathfound
        );

    wchar_t* GetPackageAssemblies();

    wchar_t* ParsePackageManifest(const wchar_t* pwszManifestFilePath);
    wchar_t* ParsePackageManifestFromDirectory(wchar_t* pwszDirectoryPath);

    bool Load_OverrideRules();
    bool Lookup_PackageOverrideRule(
       LPWSTR pwszPackageOverrideRuleSourceLookup, 
       LPWSTR pwszPackageTargetOverride, 
       size_t cchPackageTargetOverride);

    bool NuGetCmdLine_FindEXE();
    bool TryDownloadPackage(LPWSTR pwszPackageName, LPWSTR pwszPackageVersion, LPWSTR g_wszKitePackagesPathDownload);
};

