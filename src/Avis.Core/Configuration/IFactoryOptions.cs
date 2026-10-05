namespace Avis.Configuration;

public class RetryOptions
{
    public int MaxAttempts { get; set; } = 3;
    public double BackoffBaseSeconds { get; set; } = 1.5;
}

public class IFactoryOptions
{
    public const string SectionName = "IFactory";

    public string BaseUrl { get; set; } = "";
    public int TokenRefreshMarginSeconds { get; set; } = 60;
    public string OperatorOverrideHeader { get; set; } = "x-operator-override";
    public double RequestTimeoutSeconds { get; set; } = 10.0;
    public RetryOptions Retry { get; set; } = new();
    public List<string> SkipWipStatus { get; set; } = new() { "Completed", "Shipped", "Archived" };

    /// <summary>Populated from the IFACTORY_USERNAME env var at startup, never bound from appsettings.json.</summary>
    public string Username { get; set; } = "";

    /// <summary>Populated from the IFACTORY_PASSWORD env var at startup, never bound from appsettings.json.</summary>
    public string Password { get; set; } = "";
}
