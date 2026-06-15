# JWT Gateway Sample (HS256)

Een voorbeeldproject met **3 applicaties** die berichten naar elkaar sturen,
waarbij elke "hop" beveiligd is met een **JWT-token (HS256)**.

Hoewel het console-applicaties heten, zijn het kleine **ASP.NET Core web-apps**:
ze draaien een HTTP-server om berichten te *ontvangen* én een console-lus om
op **ENTER** een bericht te *versturen*. Dat is de eenvoudigste manier om beide
te combineren.

## De drie projecten

| Project       | Poort | Rol |
|---------------|-------|-----|
| **Gateway**   | 5000  | Centrale router + token-uitgever. Stuurt op ENTER een testbericht naar App1, en heeft `/forward` om App1 → App2 door te routen. |
| **ConsoleApp1** | 5001 | Ontvangt op `/receive`. Stuurt op ENTER het laatst ontvangen bericht **via de Gateway** door naar App2. |
| **ConsoleApp2** | 5002 | Eindbestemming. Ontvangt op `/receive` en print het bericht. |
| **Shared**    | –     | Gedeelde library: berichtmodel, JWT-config (HS256-geheim) en `TokenFactory`. |

## Hoe JWT / HS256 hier werkt

- Alle apps delen **één geheim** (`JwtConfig.Secret`). Bij HS256 wordt datzelfde
  geheim gebruikt om een token te **ondertekenen** én te **valideren** (symmetrisch).
- Een service die een andere aanroept, **mint** een token (`TokenFactory.Create`)
  en zet het in de header `Authorization: Bearer <token>`.
- De ontvangende app valideert handtekening, issuer, audience en verlooptijd
  (`JwtConfig.ValidationParameters`). Elk endpoint is afgeschermd met
  `.RequireAuthorization()`, dus zonder geldig token volgt **401 Unauthorized**.

> ⚠️ Het geheim staat hier hardcoded puur voor de demo. In productie hoort dit
> in user-secrets / Key Vault / een environment variable.

## De berichtenstroom

```
   [ Gateway ]  --(ENTER, +JWT)-->  [ ConsoleApp1 ]
                                          |
                                   (ENTER, +JWT)
                                          v
   [ Gateway /forward ]  --(+nieuw JWT)-->  [ ConsoleApp2 ]
```

1. **Gateway → App1**: druk ENTER in de Gateway → testbericht naar App1.
2. **App1 → Gateway → App2**: druk ENTER in App1 → App1 stuurt het bericht naar
   de Gateway (`/forward`, bestemming `app2`); de Gateway levert het met een
   **nieuw** token af bij App2.

## Uitvoeren

Open **drie** terminals. Start ze het handigst in deze volgorde:

```bash
# Terminal 1
dotnet run --project ConsoleApp2

# Terminal 2
dotnet run --project ConsoleApp1

# Terminal 3
dotnet run --project Gateway
```

Daarna:

1. Druk **ENTER** in de **Gateway** → App1 toont "Bericht ONTVANGEN".
2. Druk **ENTER** in **ConsoleApp1** → App2 toont "Bericht ONTVANGEN via de Gateway".

Typ `exit` in de Gateway of App1 om af te sluiten; App2 stop je met Ctrl+C.

## Bouwen

```bash
dotnet build JwtGatewaySample.slnx
```
