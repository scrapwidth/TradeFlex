using System;

namespace TradeFlex.BrokerAdapters;

/// <summary>
/// Configuration for Alpaca broker connection.
/// </summary>
public class AlpacaConfiguration
{
    /// <summary>
    /// Alpaca API Key ID.
    /// </summary>
    public string ApiKeyId { get; }

    /// <summary>
    /// Alpaca Secret Key.
    /// </summary>
    public string SecretKey { get; }

    /// <summary>
    /// Whether to use paper trading (true) or live trading (false).
    /// </summary>
    public bool UsePaperTrading { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AlpacaConfiguration"/> class.
    /// </summary>
    /// <param name="apiKeyId">The API key ID.</param>
    /// <param name="secretKey">The secret key.</param>
    /// <param name="usePaperTrading">Whether to use paper trading.</param>
    public AlpacaConfiguration(string apiKeyId, string secretKey, bool usePaperTrading = true)
    {
        if (string.IsNullOrWhiteSpace(apiKeyId))
            throw new ArgumentException("API Key ID cannot be empty", nameof(apiKeyId));
        
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new ArgumentException("Secret Key cannot be empty", nameof(secretKey));

        ApiKeyId = apiKeyId;
        SecretKey = secretKey;
        UsePaperTrading = usePaperTrading;
    }

    /// <summary>
    /// Loads configuration from environment variables.
    /// </summary>
    /// <returns>A new AlpacaConfiguration instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required environment variables are missing.</exception>
    public static AlpacaConfiguration FromEnvironment()
    {
        var apiKeyId = Environment.GetEnvironmentVariable("ALPACA_API_KEY_ID");
        var secretKey = Environment.GetEnvironmentVariable("ALPACA_SECRET_KEY");
        var usePaper = Environment.GetEnvironmentVariable("ALPACA_USE_PAPER");

        if (string.IsNullOrWhiteSpace(apiKeyId))
            throw new InvalidOperationException("Environment variable ALPACA_API_KEY_ID is not set");

        if (string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException("Environment variable ALPACA_SECRET_KEY is not set");

        // Default to paper trading if not specified
        var isPaper = string.IsNullOrWhiteSpace(usePaper) || 
                      bool.Parse(usePaper);

        return new AlpacaConfiguration(apiKeyId, secretKey, isPaper);
    }
}
