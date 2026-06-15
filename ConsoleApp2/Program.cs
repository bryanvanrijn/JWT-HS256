using Microsoft.AspNetCore.Authentication.JwtBearer;
using Shared;

// ============================================================================
//  CONSOLEAPP2  (http://localhost:5002)
// ----------------------------------------------------------------------------
//  ConsoleApp2 is de eindbestemming van de keten. Hij doet maar één ding:
//  hij luistert op POST /receive en print elk bericht dat binnenkomt.
//
//  Belangrijk: App2 krijgt het bericht NIET rechtstreeks van App1, maar van
//  de Gateway. De Gateway heeft het bericht doorgestuurd met een eigen,
//  vers JWT-token. App2 controleert dat token (HS256, gedeeld geheim) en
//  accepteert het bericht alleen als het token geldig is.
// ============================================================================

var builder = WebApplication.CreateBuilder(args);

// Vaste poort 5002 voor ConsoleApp2.
builder.WebHost.UseUrls(ServiceRegistry.ConsoleApp2Url);

// Dezelfde gedeelde JWT-validatie als de andere apps.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = JwtConfig.ValidationParameters;
    });
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

// ---------------------------------------------------------------------------
//  ENDPOINT:  POST /receive   (JWT vereist)
//  De laatste halte: hier wordt het bericht ontvangen en getoond.
// ---------------------------------------------------------------------------
app.MapPost("/receive", (Message message) =>
{
    Console.WriteLine();
    Console.WriteLine("[APP2] >>> Bericht ONTVANGEN via de Gateway! <<<");
    Console.WriteLine($"[APP2]   Bericht-id : {message.Id}");
    Console.WriteLine($"[APP2]   Van        : {message.From}");
    Console.WriteLine($"[APP2]   Inhoud     : \"{message.Text}\"");
    Console.WriteLine($"[APP2]   Verstuurd  : {message.SentAt:HH:mm:ss}");
    Console.WriteLine("[APP2]   (einde van de keten)");

    return Results.Ok(new { status = "received", id = message.Id });
})
.RequireAuthorization();

Console.WriteLine("==================================================================");
Console.WriteLine("  CONSOLEAPP2 draait op " + ServiceRegistry.ConsoleApp2Url);
Console.WriteLine("------------------------------------------------------------------");
Console.WriteLine("  Wacht op berichten die de Gateway hierheen doorstuurt (/receive).");
Console.WriteLine("==================================================================");

// App2 heeft geen console-invoer nodig; hij hoeft alleen te luisteren.
// app.Run() blokkeert en houdt de webserver draaiend tot je Ctrl+C drukt.
app.Run();
