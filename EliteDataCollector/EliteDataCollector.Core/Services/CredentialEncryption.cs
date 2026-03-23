using System;
using System.Security.Cryptography;
using System.Text;

namespace EliteDataCollector.Core.Services
{
    /// <summary>
    /// Utility for encrypting and decrypting sensitive credentials (INARA API key).
    ///
    /// Teaching: Data Protection API (DPAPI) pattern
    /// - Uses Windows DPAPI for transparent encryption
    /// - Credentials encrypted at rest in settings.json
    /// - Only currentuser can decrypt (tied to Windows login)
    /// - No external key management needed (DPAPI handles it)
    ///
    /// Security Note: DPAPI is Windows-only and user-specific.
    /// If user logs into different computer, credentials cannot be decrypted.
    /// This is intentional for security.
    /// </summary>
    public static class CredentialEncryption
    {
        /// <summary>
        /// Encrypts a plaintext credential using Windows DPAPI.
        ///
        /// Teaching: DPAPI takes plaintext, returns base64-encoded ciphertext
        /// - DPAPI handles all key derivation internally
        /// - Uses Windows user's cryptographic keys
        /// - Safe to store in files (encrypted form)
        /// </summary>
        public static string Encrypt(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext))
                return string.Empty;

            try
            {
                // Convert plaintext to bytes
                byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

                // Encrypt using DPAPI (DataProtectionScope.CurrentUser = specific to logged-in user)
                byte[] encryptedBytes = ProtectedData.Protect(
                    plaintextBytes,
                    null,  // No additional entropy
                    DataProtectionScope.CurrentUser);

                // Convert encrypted bytes to base64 for storage in JSON
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to encrypt credential", ex);
            }
        }

        /// <summary>
        /// Decrypts a credential encrypted with Encrypt().
        ///
        /// Teaching: DPAPI reverses the encryption
        /// - Takes base64-encoded ciphertext
        /// - Returns plaintext if user/machine matches original
        /// - Throws if decryption fails (wrong user or tampered data)
        /// </summary>
        public static string Decrypt(string ciphertext)
        {
            if (string.IsNullOrEmpty(ciphertext))
                return string.Empty;

            try
            {
                // Convert base64 ciphertext back to bytes
                byte[] encryptedBytes = Convert.FromBase64String(ciphertext);

                // Decrypt using DPAPI
                byte[] plaintextBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    null,  // Same entropy as encryption
                    DataProtectionScope.CurrentUser);

                // Convert bytes to plaintext
                return Encoding.UTF8.GetString(plaintextBytes);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to decrypt credential (may be corrupted or encrypted by different user)", ex);
            }
        }
    }
}
