using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Security;

using mdToken = System.Int32;
using HCORENUM = System.IntPtr;

namespace K.Core.Metadata
{
    public enum CorOpenFlags
    {
        Read = 0,
        Write = 1,
        ReadWriteMask = 1,
        CopyMemory = 2,
        ReadOnly = 0x10
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ASSEMBLYMETADATA
    {
        public ushort usMajorVersion;      // Major Version.
        public ushort usMinorVersion;      // Minor Version.
        public ushort usBuildNumber;       // Build Number.
        public ushort usRevisionNumber;    // Revision Number.
        /// <summary>
        /// Actually this is a LPCWSTR Win API type reference, so it could be converted to <c>string</c> if required.
        /// </summary>
        public IntPtr szLocale;          // Locale.    //LPWSTR  szLocale;
        public uint cbLocale;            // [IN/OUT] Size of the buffer in wide chars/Actual size.
        public IntPtr rProcessor;        // Processor ID array.
        public uint ulProcessor;         // [IN/OUT] Size of the Processor ID array/Actual # of entries filled in.
        public IntPtr rOS;               // OSINFO array.
        public uint ulOS;                // [IN/OUT]Size of the OSINFO array/Actual # of entries filled in.
    }

    // Importing a metadata dispenser.
    //[ComImport, Guid("809C652E-7396-11D2-9771-00A0C9B4D50C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    [ComImport, Guid("809C652E-7396-11D2-9771-00A0C9B4D50C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMetaDataDispenser
    {
        void Gap1(); // DefineScope

        IMetaDataAssemblyImport OpenScope(
            [MarshalAs(UnmanagedType.LPWStr)]string szScope,
            CorOpenFlags dwOpenFlags,
            ref Guid riid);

        void Gap3(); // OpenScopeOnMemory
    }

    //[ComImport, Guid("EE62470B-E94B-424e-9B7C-2F00C9249F93"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), SuppressUnmanagedCodeSecurity]
    [ComImport, Guid("EE62470B-E94B-424e-9B7C-2F00C9249F93"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMetaDataAssemblyImport
    {
        void GetAssemblyProps(
            mdToken mda,
            IntPtr ppPublicKey,
            IntPtr pcbPublicKey,
            IntPtr pulHashAlgId,
            [In, Out, MarshalAs(UnmanagedType.LPWStr)]StringBuilder szName,
            int cchName,
            out int pchName,
            //IntPtr pMetaData,
            [Out] out ASSEMBLYMETADATA pMetaData,
            IntPtr pdwAssemblyFlags);

        void GetAssemblyRefProps(
            mdToken mdar,
            IntPtr ppbPublicKeyOrToken,
            IntPtr pcbPublicKeyOrToken,
            [In, Out, MarshalAs(UnmanagedType.LPWStr)]StringBuilder szName,
            int cchName,
            out int pchName,
            //IntPtr pMetaData,
            [Out] out ASSEMBLYMETADATA pMetaData,
            IntPtr ppbHashValue,
            IntPtr pcbHashValue,
            IntPtr pdwAssemblyRefFlags);

        void Gap3(); // GetFileProps
        void Gap4(); // GetExportedTypeProps
        void Gap5(); // GetManifestResourceProps

        void EnumAssemblyRefs(
            ref HCORENUM phEnum,
            out mdToken rAssemblyRefs,
            int cMax,
            out int pcTokens);

        void Gap7(); // EnumFiles
        void Gap8(); // EnumExportedTypes
        void Gap9(); // EnumManifestResources

        mdToken GetAssemblyFromScope(); // GetAssemblyFromScope

        void Gap11(); // FindExportedTypeByName
        void Gap12(); // FindManifestResourceByName

        void CloseEnum(HCORENUM phEnum);

        void Gap14(); // FindAssembliesByName
    }
}
