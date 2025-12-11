

---

# **Technical Architecture Conventions – PO Companion**

Dit document bevat alle bindende technische architectuur- en implementatieregels voor de PO Companion.
Alle toekomstige code, refactorings, packages, AI-generaties en feature-implementaties moeten hiermee consistent zijn.

---

## **1. Overall Architecture Goal**

De applicatie bestaat uit drie gescheiden lagen:

1. **Backend**
   ASP.NET Core Web API + SignalR, database, integraties, domeinservices.

2. **Frontend**
   Blazor WebAssembly, communiceert uitsluitend via de Web API en SignalR.

3. **Shell**
   MAUI desktop-applicatie die de Blazor-frontend host en de backend start.

De scheiding blijft altijd intact, ongeacht de hostingvorm.

---

## **2. Hosting & Runtime Model**

### **2.1 Eerste versies**

* De gehele applicatie draait als **single executable**.
* Backend wordt als ASP.NET host **in-process** gestart door de MAUI shell.
* Frontend draait in een MAUI WebView en communiceert via `localhost` HTTP + SignalR.

### **2.2 Toekomst**

* Backend moet zonder codewijzigingen kunnen hosten als:

  * Los proces
  * Windows service
  * Container
* Communicatie blijft altijd via dezelfde API’s en SignalR.

---

## **3. Backend Requirements**

### **3.1 Technologie**

* ASP.NET Core Web API + SignalR.
* Target runtime: .NET 10.
* Configuratie via standaard `.json`-config.

### **3.2 Database**

* Eerste versies gebruiken **SQL Express / LocalDB** met EF Core.
* Migraties zijn verplicht vanaf dag één.
* Database is bedoeld voor:

  * Gebruikersinstellingen
  * Lokale caches
  * Eigen metadata

**Niet** voor het opslaan van canonieke TFS-data.

### **3.3 TFS-integratie**

* TFS-communicatie is uitsluitend beschikbaar via een interface (zoals `ITfsClient`).
* Concrete implementatie beheert:

  * TFS-calls
  * Authorisatie met PAT
  * Resultaatmapping

### **3.4 TFS-mutaties**

* Elke TFS-mutatie moet representeren:

  * Eén expliciete backend-actie
  * Geen verborgen side-effects
  * Logging van type actie en resultaat
* De frontend mag nooit impliciet TFS-updates triggeren.

### **3.5 Authenticatie (PAT)**

* PAT wordt door de gebruiker geconfigureerd.
* PAT wordt versleuteld opgeslagen.
* PAT wordt nooit gelekt naar de frontend.
* De backend is verantwoordelijk voor beveiligde opslag en gebruik.

### **3.6 Communicatiepatroon**

* Frontend praat **uitsluitend** met backend via:

  * HTTP Web API
  * SignalR
* Geen directe method calls tussen frontend en backend, zelfs niet in-process.

### **3.7 Logging & Health**

* Centraal loggingmechanisme is verplicht voor:

  * TFS-mutaties
  * Backend-fouten
  * Belangrijke acties
* Een health-endpoint is aanwezig en moet bereikbaar zijn voor de shell.

---

## **4. Frontend Requirements**

### **4.1 Technologie**

* Blazor WebAssembly.
* SPA structuur, klassieke routing.
* SignalR wordt alleen gebruikt voor interne backend-feedback.

### **4.2 Navigatiestructuur**

* De UI gebruikt een **stabiele linkernavigatiebalk**.
* De navigatie is gebaseerd op “views” (Bugs, PRs, Releases, etc.).
* Features **mogen geen nieuwe sidebar-items introduceren**.
* Features breiden **bestaande views** uit.

### **4.3 Communicatie**

* Frontend haalt alle data via Web API calls.
* Real-time notificaties lopen via SignalR.
* Frontend slaat geen gevoelige data lokaal op.

---

## **5. Shell (MAUI) Requirements**

### **5.1 Taken**

* Start backend (in-process in eerste versies).
* Monitor backend via health-checks.
* Tonen van frontend via WebView.

### **5.2 Toekomstbestendigheid**

* Shell moet eenvoudig aanpasbaar zijn om:

  * De backend extern te starten
  * Met remote backend-instances te verbinden

### **5.3 Geen logica**

* De shell bevat geen domeinlogica en geen dataverwerking.

---

## **6. Projectstructuur**

De solution bevat minimaal:

* **Core**

  * Domeinmodellen, pure services, interfaces, logica.
  * Geen directe afhankelijkheid van infrastructuur of UI.

* **Api**

  * Web API controllers, SignalR hubs, database-contexten, integraties.
  * Implementaties van TFS-klanten, EF Core repositories en pipelines.

* **App (Shell)**

  * MAUI desktop-app.
  * Start backend en host WebView.

* **Tests.Unit**

  * MSTest-unit tests van domeinlogica en TFS-file-mocks.

---

## **7. Core Layer Requirements**

* Bevat alle logica die onafhankelijk moet zijn van infrastructuur.
* Geen directe toegang tot:

  * HTTP
  * EF Core
  * TFS
  * UI
* Alle logica moet zuiver en testbaar blijven.

---

## **8. Unit Testing Requirements**

### **8.1 Framework**

* MSTest is verplicht.

### **8.2 Testbare logica**

* Alle businesslogica moet in `Core` staan en volledig unit-testbaar zijn.
* Geen logica in controllers of frontend-componenten.

### **8.3 TFS Mocking**

* TFS wordt volledig gemockt via file-based fake clients.
* JSON-responses worden opgenomen via een “recording mode”.
* Unit tests gebruiken **nooit** een echte TFS-omgeving.

---

## **9. View-Architectuur (Frontend)**

* De sidebar bevat een vaste set **views**.
* Views vertegenwoordigen PO-perspectieven, niet individuele features.
* Features voegen functionaliteit binnen views toe, maar creëren geen nieuwe navigatiepunten.

---

## **10. Scalar API Testing Frontend**

* De backend moet een geïntegreerde, lokale **Scalar UI** aanbieden.
* Alleen beschikbaar op `localhost`.
* Toont en test alle Web API endpoints.
* Scalar heeft geen toegang tot PAT of frontend-credentials.
* Scalar wordt gebruikt voor handmatige verificatie, niet als vervanging van unit tests.

---

## **11. Mediator Usage**

### **11.1 Keuze**

* De applicatie gebruikt geen MediatR.
* De enige toegestane mediator is de **source-generated “Mediator” library van Milan Jovanović**.

### **11.2 Toepassing**

* Alleen gebruiken voor:

  * Commands (mutaties)
  * Queries (opvragingen)
  * Pipelines (logging, validatie)
* Niet gebruiken voor UI of navigatie.

### **11.3 Locatie**

* Commands/queries in `Core`.
* Handlers en pipelines in `Api`.

### **11.4 Testbaarheid**

* Commands en handlers moeten testbaar zijn zonder mediator-runtime of infrastructuur.

---

## **12. Third-party Packages Policy**

### **12.1 Approval required**

* Voordat een nieuwe dependency (NuGet of anders) wordt toegevoegd, is **expliciete goedkeuring** vereist.

### **12.2 Criteria**

Elke dependency wordt beoordeeld op:

* Onderhoudsstatus
* Licentie
* Security-impact
* Relevantie en focus
* Overlap met bestaande packages
* Effect op build, runtime en onderhoud

### **12.3 Default houding**

* Nieuwe dependencies worden **bij voorkeur vermeden** tenzij duidelijke meerwaarde bestaat.

---

## **13. Dependency Injection Policy**

* De applicatie maakt **uitsluitend** gebruik van het Microsoft-extensies DI-framework.
* Geen alternatieve DI-containers.
* Service-locators zijn verboden.
* Constructor-injectie is de standaard.

---

## **14. Invarianten (NOOIT breken)**

1. Frontend praat nooit direct met TFS.
2. Backend blijft strikt gescheiden van frontend en shell.
3. Core bevat alle logica en blijft infrastructuurvrij.
4. Elke TFS-mutatie is expliciet, traceerbaar en zonder verborgen bijwerkingen.
5. Views bepalen navigatie; features bepalen alleen inhoud.
6. De backend moet altijd in-process én out-of-process kunnen draaien.
7. Unit tests gebruiken nooit echte TFS-verbindingen.
8. Alleen goedgekeurde externe packages mogen worden toegevoegd.
9. Dependency Injection is altijd Microsoft DI.
10. Mediator = alleen de brongebaseerde “Mediator”-library.



* Een kortere executive versie maken voor bovenaan de repo.
* Een aparte “Copilot contract”-versie genereren zodat Copilot deze regels altijd toepast bij genereren.
