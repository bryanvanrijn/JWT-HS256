using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace Shared;

// ============================================================================
//  TokenFactory
// ----------------------------------------------------------------------------
//  Helper die een JWT-token AANMAAKT (ondertekent). Elke service die een
//  ander service aanroept, mint eerst zo'n token en stopt het in de
//  HTTP-header "Authorization: Bearer <token>".
//
//  Een JWT bestaat uit drie delen, gescheiden door punten:
//      header.payload.handtekening
//    - header  : welk algoritme (hier HS256).
//    - payload : de "claims" (informatie over de afzender + geldigheid).
//    - handtekening: HMAC-SHA256 over header+payload met het gedeelde geheim.
//  De ontvanger herberekent die handtekening en weet zo dat het token niet
//  is geknoeid en van iemand komt die het geheim kent.
// ============================================================================
public static class TokenFactory
{
    /// <summary>
    /// Maakt een ondertekend JWT-token aan voor de opgegeven afzender.
    /// </summary>
    /// <param name="subject">
    /// De naam van de service die het token uitgeeft, bijv. "Gateway".
    /// Komt in de standaard "sub" (subject) claim terecht.
    /// </param>
    /// <param name="lifetime">
    /// Hoe lang het token geldig is. Standaard 5 minuten — kort houden is
    /// veiliger, want een gelekt token verloopt dan snel.
    /// </param>
    public static string Create(string subject, TimeSpan? lifetime = null)
    {
        // De "credentials" beschrijven HOE we ondertekenen: met onze gedeelde
        // sleutel én het HS256-algoritme. Dit is het hart van HS256.
        var credentials = new SigningCredentials(
            JwtConfig.SigningKey,
            SecurityAlgorithms.HmacSha256);

        // De claims = de inhoud (payload) van het token. Hier zetten we wie de
        // afzender is en een unieke id voor dit specifieke token (jti).
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, subject),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        // Stel het token samen: issuer, audience, claims, geldigheidsduur en
        // de ondertekeningsgegevens. NotBefore = nu, Expires = nu + lifetime.
        var token = new JwtSecurityToken(
            issuer: JwtConfig.Issuer,
            audience: JwtConfig.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(5)),
            signingCredentials: credentials);

        // Serialiseer het token naar de compacte "xxxxx.yyyyy.zzzzz"-string
        // die je daadwerkelijk over HTTP verstuurt.
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
