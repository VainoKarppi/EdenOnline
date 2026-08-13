using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace DynTypeNetwork;

/// <summary>
/// Provides authentication and authentication-token validation
/// for network connections.
/// </summary>
public static class Authentication
{
    public static bool ServerDropOnFail { get; set; } = true;
    public static bool? ClientAuthenticated { get; set; }
    public static bool Enabled { get; set; } = true;
    public static string? ClientAuthenticationError { get; set; }

    private static Func<string?, Task<bool>>? _serverAuthenticationFunc;
    private static Func<object[]?, Task<bool>>? _serverValidatorFunc;

    private static Func<Task<object[]?>>? _clientAuthenticationFunc;

    /// <summary>
    /// Sets the custom validation function for authentication tokens
    /// attached to network messages.
    /// </summary>
    public static void SetValidator(Func<string?, Task<bool>> validatorFunc)
    {
        _serverAuthenticationFunc = validatorFunc;
    }

    /// <summary>
    /// Validates an authentication token attached to a network message.
    /// If no validator is configured, validation succeeds by default.
    /// </summary>
    public static async Task<bool> ValidateAsync(string? token)
    {
        if (_serverAuthenticationFunc == null)
            return true;

        return await _serverAuthenticationFunc(token);
    }

    /// <summary>
    /// Sets the server-side authentication validator.
    /// The parameters are the authentication data returned by the client
    /// in response to an AuthenticationRequest.
    /// </summary>
    public static void SetServerValidator(Func<object[]?, Task<bool>> validatorFunc)
    {
        _serverValidatorFunc = validatorFunc;
    }

    /// <summary>
    /// Validates authentication data received from the client.
    /// If no validator is configured, authentication succeeds by default.
    /// </summary>
    public static async Task<(bool Success, string? Error)> ServerValidateAsync(object[]? parameters)
    {
        if (_serverValidatorFunc == null) return (true, null);

        try {
            return (await _serverValidatorFunc(parameters), null);
        } catch (Exception ex) {
            return (false, ex.Message);
        }
    }

    


    internal static bool HasClientAuthentication => _clientAuthenticationFunc != null;
    public static void SetClientAuthentication(Func<Task<object[]?>> authenticationFunc)
    {
        _clientAuthenticationFunc = authenticationFunc;
    }

    internal static async Task<object[]?> GetClientAuthenticationAsync()
    {
        if (_clientAuthenticationFunc == null) return null;

        return await _clientAuthenticationFunc();
    }
}