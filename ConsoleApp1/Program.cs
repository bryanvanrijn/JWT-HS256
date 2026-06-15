using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Shared;

// ============================================================================
//  CONSOLEAPP1  (http://localhost:5001)
// ----------------------------------------------------------------------------
//  ConsoleApp1 zit in het midden van de keten:
//
//   1) Hij ONTVANGT berichten van de Gateway op het endpoint POST /receive.
//      Dat endpoint is afgeschermd met JWT: zonder geldig token geen toegang.
//      Bij ontvangst print App1 dat hij het bericht binnen heeft.
//
//   2) Druk je in dit console op ENTER, dan stuurt App1 het LAATST ontvangen
//      bericht WEER door — maar niet rechtstreeks naar ConsoleApp2. In plaats
//      daarvan stuurt App1 het naar de Gateway (POST /forward) met de
//      bestemming "app2". De Gateway levert het vervolgens af bij App2.
//      Zo loopt al het verkeer netjes via de Gateway.
// ============================================================================

var builder = WebApplication.CreateBuilder(args);

// Vaste poort 5001 voor ConsoleApp1.
builder.WebHost.UseUrls(ServiceRegistry.ConsoleApp1Url);

// JWT-authenticatie inschakelen, met dezelfde gedeelde validatieregels (HS256).
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = JwtConfig.ValidationParameters;
    });
builder.Services.AddAuthorization();
builder.Services.AddHttpClient();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

// ---------------------------------------------------------------------------
//  Opslag voor het laatst ontvangen bericht.
//  Het /receive-endpoint (draait op een webserver-thread) SCHRIJFT hierin,
//  de console-lus (andere thread) LEEST eruit. Daarom een lock-object om
//  race conditions te voorkomen.
// ---------------------------------------------------------------------------
Message? laatstOntvangen = null;
var slot = new object();

// ---------------------------------------------------------------------------
//  ENDPOINT:  POST /receive   (JWT vereist)
//  Hier komt het bericht van de Gateway binnen.
// ---------------------------------------------------------------------------
app.MapPost("/receive", (Message message) =>
{
    // Bewaar het bericht zodat we het later op ENTER kunnen doorsturen.
    lock (slot) { laatstOntvangen = message; }

    Console.WriteLine();
    Console.WriteLine("[APP1] >>> Bericht ONTVANGEN! <<<");
    Console.WriteLine($"[APP1]   Bericht-id : {message.Id}");
    Console.WriteLine($"[APP1]   Van        : {message.From}");
    Console.WriteLine($"[APP1]   Inhoud     : \"{message.Text}\"");
    Console.WriteLine($"[APP1]   Verstuurd  : {message.SentAt:HH:mm:ss}");
    Console.WriteLine("[APP1]   Druk op ENTER om dit bericht via de Gateway naar App2 te sturen.");
    Console.Write("> ");

    return Results.Ok(new { status = "received", id = message.Id });
})
.RequireAuthorization();

// Webserver op de achtergrond starten, daarna door naar de console-lus.
await app.StartAsync();

Console.WriteLine("==================================================================");
Console.WriteLine("  CONSOLEAPP1 draait op " + ServiceRegistry.ConsoleApp1Url);
Console.WriteLine("------------------------------------------------------------------");
Console.WriteLine("  Wacht op berichten van de Gateway (/receive).");
Console.WriteLine("  ENTER -> stuur het laatst ontvangen bericht via de Gateway naar App2");
Console.WriteLine("  exit  -> afsluiten");
Console.WriteLine("==================================================================");

var clientFactory = app.Services.GetRequiredService<IHttpClientFactory>();

// --- Console-lus: ENTER = laatst ontvangen bericht via Gateway naar App2 --
while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine();

    // Geen interactieve console (stdin gesloten): rustig blijven draaien zodat
    // het /receive-endpoint bereikbaar blijft, i.p.v. een tight loop.
    if (input is null)
    {
        await Task.Delay(500);
        continue;
    }

    if (string.Equals(input.Trim(), "exit", StringComparison.OrdinalIgnoreCase))
        break;

    // Pak een kopie van het laatst ontvangen bericht (binnen de lock).
    Message? teVersturen;
    lock (slot) { teVersturen = laatstOntvangen; }

    if (teVersturen is null)
    {
        Console.WriteLine("[APP1] Nog geen bericht ontvangen om door te sturen. Start eerst de Gateway en druk daar op ENTER.");
        continue;
    }

    // We sturen het bericht door. We zetten 'From' op "ConsoleApp1" zodat de
    // Gateway in zijn logs ziet wie het doorstuurverzoek deed.
    var doorgestuurd = teVersturen with { From = "ConsoleApp1" };

    Console.WriteLine($"[APP1] Stuur bericht {doorgestuurd.Id} via de Gateway door naar App2 ...");
    try
    {
        await ForwardViaGatewayAsync(clientFactory, doorgestuurd, destination: "app2");
        Console.WriteLine("[APP1]   --> Afgegeven aan de Gateway (/forward). De Gateway levert af bij App2.");
    }
    catch (HttpRequestException ex)
    {
        Console.WriteLine($"[APP1]   !! Kon de Gateway niet bereiken: {ex.Message}");
        Console.WriteLine($"[APP1]      Draait de Gateway al op {ServiceRegistry.GatewayUrl}?");
    }
}

await app.StopAsync();

// ===========================================================================
//  Helper: stuur een bericht naar de Gateway (/forward) met JWT-token,
//  inclusief de gewenste eindbestemming.
// ===========================================================================
static async Task ForwardViaGatewayAsync(IHttpClientFactory factory, Message message, string destination)
{
    var client = factory.CreateClient();

    // ConsoleApp1 mint zelf een token (subject "ConsoleApp1"). Omdat alle apps
    // hetzelfde HS256-geheim delen, vertrouwt de Gateway dit token.
    var token = TokenFactory.Create("ConsoleApp1");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    // Verpak bericht + bestemming in een ForwardRequest en POST naar /forward.
    var payload = new ForwardRequest { Destination = destination, Message = message };
    var response = await client.PostAsJsonAsync($"{ServiceRegistry.GatewayUrl}/forward", payload);
    response.EnsureSuccessStatusCode();
}
