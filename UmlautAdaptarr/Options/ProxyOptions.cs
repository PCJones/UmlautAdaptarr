namespace UmlautAdaptarr.Options;

/// <summary>
///     Represents options for proxy configuration.
/// </summary>
public class ProxyOptions
{
    /// <summary>
    ///     Gets or sets a value indicating whether to use a proxy.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Gets or sets the address of the proxy.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    ///     Gets or sets the username for proxy authentication.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    ///     Gets or sets the password for proxy authentication.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    ///     Bypass Local Ip Addresses , Proxy will ignore local Ip Addresses
    /// </summary>
    public bool BypassOnLocal { get; set; }
}