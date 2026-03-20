using System.Threading.Tasks;

namespace EliteDataCollector.Core.Services
{
    /// <summary>
    /// Validates that the authenticated commander is a member of the approved squadron (Lavigny's Legion).
    ///
    /// Design Decision: Separate interface because
    /// - Squadron validation is a specific business rule
    /// - Can be tested independently
    /// - Can be replaced with different validation logic (guild system, whitelist, etc.)
    /// - Access control should be clear and auditable
    /// </summary>
    public interface ISquadronValidator
    {
        /// <summary>
        /// Initializes the validator (e.g., loads approved squadron names from config).
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Validates the current user's squadron membership.
        /// Returns true if user is authorized; false otherwise.
        /// Throws if validation fails due to error (e.g., network issues).
        /// </summary>
        Task<bool> ValidateAsync();

        /// <summary>
        /// Returns the currently validated commander's squadron name, or null if not validated.
        /// </summary>
        string? GetValidatedSquadron();
    }
}
