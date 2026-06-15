namespace Shared;

// ============================================================================
//  Message
// ----------------------------------------------------------------------------
//  Dit is het "berichtcontract": de vorm van de data die over de lijn gaat.
//  Alle drie de applicaties (Gateway, ConsoleApp1, ConsoleApp2) gebruiken
//  EXACT dezelfde class, want hij staat in de gedeelde 'Shared' library.
//  Zo weet de ontvanger altijd hoe hij de JSON moet uitpakken.
//
//  We gebruiken een 'record' i.p.v. een gewone class:
//    - records zijn bedoeld voor onveranderlijke data (immutable),
//    - ze worden netjes als JSON geserialiseerd door System.Text.Json.
// ============================================================================
public record Message
{
    // Unieke id van het bericht. Handig om in de logs te kunnen volgen
    // welk bericht waar langskomt (correlatie over de drie apps heen).
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];

    // De daadwerkelijke inhoud van het bericht (de "payload").
    public string Text { get; init; } = string.Empty;

    // Wie het bericht oorspronkelijk heeft verstuurd, bijv. "Gateway" of "ConsoleApp1".
    public string From { get; init; } = string.Empty;

    // Wanneer het bericht is aangemaakt. UTC zodat tijdzones geen rol spelen.
    public DateTimeOffset SentAt { get; init; } = DateTimeOffset.UtcNow;
}

// ============================================================================
//  ForwardRequest
// ----------------------------------------------------------------------------
//  Wordt door ConsoleApp1 naar de Gateway gestuurd wanneer App1 een bericht
//  WEER WIL DOORSTUREN, maar dit moet via de Gateway lopen.
//  Het request bevat:
//    - Destination: de logische naam van de eindbestemming ("app2"),
//    - Message:     het bericht dat doorgestuurd moet worden.
//  De Gateway zoekt op basis van Destination het echte adres op en levert af.
// ============================================================================
public record ForwardRequest
{
    public string Destination { get; init; } = string.Empty;
    public Message Message { get; init; } = new();
}
