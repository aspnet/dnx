#if DNXCORE50
namespace System.Security.Cryptography
{
    public static class ProtectedData
    {
        public static byte[] Protect(byte[] userData, byte[] optionalEntropy, DataProtectionScope scope)
        {
            throw new NotImplementedException();
        }
        
        public static byte[] Unprotect(byte[] encryptedData, byte[] optionalEntropy, DataProtectionScope scope)
        {
            throw new NotImplementedException();
        }
    }

    public enum DataProtectionScope
    {
        CurrentUser,
        LocalMachine
    }
}
#endif
