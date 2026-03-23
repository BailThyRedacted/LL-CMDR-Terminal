using System.Threading.Tasks;

namespace EliteDataCollector.Core.Services
{
    /// <summary>
    /// Handles OAuth2 PKCE authentication with Frontier's CAPI.
    /// Manages token storage, refreshing, and retrieval.
    ///
    /// Design Decision: Separate interface for CAPI auth
    /// - Can be tested with mock CAPI responses
    /// - Can swap JWT or other auth methods later
    /// - Keeps secret handling in one place
    /// </summary>
    public interface CapiAuth
    {
        /// <summary>
        /// Initializes the auth service (e.g., loads stored tokens from secure storage).
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Gets the current access token. Returns null if no token is available.
        /// </summary>
        Task<string?> GetAccessTokenAsync();

        /// <summary>
        /// Refreshes the access token using the stored refresh token.
        /// Throws if refresh fails or no refresh token is available.
        /// </summary>
        Task RefreshTokenAsync();

        /// <summary>
        /// Starts the OAuth2 PKCE flow (opens browser, waits for callback).
        /// Stores the resulting tokens securely.
        /// </summary>
        Task<bool> AuthenticateAsync();

        /// <summary>
        /// Returns true if we have at least a refresh token (can authenticate).
        /// </summary>
        bool HasStoredCredentials();
    }
}
