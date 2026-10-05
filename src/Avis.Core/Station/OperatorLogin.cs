namespace Avis.Station;

/// <summary>
/// Builds the "Domain\employeeId" operator login string iFactory's
/// x-operator-override header expects (Software Reference Guide's own example:
/// "Operator UserLogin (Jabil\'NT') to be recorded against the record posted in
/// iFactory") from a station's configured domain and a scanned employee ID.
/// </summary>
public static class OperatorLogin
{
    public static string Build(string domain, string employeeId) => $"{domain}\\{employeeId}";
}
