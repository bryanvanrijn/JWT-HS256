namespace Shared;

// ============================================================================
//  ServiceRegistry
// ----------------------------------------------------------------------------
//  Eén centrale plek met de vaste adressen (poorten) van alle services.
//  Zo hoeft niemand URL's te raden en is er één bron van waarheid.
//
//  Poortverdeling:
//    - Gateway     : http://localhost:5000   (de centrale router/uitgever)
//    - ConsoleApp1 : http://localhost:5001
//    - ConsoleApp2 : http://localhost:5002
//
//  Elke app start zelf op zijn eigen poort (zie de Program.cs van die app).
// ============================================================================
public static class ServiceRegistry
{
    public const string GatewayUrl = "http://localhost:5000";
    public const string ConsoleApp1Url = "http://localhost:5001";
    public const string ConsoleApp2Url = "http://localhost:5002";

    /// <summary>
    /// Zet een logische bestemmingsnaam ("app1"/"app2") om naar het echte
    /// basis-URL. De Gateway gebruikt dit om te bepalen waar hij een
    /// doorgestuurd bericht moet afleveren. Niet hoofdlettergevoelig.
    /// </summary>
    public static string? ResolveDestination(string destination) =>
        destination.Trim().ToLowerInvariant() switch
        {
            "app1" or "consoleapp1" => ConsoleApp1Url,
            "app2" or "consoleapp2" => ConsoleApp2Url,
            "gateway" => GatewayUrl,
            _ => null // onbekende bestemming
        };
}
