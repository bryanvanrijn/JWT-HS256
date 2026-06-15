using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Shared;

// ============================================================================
//  JwtConfig
// ----------------------------------------------------------------------------
//  Centrale plek voor ALLE JWT-instellingen. Alle drie de apps gebruiken
//  dezelfde waarden, want ze delen deze library. Dat is precies de truc van
//  HS256 (HMAC-SHA256): één gedeeld GEHEIM (symmetrische sleutel) dat zowel
//  gebruikt wordt om een token te ONDERTEKENEN als om het te VALIDEREN.
//
//  Belangrijk om te snappen:
//    - HS256 is symmetrisch: dezelfde sleutel tekent én controleert.
//      Iedereen die de sleutel kent kan dus geldige tokens maken. Voor een
//      sample is dat prima; in productie zou je dit geheim NOOIT hardcoden
//      maar uit een veilige store (user-secrets, Key Vault, env var) halen.
//    - "Issuer"   = wie het token uitgeeft (hier: de Gateway / een service).
//    - "Audience" = voor wie het token bedoeld is (de andere services).
// ============================================================================
public static class JwtConfig
{
    // Het gedeelde geheim. MOET minstens 32 bytes (256 bits) zijn voor HS256,
    // anders weigert de library te ondertekenen. Vandaar deze lange string.
    // !! In productie: NOOIT in code. Hier puur voor de demo. !!
    public const string Secret = "dit-is-een-supergeheime-demo-sleutel-van-minstens-32-bytes!";

    // De uitgever en de doelgroep van de tokens. We houden ze gelijk over alle
    // services, zodat elke service tokens van elke andere service vertrouwt.
    public const string Issuer = "jwt-gateway-sample";
    public const string Audience = "jwt-gateway-sample-clients";

    // De ondertekeningssleutel als object dat de token-bibliotheek begrijpt.
    // We zetten de geheime string om naar bytes (UTF8) en verpakken die in een
    // SymmetricSecurityKey (symmetrisch = HS256).
    public static SymmetricSecurityKey SigningKey =>
        new(Encoding.UTF8.GetBytes(Secret));

    // ------------------------------------------------------------------------
    //  TokenValidationParameters
    // ------------------------------------------------------------------------
    //  Deze instellingen gebruiken de ONTVANGENDE apps om een binnenkomend
    //  token te controleren. Ze bepalen WAT er gevalideerd wordt:
    //    - Handtekening klopt? (met onze gedeelde sleutel)
    //    - Issuer en Audience kloppen?
    //    - Token nog niet verlopen?
    // ------------------------------------------------------------------------
    public static TokenValidationParameters ValidationParameters => new()
    {
        // Controleer de digitale handtekening met onze gedeelde HS256-sleutel.
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = SigningKey,

        // Controleer dat het token van de verwachte uitgever komt.
        ValidateIssuer = true,
        ValidIssuer = Issuer,

        // Controleer dat het token voor ons (deze doelgroep) bedoeld is.
        ValidateAudience = true,
        ValidAudience = Audience,

        // Controleer de verlooptijd. ClockSkew op nul zodat "verlopen" ook
        // echt direct verlopen betekent (standaard staat er 5 min speling op).
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
}
