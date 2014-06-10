#if K10
namespace System.Security.Cryptography
{
    public static class ProtectedData
    {
        public static byte[] Protect(byte[] userData, byte[] optionalEntropy, DataProtectionScope scope)
        {
            return userData;
        }
        
        public static byte[] Unprotect(byte[] encryptedData, byte[] optionalEntropy, DataProtectionScope scope)
        {
            return encryptedData;
        }
    }

    public enum DataProtectionScope
    {
        CurrentUser,
        LocalMachine
    }
}
#endif