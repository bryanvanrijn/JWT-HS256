using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Shared;

// ============================================================================
//  GATEWAY  (http://localhost:5000)
// ----------------------------------------------------------------------------
//  De Gateway is de spil van het geheel. Hij doet twee dingen:
//
//   1) ZELF berichten versturen:
//      Druk je in dit console op ENTER, dan maakt de Gateway een testbericht
//      aan, mint een JWT-token en stuurt het bericht naar ConsoleApp1.
//
//   2) Berichten DOORROUTEN (de "gateway"-functie):
//      ConsoleApp1 wil zijn bericht naar ConsoleApp2 sturen, maar NIET
//      rechtstreeks — het moet VIA de Gateway. Daarom biedt de Gateway een
//      endpoint POST /forward aan. App1 stuurt zijn bericht daar naartoe; de
//      Gateway controleert het JWT-token, bepaalt de eindbestemming en levert
//      het bericht (met een NIEUW token) af bij ConsoleApp2.
//
//  Op elke "hop" (sprong) tussen services wordt JWT-authenticatie gebruikt.
// ============================================================================

var builder = WebApplication.CreateBuilder(args);

// --- 1. Vaste poort instellen --------------------------------------------
// We binden de Gateway expliciet aan poort 5000 (uit de ServiceRegistry),
// zodat de andere apps weten waar ze hem kunnen vinden.
builder.WebHost.UseUrls(ServiceRegistry.GatewayUrl);

// --- 2. JWT-authenticatie configureren -----------------------------------
// Hiermee leert de Gateway om binnenkomende "Authorization: Bearer <token>"
// headers te controleren. De validatieregels komen uit de gedeelde JwtConfig
// (zelfde geheim/issuer/audience voor alle apps → HS256).
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = JwtConfig.ValidationParameters;
    });
builder.Services.AddAuthorization();

// Eén herbruikbare HttpClient om uitgaande requests te doen (naar App1/App2).
builder.Services.AddHttpClient();

var app = builder.Build();

// Schakel de authenticatie- en autorisatiemiddleware in. Volgorde is belangrijk:
// eerst authenticatie (wie ben je?), dan autorisatie (mag je dit?).
app.UseAuthentication();
app.UseAuthorization();

// ---------------------------------------------------------------------------
//  ENDPOINT:  POST /forward
//  Dit endpoint wordt aangeroepen door ConsoleApp1. Het is afgeschermd met
//  .RequireAuthorization(), dus zonder geldig JWT-token kom je er niet in.
// ---------------------------------------------------------------------------
app.MapPost("/forward", async (ForwardRequest request, IHttpClientFactory httpFactory) =>
{
    Console.WriteLine();
    Console.WriteLine($"[GATEWAY] Doorstuurverzoek ontvangen van '{request.Message.From}'.");
    Console.WriteLine($"[GATEWAY]   Bericht-id : {request.Message.Id}");
    Console.WriteLine($"[GATEWAY]   Inhoud     : \"{request.Message.Text}\"");
    Console.WriteLine($"[GATEWAY]   Bestemming : {request.Destination}");

    // Zoek het echte adres van de bestemming op (bijv. "app2" -> :5002).
    var targetUrl = ServiceRegistry.ResolveDestination(request.Destination);
    if (targetUrl is null)
    {
        Console.WriteLine($"[GATEWAY]   !! Onbekende bestemming, verzoek geweigerd.");
        return Results.BadRequest($"Onbekende bestemming: {request.Destination}");
    }

    // De Gateway tekent het bericht door met een NIEUW token. De volgende hop
    // (App2) vertrouwt dus de Gateway, niet rechtstreeks App1. Zo fungeert de
    // Gateway echt als tussenpersoon.
    await SendToServiceAsync(httpFactory, targetUrl, request.Message, subject: "Gateway");

    Console.WriteLine($"[GATEWAY]   --> Doorgestuurd naar {targetUrl}/receive");
    Console.Write("> "); // herstel de prompt voor de console-invoer
    return Results.Ok(new { status = "forwarded", to = targetUrl });
})
.RequireAuthorization();

// --- 3. Webserver starten (niet-blokkerend) ------------------------------
// StartAsync start Kestrel op de achtergrond. Daarna kunnen we gewoon door
// naar de console-lus hieronder; de webserver blijft ondertussen draaien.
await app.StartAsync();

PrintBanner();

// Eén HttpClient ophalen voor onze eigen uitgaande "druk op enter"-berichten.
var clientFactory = app.Services.GetRequiredService<IHttpClientFactory>();

// --- 4. Console-lus: ENTER = testbericht naar ConsoleApp1 ----------------
var teller = 0;
while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine();

    // Geen interactieve console (stdin is gesloten, bijv. als achtergrondproces):
    // niet in een tight loop blijven hangen, maar rustig blijven draaien zodat
    // de webserver (en het /forward-endpoint) gewoon beschikbaar blijft.
    if (input is null)
    {
        await Task.Delay(500);
        continue;
    }

    // Typ "exit" om af te sluiten; elke andere Enter stuurt een testbericht.
    if (string.Equals(input.Trim(), "exit", StringComparison.OrdinalIgnoreCase))
        break;

    teller++;
    var bericht = new Message
    {
        Text = $"Testbericht #{teller} vanuit de Gateway",
        From = "Gateway"
    };

    Console.WriteLine($"[GATEWAY] Verstuur bericht {bericht.Id} naar ConsoleApp1 ...");
    try
    {
        // Mint een token met subject "Gateway" en stuur het bericht naar App1.
        await SendToServiceAsync(clientFactory, ServiceRegistry.ConsoleApp1Url, bericht, subject: "Gateway");
        Console.WriteLine($"[GATEWAY]   --> Afgeleverd bij ConsoleApp1 (/receive).");
    }
    catch (HttpRequestException ex)
    {
        // Meestal: ConsoleApp1 draait nog niet. Geef een duidelijke hint.
        Console.WriteLine($"[GATEWAY]   !! Kon ConsoleApp1 niet bereiken: {ex.Message}");
        Console.WriteLine($"[GATEWAY]      Draait ConsoleApp1 al op {ServiceRegistry.ConsoleApp1Url}?");
    }
}

await app.StopAsync();

// ===========================================================================
//  Lokale helperfunctie: stuur een bericht naar /receive van een service,
//  voorzien van een vers JWT-token in de Authorization-header.
// ===========================================================================
static async Task SendToServiceAsync(IHttpClientFactory factory, string baseUrl, Message message, string subject)
{
    var client = factory.CreateClient();

    // 1) Mint een JWT-token (ondertekend met HS256 + gedeeld geheim).
    var token = TokenFactory.Create(subject);

    // 2) Hang het token als "Bearer"-token in de Authorization-header.
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    // 3) POST het bericht als JSON naar het /receive-endpoint van de service.
    var response = await client.PostAsJsonAsync($"{baseUrl}/receive", message);
    response.EnsureSuccessStatusCode(); // gooit een exception bij een foutcode
}

// Kleine helper die een startscherm toont.
static void PrintBanner()
{
    Console.WriteLine("==================================================================");
    Console.WriteLine("  GATEWAY draait op " + ServiceRegistry.GatewayUrl);
    Console.WriteLine("------------------------------------------------------------------");
    Console.WriteLine("  ENTER  -> stuur een testbericht naar ConsoleApp1 (met JWT)");
    Console.WriteLine("  exit   -> afsluiten");
    Console.WriteLine("  /forward endpoint staat klaar om App1 -> App2 door te routen.");
    Console.WriteLine("==================================================================");
}
