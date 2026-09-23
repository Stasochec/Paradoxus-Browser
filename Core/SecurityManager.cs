using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ParadoxusBrowser.Core
{
    /// <summary>
    /// Handles cryptography (Windows DPAPI), URL security validation,
    /// and ephemeral storage lifecycle for private browsing sessions.
    /// </summary>
    public static class SecurityManager
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ParadoxusBrowser_SecuritySalt_2026");

        /// <summary>
        /// Encrypts sensitive string data using Windows DPAPI (Current User scope).
        /// </summary>
        public static string EncryptSecret(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] cipherBytes = ProtectedData.Protect(
                    plainBytes,
                    Entropy,
                    DataProtectionScope.CurrentUser
                );
                return Convert.ToBase64String(cipherBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DPAPI Encryption Error: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Decrypts DPAPI-encrypted base64 string for the current Windows user.
        /// </summary>
        public static string DecryptSecret(string cipherBase64)
        {
            if (string.IsNullOrEmpty(cipherBase64))
                return string.Empty;

            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherBase64);
                byte[] plainBytes = ProtectedData.Unprotect(
                    cipherBytes,
                    Entropy,
                    DataProtectionScope.CurrentUser
                );
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DPAPI Decryption Error: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Validates whether a scheme is safe to navigate to directly from Omnibox.
        /// Rejects dangerous schemes like javascript:, vbscript:, file: executables, etc.
        /// </summary>
        public static bool IsSafeNavigationScheme(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            string trimmed = url.Trim().ToLowerInvariant();

            if (trimmed.StartsWith("javascript:") ||
                trimmed.StartsWith("data:") ||
                trimmed.StartsWith("vbscript:") ||
                trimmed.StartsWith("cmd:") ||
                trimmed.StartsWith("powershell:"))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Creates a unique ephemeral directory for an InPrivate/Incognito session.
        /// </summary>
        public static string CreateEphemeralUserDataFolder()
        {
            string tempBase = Path.Combine(Path.GetTempPath(), "ParadoxusBrowser_InPrivate");
            string sessionFolder = Path.Combine(tempBase, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sessionFolder);
            return sessionFolder;
        }

        /// <summary>
        /// Safely deletes an ephemeral private session folder upon tab/browser closure.
        /// </summary>
        public static void CleanupEphemeralFolder(string? folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                return;

            try
            {
                Directory.Delete(folderPath, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cleaning up ephemeral folder: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns the standard persistent user data folder for regular browsing profiles.
        /// </summary>
        public static string GetDefaultUserDataFolder()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string browserDir = Path.Combine(appData, "ParadoxusBrowser", "UserData");
            Directory.CreateDirectory(browserDir);
            return browserDir;
        }
    }
}