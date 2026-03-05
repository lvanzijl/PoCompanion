# PO Companion — Gebruikershandleiding

**Doelgroep:** Product Owners  
**Taal:** Nederlands  
**Laatste bijwerking:** 2026-03-05

---

## Inhoudsopgave

1. [Wat is PO Companion?](#1-wat-is-po-companion)
2. [Eerste keer opstarten](#2-eerste-keer-opstarten)
3. [Profielen kiezen](#3-profielen-kiezen)
4. [Synchronisatie wachten (Sync Gate)](#4-synchronisatie-wachten-sync-gate)
5. [Startpagina (Home)](#5-startpagina-home)
6. [Backlog Overzicht](#6-backlog-overzicht)
7. [Health-werkruimte — Nu](#7-health-werkruimte--nu)
   - 7.1 [Validatie Triage](#71-validatie-triage)
   - 7.2 [Validatiewachtrij](#72-validatiewachtrij)
   - 7.3 [Validatie-fixsessie](#73-validatie-fixsessie)
8. [Delivery-werkruimte](#8-delivery-werkruimte)
   - 8.1 [Sprint Delivery](#81-sprint-delivery)
   - 8.2 [Sprint-activiteit](#82-sprint-activiteit)
   - 8.3 [Portfolio Delivery](#83-portfolio-delivery)
9. [Trends-werkruimte — Verleden](#9-trends-werkruimte--verleden)
   - 9.1 [Pull Request-inzichten](#91-pull-request-inzichten)
   - 9.2 [Pipeline-trend](#92-pipeline-trend)
   - 9.3 [Pipeline-inzichten](#93-pipeline-inzichten)
   - 9.4 [Delivery Trends](#94-delivery-trends)
10. [Planning-werkruimte — Toekomst](#10-planning-werkruimte--toekomst)
    - 10.1 [Planbord](#101-planbord)
    - 10.2 [Afhankelijkheidsoverzicht](#102-afhankelijkheidsoverzicht)
11. [Bugbeheer](#11-bugbeheer)
    - 11.1 [Bug-inzichten](#111-bug-inzichten)
    - 11.2 [Bug-triage](#112-bug-triage)
12. [Work Item Explorer](#12-work-item-explorer)
13. [Instellingen](#13-instellingen)
    - 13.1 [Product Owners beheren](#131-product-owners-beheren)
    - 13.2 [Producten beheren](#132-producten-beheren)
    - 13.3 [Teams beheren](#133-teams-beheren)
    - 13.4 [Werkitemstatussen configureren](#134-werkitemstatussen-configureren)
    - 13.5 [TFS-verbinding configureren](#135-tfs-verbinding-configureren)
14. [Validatieregels — uitleg](#14-validatieregels--uitleg)
15. [Sneltoetsen](#15-sneltoetsen)
16. [Veelgestelde vragen](#16-veelgestelde-vragen)

---

## 1. Wat is PO Companion?

PO Companion is een hulpmiddel voor Product Owners die werken met Azure DevOps (TFS). Het haalt je backlog op uit TFS, slaat die lokaal op in een cache, en geeft je vervolgens een overzicht van de gezondheid van je backlog, trends over tijd, en hulp bij plannen.

De applicatie is opgebouwd rond vier vragen die een Product Owner zichzelf elke dag stelt:

| Vraag | Werkruimte |
|---|---|
| Wat is er op dit moment aan de hand? | **Health** (Nu) |
| Wat hebben we opgeleverd? | **Delivery** |
| Wat is er in het verleden gebeurd? | **Trends** (Verleden) |
| Wat moet er als volgende komen? | **Planning** (Toekomst) |

> **Delivery vs. Trends:** De Delivery-werkruimte richt zich op *wat er daadwerkelijk is opgeleverd* — per sprint of geaggregeerd over producten. De Trends-werkruimte richt zich op *patronen over tijd* met een sprinttijdlijn op de X-as. Gebruik Delivery voor sprintinspectie; gebruik Trends voor structurele prestatieanalyse.

Daarnaast is er een **Backlog Overzicht** als primaire startpagina voor backlogbeslissingen: welke epics zijn klaar voor implementatie, welke hebben nog verfijning nodig, en waar zitten structurele problemen?

De hiërarchie van werkitems die de tool ondersteunt is:

```
Goal → Objective → Epic → Feature → PBI → Task
```

---

## 2. Eerste keer opstarten

### Vereisten

- De API-server (`PoTool.Api`) moet draaien. Start deze op via Visual Studio of via de opdrachtregel:
  ```
  cd PoTool.Api
  dotnet run
  ```
- Open de browser en navigeer naar `http://localhost:5291`.

### Eerste configuratiestap: TFS-verbinding

Bij het allereerste gebruik moet je de verbinding met TFS instellen. Ga via de knop **Instellingen** (tandwielpictogram in de bovenste balk) naar het tabblad **TFS-configuratie** en vul in:

- **Server-URL** — het adres van je Azure DevOps-server of TFS-instantie.
- **Persoonlijk toegangstoken (PAT)** — genereer dit in Azure DevOps onder *User Settings → Personal Access Tokens*. De vereiste scopes zijn: Work Items (Read), Code (Read), Build (Read).
- **Projectnaam** — de naam van het TFS-project waaruit werkitems worden geladen.

Sla de instellingen op. De applicatie is nu klaar om profielen aan te maken.

---

## 3. Profielen kiezen

**Pagina:** `/profiles`

Nadat de TFS-verbinding is geconfigureerd, kom je op de profielen-startpagina. Hier zie je alle ingestelde Product Owner-profielen als tegels met een avatar en naam.

### Wat kun je hier doen?

- **Profiel selecteren** — klik op een tegel om dat profiel te activeren. Je wordt daarna doorgestuurd naar de Startpagina.
- **Terugkeer-URL** — als je ergens in de applicatie een pagina bezoekt zonder actief profiel, word je automatisch teruggestuurd naar de profielen-pagina. Na selectie ga je direct naar de oorspronkelijke pagina.

Als er nog geen profielen zijn, ga je eerst naar **Instellingen → Product Owners** om een profiel aan te maken. Zie ook [§12.1 Product Owners beheren](#121-product-owners-beheren).

---

## 4. Synchronisatie wachten (Sync Gate)

**Pagina:** `/sync-gate`

Zodra je een profiel selecteert, controleert de applicatie of de lokale cache up-to-date is. Als dat niet zo is, of bij de allereerste keer, start automatisch een synchronisatie met TFS.

### Wat zie je hier?

- Een voortgangsindicator met een duidelijke statusmelding (bijv. "Bezig met synchroniseren van werkitems…").
- Zodra de synchronisatie klaar is, ga je **automatisch** door naar de Startpagina — je hoeft niets te doen.

### Wat als de sync mislukt?

- Er verschijnt een foutmelding met een **Opnieuw proberen**-knop.
- Als je twijfelt of je het juiste profiel hebt gekozen, klik dan op **Terug naar profielen**.

> **Tip:** De synchronisatie kan bij een grote backlog enkele minuten duren. Je kunt de voortgangsmeldingen volgen in het venster.

---

## 5. Startpagina (Home)

**Pagina:** `/home`

De Startpagina is het centrale vertrekpunt van de applicatie. Je ziet hier in één oogopslag hoe de backlog ervoor staat en kunt direct doorklikken naar de werkruimte van je keuze.

### Gezondheidsignalen

Bovenaan zie je drie live-signaalkaarten:

| Signaal | Betekenis | Kleuren |
|---|---|---|
| Validatieproblemen | Totaal aantal werkitems met validatiefouten | Groen (0), Blauw (1–9), Geel (10–49), Rood (50+) |
| Actieve bugs | Totaal aantal open bugs | Groen (0), Blauw (1–9), Geel (10–49), Rood (50+) |
| Totaal werkitems | Totaal aantal werkitems in de cache | Informatief |

### Productcontextfilter

Onder de signaalkaarten staat een optionele productfilter. Als je een product selecteert, nemen alle werkruimten die context over — zo zie je altijd de juiste gegevens voor dat product.

- Selecteer "Alle producten" (de chip) om de filter te wissen.
- De productfilter wordt meegegeven als `?productId=` in de URL naar elke werkruimte.

### Werkruimtekaarten

Er zijn vijf werkruimtekaarten:

1. **Backlog Overzicht** — primaire startpagina voor backlogbeslissingen.
2. **Health (Nu)** — validatieproblemen en bugs die direct aandacht vragen.
3. **Trends (Verleden)** — historische analyses van teamprestaties over tijd.
4. **Planning (Toekomst)** — capaciteitsrisico's en opzet van aankomende sprints.
5. **Delivery** — overzicht van wat er is opgeleverd per sprint of per periode.

### Snelkoppelingen

- **Validatie Triage** (primaire knop) — ga direct naar de gestructureerde validatieflow.
- **Bug Triage** — ga direct naar de bug-triage-pagina.
- **Handmatig synchroniseren** — start een handmatige cache-sync en herlaad de signalen daarna.

### Geavanceerde tools

- **Work Item Explorer** (tekstknop, lagere visuele prioriteit) — open de volledige hiërarchische explorer voor inspectie. Dit is een gereedschap voor gevorderd gebruik, niet het startpunt voor validatiewerk.

---

## 6. Backlog Overzicht

**Pagina:** `/home/backlog-overview`

Het Backlog Overzicht geeft een productgerichte kijk op de volwassenheid van de backlog. Als Product Owner zie je in één scherm welke epics klaar zijn voor implementatie, welke nog verfijning nodig hebben, en hoeveel structurele problemen er zijn.

> **Wanneer gebruik je dit?** Gebruik het Backlog Overzicht als je wilt weten wat er gepland kan worden, wat je prioriteit in verfijning moet zijn, en wat er aan structureel onderhoud gedaan moet worden.

### Wat zie je hier?

#### Productselector

Als je meerdere producten hebt, kies je hier welk product je wilt bekijken. Bij één product wordt het automatisch geselecteerd. De selector respecteert ook de productcontext die je op de Startpagina hebt ingesteld.

#### Sectie: Klaar voor implementatie

Hier staan de epics waarvan 100% van de features gereed is. Elke epic wordt getoond als een kaart met het aantal features. Klik op een epic om de Work Item Explorer te openen, gefilterd op die epic.

#### Sectie: Heeft verfijning nodig

Hier staan de epics die nog niet volledig zijn, gesorteerd op verfijningsscore (hoogste score bovenaan). Per epic zie je:

- De verfijningsscore als percentage.
- Een uitklapbaar paneel met alle features van die epic.
- Per feature: score, eigenaar-badge (PO / Team / Klaar), en een voortgangsbalk.

Klik op een feature-rij om de Work Item Explorer te openen, gefilterd op de bovenliggende epic.

**De eigenaar-badge vertelt je wie aan zet is:**

| Badge | Betekenis |
|---|---|
| **PO** | De verfijning is geblokkeerd door een Refinement Readiness-probleem — de Product Owner moet actie ondernemen (bijv. beschrijving toevoegen). |
| **Team** | De implementatiegereedheid is onvolledig — het team moet actie ondernemen (bijv. inspanning schattingen toevoegen). |
| **Klaar** | De feature is volledig verfijnd en klaar voor implementatie. |

#### Sectie: Integriteitsonderhoud

Deze sectie toont het aantal Structurele Integriteits-bevindingen voor het geselecteerde product. De chip wordt rood als er meer dan 0 bevindingen zijn.

> **Belangrijk:** Structurele Integriteitsfouten beïnvloeden de verfijningsscores **niet**. Ze zijn apart gepresenteerd als onderhoudssignaal — een kwaliteitsindicator, geen planningsblocker.

Klik op de knop **Open validatiewachtrij** om direct naar de SI-wachtrij te gaan.

#### Navigatie naar andere werkruimten

Rechtsonder staan knoppen om direct naar Health (Nu), Trends (Verleden) en Planning (Toekomst) te gaan.

---

## 7. Health-werkruimte — Nu

**Pagina:** `/home/health`

De Health-werkruimte beantwoordt de vraag: *Wat is er op dit moment aan de hand met mijn backlog?* Je ziet twee aparte secties: Verfijningssignalen en Integriteit (onderhoud).

### Verfijningssignalen

| Signaalkaart | Wat meet het? | Doorklik |
|---|---|---|
| **Refinement Readiness (RR)** | Werkitems die verfijning blokkeren (RR-regels). Oranje als > 0. | `/home/validation-queue?category=RR` |
| **Refinement Completeness (RC)** | Werkitems die verfijning nodig hebben (RC-regels). Oranje als > 0. | `/home/validation-queue?category=RC` |
| **Bugs** | Alle actieve bugs. Kleurcodering op drempelwaarden. | Bug-inzichten |

### Integriteit (onderhoud)

| Signaalkaart | Wat meet het? | Doorklik |
|---|---|---|
| **Structural Integrity (SI)** | Werkitems met structurele integriteitsfouten (SI-regels). Rood als > 0. | `/home/validation-queue?category=SI` |

> **Onthoud:** SI-bevindingen blokkeren de backlog **niet** voor planning. Ze zijn zichtbaar als onderhoudssignaal en worden apart bijgehouden.

### Backlog Health-analysepaneel

Onderaan de pagina is een ingesloten paneel met de backlog-gezondheidsanalyse voor de laatste 3 iteraties. Klik op **Backlog Overzicht** om door te gaan naar de detailpagina.

### Primaire actie: Validatie Triage

De knop **Validatie Triage** bovenaan de pagina is de aanbevolen manier om validatiewerk te starten. Die brengt je naar een gestructureerd overzicht per categorie, zodat je kunt beslissen waar je je aandacht op richt.

---

### 7.1 Validatie Triage

**Pagina:** `/home/validation-triage`

De Validatie Triage-pagina is het **startpunt voor al je validatiewerk**. Je ziet hier een overzicht per categorie met het aantal aangetaste werkitems en de meest voorkomende regels.

#### Wat zie je hier?

Vier kaarten, één per validatiecategorie:

| Kaart | Categorie | Wat staat erin? |
|---|---|---|
| **SI** | Structural Integrity | Totaal aangetaste items + top-3 regelgroepen |
| **RR** | Refinement Readiness | Totaal aangetaste items + top-3 regelgroepen |
| **RC** | Refinement Completeness | Totaal aangetaste items + top-3 regelgroepen |
| **EFF** | Missing Effort | Werkitems zonder inspanningsschatting (RC-2) |

Elke kaart heeft een knop **Open wachtrij** om door te klikken naar de detailwachtrij voor die categorie.

#### Hoe gebruik je de triage-pagina?

1. Bekijk welke categorieën items bevatten.
2. Bepaal je prioriteit: begin met de categorie die het meeste impact heeft op plannen (doorgaans RR of RC).
3. Klik op **Open wachtrij** om de concrete lijst van regels en items te zien.

---

### 7.2 Validatiewachtrij

**Pagina:** `/home/validation-queue?category={SI|RR|RC|EFF}`

De Validatiewachtrij toont per validatieregel een kaart met het aantal aangetaste werkitems, gesorteerd op impact (hoogste aantal bovenaan).

#### Wat zie je hier?

- Een samenvattingsheader met categorie-icoon, label, totaal aantal items en totaal aantal regelgroepen.
- Per regel: Regel-ID, korte omschrijving, en het aantal aangetaste items.
- Knop **Start fixsessie** — opent de Validatie-fixsessie voor die specifieke regel.

#### Hoe gebruik je de wachtrij?

1. Kies de regel met de meeste impact (bovenaan de lijst).
2. Klik op **Start fixsessie** om item voor item door de lijst te werken.
3. Regels zonder items tonen een uitgeschakelde knop.

Als er helemaal geen problemen zijn in de geselecteerde categorie, zie je een groene succesmelding.

---

### 7.3 Validatie-fixsessie

**Pagina:** `/home/validation-fix?category={...}&ruleId={...}`

De Fixsessie begeleidt je item voor item door de werkitems die een specifieke validatieregel schenden. Je ziet telkens één werkitem met alle relevante informatie, en je kunt het item afhandelen of overslaan.

#### Wat zie je per item?

- Type-chip (Epic, Feature, PBI, etc.), status-chip, en inspannings-chip (indien ingesteld).
- TFS-ID en titel.
- De exacte schendingsboodschap.
- Pad-informatie: Area Path, Iteration Path, bovenliggend item (indien aanwezig).
- Beschrijving (indien aanwezig).

#### Knoppen

| Knop | Wat doet het? |
|---|---|
| **Vorige / Volgende** | Navigeer door de actieve (niet-afgehandelde) items. |
| **Klaar voor nu** | Markeer dit item als afgehandeld voor deze sessie en ga naar het volgende. |
| **Overslaan** | Ga naar het volgende item zonder dit item te markeren. |

> **Let op:** Afgehandelde items worden alleen bijgehouden **zolang je op de pagina bent**. Als je de pagina herlaadt, begint de sessie opnieuw.

#### Einde van de sessie

Als alle items zijn afgehandeld, zie je een voltooiingsstatus. Knoppen:
- **Sessie herstarten** — verwijdert de lijst met afgehandelde items en begint opnieuw.
- **Terug naar wachtrij** — gaat terug naar de validatiewachtrij.

---

## 8. Delivery-werkruimte

**Pagina:** `/home/delivery`

De Delivery-werkruimte beantwoordt de vraag: *Wat heeft mijn team daadwerkelijk opgeleverd?* Hier kun je per sprint bekijken wat er geleverd is, of je kunt een geaggregeerd overzicht bekijken over meerdere producten heen.

> **Verschil met Trends:** De Delivery-werkruimte gaat over *wat er is opgeleverd* (resultaten, voltooide items, capaciteitsbenutting). De Trends-werkruimte gaat over *patronen over tijd* — bugtrends, PR-throughput, pipeline-betrouwbaarheid. Gebruik Delivery voor sprintinspectie en retrospectieve; gebruik Trends voor structurele patroonanalyse.

### Overzichtskaarten

| Kaart | Omschrijving |
|---|---|
| **Sprint Delivery** | Geplande vs. opgeleverde metrieken per sprint |
| **Portfolio Delivery** | Geaggregeerd leveringsoverzicht over producten voor geselecteerd sprintbereik |

---

### 8.1 Sprint Delivery

**Pagina:** `/home/delivery/sprint`

> **Voormalige naam:** Sprint Trend. De pagina is verplaatst van de Trends-werkruimte naar Delivery en hernoemd.

De Sprint Delivery-pagina toont voor één geselecteerde sprint de leveringssignalen: wat er opgeleverd is, hoe de scope is gewijzigd, en hoeveel PBI's en bugs verwerkt zijn. Gebruik de navigatiepijlen om door sprints te bladeren. Voor meersprint-trendanalyse ga je naar **Delivery Trends** in de Trends-werkruimte.

**Navigatiehiërarchie:**
```
Sprint Delivery
  → Epic-tabel (waar de inspanning terechtkwam)
      → Feature-modal (hoe het werk verdeeld was binnen een epic)
          → Activiteitsgeschiedenis (wat er precies veranderde)
```

#### Navigatiepijlen

Gebruik de pijlen links en rechts om door de sprintgeschiedenis te bladeren, één sprint tegelijk.

#### Productblokken

De pagina toont per product een inklapbaar blok. Producten zonder leveringssignaal in de geselecteerde sprint (Delivered = 0, Δ Inspanning = 0, geen werkitemactiviteit) worden automatisch verborgen.

Wanneer een productblok **ingeklapt** is, zie je een compacte samenvatting:
- **Delivered**: opgeleverde inspanning in story points.
- **Δ Effort**: netto scopewijziging in story points (positief = scope toegevoegd, negatief = scope gereduceerd).
- **PBI's**: aantal voltooide PBI's.
- **Bugs**: aangemaakt / bewerkt / gesloten.

Wanneer een productblok **uitgeklapt** is, zie je de Epic-tabel.

#### Epic-tabel

De Epic-tabel toont één rij per epic met leveringssignaal in de sprint. Epics zonder activiteit (Delivered = 0, Δ Effort = 0, geen PBI's) worden niet getoond.

| Kolom | Betekenis |
|---|---|
| **Epic** | Epic-ID (klikbaar naar TFS) en titel, met kleurmarkering. |
| **Progress** | Voltooide inspanning / totale inspanning van de epic. |
| **Delivered (pts)** | Inspanning van PBI's die in deze sprint naar Done zijn gegaan. |
| **Δ Effort (pts)** | Scopewijziging in story points: inspanning_einde_sprint − inspanning_begin_sprint. Positief = scopegroei. Negatief = scopeafname. |
| **Features ✓** | Aantal features dat in deze sprint naar Done is gegaan (state-overgang naar Done binnen de sprintperiode). |
| **PBIs ✓** | Aantal PBI's dat in deze sprint naar Done is gegaan. |
| **Actions** | 🕐 Activiteitsgeschiedenis voor de epic openen. 📋 Feature-modal openen. |

> **Verwijderd:** De kolom *Sprint Δ%* is verwijderd. Scopewijziging wordt nu uitgedrukt in absolute story points (Δ Effort).

#### Feature-modal

Klik op het lijst-icoon (📋) bij een epic om de feature-modal te openen. De modal toont de verdeling van de leveringssignalen over de onderliggende features.

| Kolom | Betekenis |
|---|---|
| **Feature** | Feature-ID (klikbaar naar TFS) en titel. |
| **Progress** | Voltooide inspanning / totale inspanning van de feature. |
| **Delivered (pts)** | Inspanning van PBI's die in deze sprint naar Done zijn gegaan. |
| **Δ Effort (pts)** | Scopewijziging in story points voor deze feature in de sprint. |
| **PBIs ✓** | Aantal PBI's dat in deze sprint naar Done is gegaan onder deze feature. |

Features zonder activiteit (Delivered = 0, Δ Effort = 0, geen PBI's) worden niet getoond. Feature-voortgangsbalken zijn dunner en iets minder verzadigd dan epic-balken om de hiërarchie visueel te verduidelijken.

#### Verouderde gegevens

Als de berekende sprintmetrieken ouder zijn dan de laatste datasync, verschijnt er een waarschuwing. Klik op **Herberekenen** om de analyse te vernieuwen.

> **Historische patronen over meerdere sprints** zijn beschikbaar in de **Delivery Trends**-pagina (`/home/trends/delivery`) in de Trends-werkruimte.

---

### 8.2 Sprint-activiteit

**Pagina:** `/home/delivery/sprint/activity/{werkitemId}`

De Sprint-activiteitspagina toont de activiteitsgeschiedenis van één werkitem (Feature of Epic) en zijn onderliggende werkitems, in de context van de Sprint Delivery-analyse. De pagina legt de nadruk op leesbaarheid: aanmaakgebeurtenissen worden samengevouwen, wijzigingen worden geclassificeerd op type, en gebeurtenissen worden gegroepeerd per werkitem.

#### Wat zie je hier?

- **Werkitem-metagegevens:** type, ID, titel, en de gebruikte sprintperiode.
- **Activiteitssamenvatting:** zes KPI-tegels afgeleid uit de geladen gebeurtenissen:
  - *Werkitems aangemaakt* — aantal unieke werkitems met een aanmaakgebeurtenis.
  - *Werkitems voltooid* — aantal werkitems waarvan de status naar Done, Closed of Resolved is gegaan.
  - *Statusovergangen* — totaal aantal Workflow-gebeurtenissen (System.State-wijzigingen).
  - *Inspanningswijzigingen* — totaal aantal Scope-gebeurtenissen (planningsvelden).
  - *Scopeverhogingen* — aantal gevallen waarbij de inspanning omhoog is gegaan.
  - *Scopeverlagingen* — aantal gevallen waarbij de inspanning omlaag is gegaan.
- **Gegroepeerde activiteitsweergave:** gebeurtenissen zijn gegroepeerd per werkitem in inklapbare secties. Elke sectie toont het werkitemtype, ID, titel en het aantal gebeurtenissen. Met de knoppen *Expand all* en *Collapse all* kun je alle secties tegelijk openen of sluiten.
- **Kolom Wijzigingstype:** een afgeleid veld dat elke gebeurtenis classificeert als:
  - `Workflow` — statuswijzigingen (hoge prioriteit, volle opmaak).
  - `Scope` — planningsvelden zoals inspanning (hoge prioriteit, volle opmaak).
  - `Creation` — aanmaakgebeurtenissen (hoge prioriteit, volle opmaak).
  - `Metadata` — titels en overige metagegevens (lage prioriteit, visueel gedimd).
  - `Structure` — area path en iteratiepad (lage prioriteit, visueel gedimd).
- **Samengevouwen aanmaakgebeurtenissen:** wanneer meerdere systeemvelden (System.Id, System.WorkItemType, System.State, enz.) op hetzelfde tijdstip voor hetzelfde werkitem verschijnen, worden ze samengevouwen tot één regel *(created)* in plaats van aparte rijen.

#### Tabelkolommen

| Kolom | Beschrijving |
|---|---|
| Timestamp (UTC) | Tijdstip van de gebeurtenis in UTC. |
| Change Type | Afgeleide classificatie: Workflow, Scope, Creation, Metadata of Structure. |
| Work Item | TFS-ID van het werkitem. |
| Source | *Selected* voor het geselecteerde rootwerkitem, *Child* voor onderliggende werkitems. |
| Field | Naam van het gewijzigde veld. |
| Old Value | Waarde vóór de wijziging. |
| New Value | Waarde na de wijziging. |

---

### 8.3 Portfolio Delivery

**Pagina:** `/home/delivery/portfolio`

Het Portfolio Delivery-overzicht biedt een geaggregeerd leveringsoverzicht over alle producten heen voor één of meerdere geselecteerde sprints. Het beantwoordt de vraag: *Wat hebben we als portfolio opgeleverd in de geselecteerde sprint(s)?*

#### Wat toont Portfolio Delivery?

Portfolio Delivery toont een **momentopname of geaggregeerde momentopname** — geen tijdgebaseerde grafieken. Alle visualisaties tonen samenstelling of verdeling.

| Sectie | Beschrijving |
|---|---|
| Sprintbereik-selector | Selecteer een team en een Van sprint / Tot sprint-bereik. Standaard wordt het huidige sprint + 4 afgelopen sprints geladen. |
| Portfolio-samenvatting | Zes KPI-tegels: Voltooide PBI's, Geleverde inspanning (story points), Gem. voortgangspercentage, Bugs aangemaakt, Bugs bewerkt, Bugs gesloten — geaggregeerd over alle producten. |
| Productbijdragegrafiek | Horizontale balkgrafiek met het aandeel van geleverde inspanning per product (%). Geordend van hoogste naar laagste bijdrage. |
| Featurebijdragegrafiek | Top-bijdragende features op basis van geleverde inspanning. Beperkt tot de top 10 voor leesbaarheid. De bijbehorende Epic wordt als subtitel getoond. |
| Bugverdeling per product | Tabel met Bugs aangemaakt, bewerkt, gesloten en netto (aangemaakt − gesloten) per product. Alleen zichtbaar als er bugactiviteit is in het geselecteerde bereik. |

#### Enkelvoudige versus meervoudige sprints

- **Enkelvoudige sprint geselecteerd** → momentopname van dat specifieke sprint.
- **Meerdere sprints geselecteerd** → geaggregeerde momentopname over het volledige bereik. Alle metrics zijn opgeteld (gesommeerd) over de sprints.

#### Wat toont Portfolio Delivery NIET?

Portfolio Delivery toont **geen trends**. Er zijn geen grafieken met een tijdas (sprinttijdlijn op de X-as). Tijdgebaseerde analyse is beschikbaar in de **Trends-werkruimte** via:
- **Delivery Trends** (`/home/trends/delivery`) — PBI-doorvoer, inspanningsdoorvoer, voortgangs- en bugtrends over meerdere sprints.
- **Portfolio Progress** (`/home/portfolio-progress`) — strategisch backlog-voortgangsoverzicht met stock-and-flow model.

---

## 9. Trends-werkruimte — Verleden

**Pagina:** `/home/trends`

De Trends-werkruimte beantwoordt de vraag: *Wat zijn de structurele patronen in teamprestaties over de afgelopen maanden?* Je ziet tijdgebaseerde analyses met een sprinttijdlijn op de X-as.

> **Opmerking:** Sprint Delivery (voorheen Sprint Trend) is verplaatst naar de Delivery-werkruimte. Ga naar Delivery voor sprintinspectie.

### Filters

- **Teamselector** — filter het sprintbereik op een specifiek team. Bij "Alle teams" worden de afgelopen 6 maanden getoond.
- **Van sprint / Tot sprint** — zodra je een team selecteert, verschijnen deze velden. Stel een eigen tijdvenster in voor de analyse. De sprintnaam en startmaand worden getoond.
- **Chip "Afgelopen 6 maanden"** — zichtbaar wanneer geen sprintbereik is ingesteld.

### Signaalkaarten

| Signaalkaart | Wat meet het? | Doorklik |
|---|---|---|
| **Bug Trend** | Bugpatronen over tijd | Bug-inzichten |
| **PR Trend** | Pull request-patronen | PR-inzichten |
| **Pipeline Trend** | Build- en deployment-betrouwbaarheid over meerdere sprints (trendgrafieken) | Pipeline-trend |
| **Pipeline Insights** | Sprint-specifieke pipeline stabiliteitsanalyse per product | Pipeline-inzichten |
| **Portfolio Progress** | Strategische voortgang per product over een sprintbereik | Portfolio Progress |
| **Delivery Trends** | PBI-doorvoer, inspanningsdoorvoer en bugtrend per sprint | Delivery Trends |

> **Velocity en voorspelbaarheid** zijn niet langer een aparte signaalkaart in Trends. Ze zijn ingebed als *Calibratiepaneel* in Sprint Delivery (tactische context) en als *Capaciteitsvertrouwen* in Planning (voorspellingscontext).

### Interactieve bug-trendgrafiek

Onderaan de pagina zie je een drieseriengrafiek (Totaal bugs, Opgeloste bugs, Toegevoegde bugs) voor het geselecteerde tijdvak. Klik op een staaf om door te gaan naar Bug-inzichten gefilterd op die periode.

---

### 9.1 Pull Request-inzichten

**Pagina:** `/home/pull-requests`

Een teamgericht, alleen-lezen overzicht van pull request-wrijving, gericht op de vraag: *"Welke pull requests duiden op leveringswrijving binnen het team?"*

Alle gegevens komen uitsluitend uit de lokale cache — er worden geen live Azure DevOps-aanroepen gedaan.

#### Filters

- **Teamselector** — beperkt de PR-gegevens tot alle producten die aan het geselecteerde team zijn gekoppeld.
- **Datumbereikfilter** — standaard de afgelopen 6 maanden.
- **Repository (optioneel)** — filter op één specifieke repository.

#### Wat zie je hier?

**Sectie 1 — Samenvatting**

Zes chips bovenaan de pagina tonen de teamgezondheid van PR's in één oogopslag:

| Chip | Omschrijving |
|---|---|
| Totaal PR's | Totaal aantal PR's in het geselecteerde bereik |
| Samenvoegpercentage | Percentage afgesloten PR's ten opzichte van het totaal |
| Afbreekpercentage | Percentage afgebroken PR's |
| Herwerk-percentage | Percentage samengevoegde PR's na herbewerking (proxy: meer dan één iteratie) |
| Mediane levensduur | Mediane levensduur van alle PR's in het bereik |
| P90-levensduur | 90e percentiel van de levensduur (alleen beschikbaar bij ≥ 3 PR's) |

**Sectie 2 — Top 3 problematische PR's**

Drie kaarten tonen de PR's die het meest bijdragen aan wrijving in de workflow, gerangschikt op een samengesteld score:

- Levensduur: 40 %
- Revisiecycli: 30 %
- Gewijzigde bestanden: 20 %
- Commentaren: 10 %

Klik op een kaart om de bijbehorende punt in het spreidingsdiagram te markeren.

**Sectie 3 — PR-spreidingsdiagram**

Een SVG-spreidingsdiagram (`PullRequestScatterSvg`) met:

- **X-as** — aanmaakdatum van de PR
- **Y-as** — levensduur in uren

Puntkleur:

| Kleur | Betekenis |
|---|---|
| Groen | Samengevoegd zonder herbewerking |
| Geel | Samengevoegd na herbewerking |
| Rood | Afgebroken PR |
| Grijs | Actieve (nog openstaande) PR |

Pijlers geven de repository aan (cirkel / vierkant / driehoek). Zweef boven een punt voor een tooltip met de PR-titel, auteur, levensduur, revisiecycli, bestanden en commentaren. Klik om die PR te markeren en alle andere te dimmen, en om het **PR-detailpaneel** (zie hieronder) te openen. Optionele mediaan- en P90-lijnen zijn beschikbaar.

**PR-detailpaneel (Drawer)**

Klikken op een punt in het spreidingsdiagram opent een inschuifpaneel aan de rechterkant van de pagina. Het paneel toont:

- Statusindicator (Merged clean / Merged rework / Abandoned / Active)
- PR-titel en auteur
- Repository
- Aanmaakdatum
- Levensduur
- Revisiecycli (met "rework"-label bij meer dan één revisie)
- Gewijzigde bestanden
- Commentaren

Sluit het paneel via de ×-knop of door de filters te wijzigen.

**Sectie 4 — Langst openstaande PR's**

Tabel met de top 20 langste PR's, gesorteerd op levensduur aflopend. Kolommen: PR-titel, repository, auteur, levensduur, revisiecycli, gewijzigde bestanden, commentaren, status.

**Sectie 5 — Breakdowntabel per repository**

Inklapbare tabel met workflowstatistieken per repository. Gesorteerd op PR-aantal aflopend. Kolommen: repository, PR-aantal, samenvoeging %, afbreking %, mediane levensduur, P90-levensduur, gemiddelde revisiecycli.

**Sectie 6 — Breakdowntabel per auteur**

Inklapbare tabel met workflowstatistieken per auteur (maker van de PR). Gesorteerd op PR-aantal aflopend. Kolommen: auteur, PR-aantal, samenvoeging %, afbreking %, herwerk %, mediane levensduur, gemiddelde revisiecycli.

> De herwerk %-kolom is gemarkeerd in oranje wanneer meer dan 30 % van de PR's van die auteur werd samengevoegd na herbewerking.

---

### 9.2 Pipeline-trend

**Pagina:** `/home/pipelines`

De Pipeline-trend-pagina toont de build- en deployment-gezondheid over meerdere opeenvolgende sprints als trendgrafieken. Gebruik deze pagina om structurele patronen te herkennen in betrouwbaarheid, doorlooptijd en instabiliteit. Alle grafieken tonen een sprinttijdlijn op de X-as.

#### Wat zie je hier?

- **Teamselector** — filter de sprintlijst op een specifiek team. Bij "Alle teams" worden de gegevens niet op teamniveau gefilterd.
- **Productselector** — optioneel filter op een specifiek product. Standaard worden alle producten getoond.
- **Eindsprint** — de meest recente sprint die in het bereik wordt getoond.
- **Aantal sprints** — stel in hoeveel sprints worden weergegeven.

#### Grafieken

| Grafiek | Omschrijving |
|---|---|
| **Betrouwbaarheids-trend** | Slagingspercentage van pipelines per sprint. Hogere waarden zijn beter. |
| **Time-to-Green-trend** | Mediane pipeline-doorlooptijd (uren) per sprint. Lagere waarden zijn beter. |
| **Staartrisico-trend** | P90-pipeline-doorlooptijd (uren) per sprint. Null/gat weergegeven bij minder dan 3 runs in een sprint. Lagere waarden zijn beter. |
| **Instabiliteitsrisico-trend** | Percentage pipelines met zowel successen als mislukkingen in dezelfde sprint. Lagere waarden zijn beter. |

Elke grafiek toont een **helling-badge** (Verbeterend / Stabiel / Verslechterend) op basis van het eerste en laatste datapunt in het bereik.

#### Drill-down

Onderaan de pagina bevindt zich een inklapbaar **Drill-down**-paneel met een tabel van per-pipeline details: slagingspercentage, mediane duur, P90-duur, main-branchgegevens en instabiliteitspercentage.

---

### 9.3 Pipeline-inzichten

**Pagina:** `/home/pipeline-insights`

De Pipeline-inzichten-pagina is een PO-gericht stabiliteitsoverzicht voor één geselecteerde sprint. In tegenstelling tot de Pipeline-trend-pagina (die trendgrafieken toont over meerdere sprints) richt Pipeline-inzichten zich op de huidige of geselecteerde sprint: welke pipelines zijn het meest problematisch, hoe stabiel zijn ze gedurende de sprint, en welke richting gaan ze op?

Alle gegevens zijn afkomstig uit de lokale cache — er worden geen TFS-aanroepen gedaan.

#### Filters en configuratie

- **Teamselector** — selecteer een team. De sprintlijst wordt automatisch geladen; de huidige sprint (of de meest recente afgelopen sprint) wordt automatisch geselecteerd.
- **Sprints selector** — selecteer de te analyseren sprint. Wordt gevuld zodra een team is gekozen.
- **Include partial success** (standaard aan) — wanneer ingeschakeld worden gedeeltelijk geslaagde runs (`partiallySucceeded`) meegeteld als voltooid en getoond als waarschuwingen.
- **Include canceled** (standaard uit) — wanneer ingeschakeld worden geannuleerde runs meegeteld in het totaal.
- **SLO-duratie (min)** — optioneel: stel een Service Level Objective-grens (in minuten) in. Dit tekent een horizontale SLO-lijn op alle scatter-grafieken.

#### Globale samenvatting

Bovenaan de pagina staan vier samenvattingschips, geaggregeerd over alle producten van de actieve Product Owner:

| Chip | Betekenis |
|---|---|
| **Totaal builds** | Aantal gecachte pipeline-runs in de geselecteerde sprint. |
| **Mislukkingspercentage** | Percentage mislukte runs (met absoluut aantal). |
| **Waarschuwingspercentage** | Percentage gedeeltelijk geslaagde runs (alleen zichtbaar wanneer Include partial success aan is). |
| **P90-duratie** | 90ste-percentiel van de build-duur in minuten, over alle runs. |

#### Globale top 3 probleempipelines

De drie meest problematische pipelines wereldwijd (over alle producten), gerangschikt op mislukkingspercentage (hoogste eerst). Per kaart wordt getoond:

- Pipelinenaam en productnaam.
- Mislukkingspercentage met absoluut aantal mislukte/voltooide runs.
- Delta (Δ) ten opzichte van de vorige sprint — `n/a` wanneer er geen vorige sprintgegevens zijn.

Klik op een kaart om de pagina soepel naar het betreffende productblok te scrollen.

#### Per-productblokken

De pagina toont één blok per product dat eigendom is van de actieve Product Owner, gesorteerd op productnaam. Elk blok bevat:

1. **Per-product top-3 probleempipelines** — klik op een pipelinenaam om de bijbehorende punten te markeren in de scatter-grafiek (overige punten worden gedimd). Klik opnieuw om de markering te wissen.
2. **Pipeline-stabiliteits-scatter** (TimeScatterSvg) — zie hieronder.
3. **Per-product samenvattingschips** — mislukkingspercentage, waarschuwingspercentage, slagingspercentage, mediane duur en P90-duur voor het product.
4. **Per-pipeline uitsplitsingstabel** — zie hieronder.

Wanneer er geen gecachte runs zijn voor het geselecteerde product in de gekozen sprint, wordt een lege staat weergegeven.

#### Pipeline-stabiliteits-scatter

Per product wordt een SVG-scatter-grafiek weergegeven:

| As / Element | Betekenis |
|---|---|
| **X-as** | Starttijd van de build binnen de sprint. |
| **Y-as** | Build-duur in minuten. |
| **Kleur van punt** | Groen = geslaagd, geel = gedeeltelijk geslaagd, rood = mislukt, grijs = geannuleerd. |
| **Mediane lijn** | Gestippelde blauwe lijn op de mediane duur. |
| **P90-lijn** | Gestippelde oranje lijn op de P90-duur. |
| **SLO-lijn** | Rode lijn op de ingestelde SLO-duratie (alleen wanneer een SLO is ingesteld). |

Klik op een punt om de **Build-samenvattingslade** te openen (rechts verankerd). De lade toont: buildnummer, pipelinenaam, resultaat, starttijd, eindtijd, duur, branch en een link naar Azure DevOps (wanneer beschikbaar in de cache).

#### Per-pipeline uitsplitsingstabel

Onderaan elk productblok bevindt zich een inklapbare tabel met alle pipelines van het product (niet alleen de top 3), gesorteerd op mislukkingspercentage (hoogste eerst). Kolommen:

| Kolom | Betekenis |
|---|---|
| **Pipeline** | Naam van de pipeline. |
| **Runs** | Aantal runs in de sprint. |
| **Succes%** | Percentage geslaagde runs. |
| **Mislukking%** | Percentage mislukte runs. |
| **Mediane duur** | Mediane build-duur in de sprint. |
| **P90** | 90ste-percentiel van de build-duur (alleen bij ≥ 3 runs). |
| **Δ Mislukking** | Verschil in mislukkingspercentage ten opzichte van de vorige sprint (in procentpunten). |
| **Halvesprint-trend** | Zie hieronder. |

Bij meer dan 8 pipelines wordt de tabel scrollbaar (max. 320 px).

#### Halvesprint-trend-chip

Per pipeline wordt de trend binnen de sprint bepaald door de sprint te halveren en de mislukkingspercentages van de eerste en tweede helft te vergelijken:

| Chip | Kleur | Voorwaarde |
|---|---|---|
| **Verbeterend** | Groen | Mislukkingspercentage daalde ≥ 10 procentpunten in de tweede helft. |
| **Verslechterend** | Rood | Mislukkingspercentage steeg ≥ 10 procentpunten in de tweede helft. |
| **Stabiel** | Grijs | Minder dan 10 procentpunten verschil. |
| **—** (Onvoldoende) | — | Minder dan 2 voltooide runs in een van de helften. |

De tooltip toont de exacte mislukkingspercentages van de eerste en tweede helft.

#### Lege staat en foutafhandeling

- Wanneer nog geen sprint is geselecteerd, wordt een instructie getoond die de gebruiker vraagt een team en sprint te kiezen.
- Bij netwerk- of cachefouten verschijnt een foutmelding met een **Opnieuw proberen**-knop.

---

### 9.4 Delivery Trends

**Pagina:** `/home/trends/delivery`

De Delivery Trends-pagina analyseert leveringsgedrag over meerdere sprints. Gebruik deze pagina om structurele patronen te herkennen in doorvoer, inspanning en bugactiviteit. Alle grafieken tonen een sprinttijdlijn op de X-as.

#### Wat zie je hier?

- **Teamselector** — filter de sprintlijst op een specifiek team.
- **Productselector** — optioneel filter op een specifiek product. Standaard worden totalen over alle producten getoond.
- **Eind-sprint** — de meest recente sprint die in het bereik wordt getoond.
- **Aantal sprints** — stel in hoeveel sprints worden weergegeven (minimaal 2, standaard 6).

#### Grafieken

| Grafiek | Omschrijving |
|---|---|
| **PBI-doorvoertrend** | Aantal voltooide PBI's per sprint (primaire visualisatie, volledige breedte). Hogere waarden zijn beter. |
| **Inspanningsdoorvoertrend** | Story points opgeleverd per sprint. Hogere waarden zijn beter. |
| **Voortgangstrend** | Voltooide inspanning als percentage van geplande inspanning per sprint. Hogere waarden zijn beter. |
| **Bugtrend** | Aangemaakte versus gesloten bugs per sprint. Minder aangemaakt is beter. |

Elke grafiek toont een **helling-badge** (Verbeterend / Stabiel / Verslechterend) op basis van het eerste en laatste datapunt in het bereik.

#### Drill-down

Onderaan de pagina bevindt zich een ingeklapt **Drill-down**-paneel met een tabel van per-sprintdetails: voltooide PBI's, voltooide inspanning, geplande inspanning, voltooiingspercentage, aangemaakt bugs en gesloten bugs.

---

## 10. Planning-werkruimte — Toekomst

**Pagina:** `/home/planning`

De Planning-werkruimte beantwoordt de vraag: *Wat moet er als volgende komen?* Je ziet beslissingsondersteunende signalen voor aankomend werk, gericht op capaciteitsrisico's en backlogkwaliteit. De weergave beslaat de huidige iteratie plus de volgende 3 iteraties.

### Signaalkaarten

| Signaalkaart | Wat meet het? | Kleuren |
|---|---|---|
| **Epic overschrijdt velocity** | Epics waarvan het resterende werk meer dan 3× de gemiddelde teamvelocity is. | Groen (alles goed), Geel (≤ 2 risico's), Rood (> 2 risico's) |
| **Epic met ongeldige items** | Epics met child-werkitems die validatiefouten hebben. | Informatief |
| **Epic-afhankelijkheden** | Alleen-lezen overzicht van afhankelijkheden. | Informatief |

### Capaciteitsvertrouwen

Als er voltooide sprintdata beschikbaar is, toont de Planning-werkruimte een **Capaciteitsvertrouwen**-blok met calibratiesignalen:

| Signaal | Omschrijving |
|---|---|
| **Mediaan velocity** | Typische sprintcapaciteit in story points (P50). |
| **P25–P75 band** | Volatiliteitsband — gebruik P25 voor conservatieve plannen. |
| **Mediaan voorspelbaarheid** | Verhouding voltooid/gepland over de beschikbare sprints. |
| **Veilige plancapaciteit** | P25-velocity: hoge betrouwbaarheid voor sprintplanning. |

> **Plan bij ~P25 punten als je hoge betrouwbaarheid wilt.** Gebruik de mediaan als typische output.

Het blok werkt bij als de productselectie verandert.

### Detailtabellen

**Epics die velocity overschrijden** — zichtbaar als er epics in risico zijn:

| Kolom | Omschrijving |
|---|---|
| ID | Epic-ID (klikbaar naar Sprint Delivery voor calibratiedetails) |
| Titel | Epic-naam |
| Status | Huidige status |
| Resterende inspanning | Story points die nog open staan |
| Sprints tot voltooiing | Schatting op basis van gemiddelde velocity |
| Betrouwbaarheid | Vertrouwensindicator van de schatting |

**Epics met ongeldige items** — zichtbaar als er epics problemen hebben:

| Kolom | Omschrijving |
|---|---|
| ID | Epic-ID (klikbaar naar Work Item Explorer voor die epic) |
| Titel | Epic-naam |
| Status | Huidige status |
| Aantal ongeldige items | Hoeveel child-items validatiefouten hebben |

### Planbord-sectie

Onderaan is een ingesloten planbord met een productselector. De link **Volledig release-planningsbord** opent de uitgebreide release-planningspagina.

---

### 10.1 Planbord

**Pagina:** `/home/plan-board`

Het Planbord toont epics en features georganiseerd per iteratie. Het is toegankelijk via de snelkoppeling op de Startpagina.

- Gebruik de **Productselector** om het bord te beperken tot één product.
- De chip **Alle producten** is zichtbaar wanneer vanuit de Startpagina wordt geopend zonder productfilter.

---

### 10.2 Afhankelijkheidsoverzicht

**Pagina:** `/home/dependencies`

Een alleen-lezen visueel overzicht van werkitem-afhankelijkheden tussen epics en teams.

> **Let op:** Dit is een inzichtweergave. Voor het beheren van afhankelijkheden ga je via de link naar het volledige afhankelijkheidsbeheer (`/dependency-graph`).

---

## 11. Bugbeheer

### 11.1 Bug-inzichten

**Pagina:** `/home/bugs`

Een gedetailleerd overzicht van alle actieve bugs, met filters op product, team en periode.

#### Filters

- **Product-/Teamfilter** — beperk de weergave tot bugs voor een specifiek product of team.
- **Periodfilter** — als je via de Bug Trend-grafiek op een staaf klikt, worden bugs automatisch gefilterd op die periode.
- **Alle bugs weergeven** — schakeloptie om alle bugs te tonen, ongeacht de contextfilter.

#### Buglijst

Elke bug toont: ID, titel, status, ernst, tags en overige kernattributen.

Klik op een bug om naar de **Bug-detailpagina** te gaan, waar je de ernst en triaagtags kunt bewerken.

#### Primaire actie

De knop **Bug Triage** bovenaan de pagina brengt je naar de bug-triagepagina voor het triageren en categoriseren van bugs.

---

### 11.2 Bug-triage

**Pagina:** `/bugs-triage`

De Bug Triage-pagina is een gefocust hulpmiddel voor het triageren van alle openstaande bugs.

#### Wat zie je hier?

- **Bug-boomgrid** — alle openstaande bugs in een uitklapbare boomweergave, georganiseerd per producthiërarchie.
- **Niet-getriageerd aantal** — prominent weergegeven: hoeveel bugs nog triage nodig hebben.
- **Triaagtags** — wijs tags toe aan individuele bugs, zoals "Won't Fix", "Volgende sprint", of "Uitgesteld".

Bugs worden gefilterd op de actieve profielsproducten.

---

## 12. Work Item Explorer

**Pagina:** `/workitems`

De Work Item Explorer is de hiërarchische verkenner voor alle werkitems in de producten van het actieve profiel. Dit is een **geavanceerd inspectiehulpmiddel** — het startpunt voor validatiewerk is de Validatie Triage-flow, niet de Explorer.

### Indeling

De Explorer heeft twee panelen, gescheiden door een versleepbare splitter:

- **Linker paneel: Werkitemstructuur** — hiërarchische boom/raster van alle werkitems.
- **Rechter paneel: Werkitemdetails** — volledige details van het geselecteerde werkitem.

### Werkitemstructuur (links)

- **Tekstzoekveld** — filter werkitems op titel.
- **Uitklappen/inklappen** — navigeer door de hiërarchie.
- **Multi-selectie** — klik, Shift+klik (reeks selecteren), of Ctrl+klik (individuele items selecteren).
- **Toetsenbordnavigatie** — gebruik de pijltoetsen om door de boom te navigeren.
- **Scoping op rootwerkitem** — als `rootWorkItemId` via de URL wordt meegegeven, toont de boom alleen de nakomelingen van dat item.
- **Validatiecategoriefilter** — als `validationCategory` via de URL wordt meegegeven (1=SI, 2=RR, 3=RC), worden alleen items met problemen in die categorie getoond.
- **Alle producten/teams** — als `allProducts=true` wordt meegegeven, worden werkitems van alle producten geladen.

### Validatiesamenvatting (uitklapbaar paneel)

Toont alle validatieproblemen over de geladen werkitems heen, met automatisch-correctiesugesties waar beschikbaar.

### Validatiegeschiedenis (uitklapbaar paneel)

Toont eerdere validatieruns en hun resultaten.

### Validatiefiltercheckboxes

Snelfilters voor de boom per categorie:
- **Structural Integrity** (SI)
- **Refinement Readiness** (RR)
- **Refinement Completeness** (RC)

Per filter wordt het aantal aangetaste items getoond.

### Werkitemdetails (rechts)

Bij het selecteren van een werkitem zie je:

- **Metagegevens** — type, status, toewijzing, area path, iteration path, inspanning.
- **Beschrijving** — volledige beschrijving van het werkitem.
- **Validatieproblemen** — lijst van alle schendingen voor dit item.
- **Activiteitstijdlijn** — revisiegeschiedenis via een tijdlijn (statuswijzigingen, inspanningsupdates, toewijzingswijzigingen).

---

## 13. Instellingen

**Pagina:** `/settings`

Via de **Instellingen**-knop (tandwielpictogram) in de bovenste balk ga je naar de instellingenpagina. Hier beheer je de configuratie van de applicatie.

---

### 13.1 Product Owners beheren

**Pagina:** `/settings/productowner/{profielId}`

Een Product Owner-profiel is de kern van de applicatie. Elk profiel heeft:

- **Naam** — de weergavenaam van de Product Owner.
- **Profielfoto** — kies een avatar of upload een eigen foto.
- **Goal-werkitem** — koppel het profiel aan een specifiek Goal-werkitem in TFS. Alleen werkitems onder dit Goal worden geladen.
- **Producten** — de producten die aan dit profiel zijn gekoppeld.

#### Nieuw profiel aanmaken

Ga naar **Instellingen → Product Owners → Toevoegen**. Vul de naam in, kies een profielfoto, en selecteer het Goal-werkitem.

#### Profiel bewerken

Klik op een bestaand profiel in de lijst en pas de velden aan. Klik op **Opslaan** om de wijzigingen te bewaren.

---

### 13.2 Producten beheren

**Pagina:** `/settings/products`

Producten zijn de primaire organisatie-eenheden waarop de backlog wordt gefilterd. Een product heeft:

- **Naam** — de productnaam.
- **Backlog-rootwerkitems** — een of meerdere werkitems (typisch Epics) die de root van de productbacklog vormen. Werkitems buiten deze roots worden niet meegenomen in productgebonden analyses.
- **Gekoppelde teams** — teams die aan dit product werken.
- **Eigenaar** — de Product Owner die verantwoordelijk is voor dit product.

#### Wees voorzichtig met wezen-producten

Producten zonder gekoppelde Product Owner worden als "wees" gemarkeerd. Gebruik de schakeloptie **Alleen wezen tonen** om deze snel te identificeren en te herstellen.

---

### 13.3 Teams beheren

**Pagina:** `/settings/teams`

Teams worden gebruikt voor filtering in Trends- en Planninganalyses. Een team heeft:

- **Naam** — de teamnaam.
- **Gekoppelde producten** — de producten waarbij het team betrokken is.

Gearchiveerde teams worden verborgen. Gebruik de schakeloptie **Toon gearchiveerd** om ze weer te zien. Teams kunnen gearchiveerd worden als ze niet meer actief zijn, zonder ze definitief te verwijderen.

---

### 13.4 Werkitemstatussen configureren

**Pagina:** `/settings/workitem-states`

TFS gebruikt projectspecifieke statusnamen (zoals "In behandeling", "Gereed", "Gesloten"). De applicatie werkt intern met vier canonieke levenscyclusstatussen:

| Canonieke status | Betekenis |
|---|---|
| **New** | Werk is nog niet begonnen |
| **In Progress** | Werk is actief bezig |
| **Done** | Werk is afgerond en opgeleverd |
| **Removed** | Werk is geannuleerd of bewust verwijderd |

Op de statusconfiguratiepagina stel je in welke TFS-status overeenkomt met welke canonieke status. Deze mapping is nodig voor:

- Correcte voortgangsberekeningen in Sprint Delivery.
- Structurele integriteitscontroles (SI-regels).
- Verfijningsscores in het Backlog Overzicht.

---

### 13.5 TFS-verbinding configureren

Via **Instellingen → TFS** configureer je de verbinding met Azure DevOps of TFS:

- **Server-URL** — het basisadres van de TFS-instantie.
- **Persoonlijk toegangstoken (PAT)** — beveiligde toegang tot de TFS API.
- **Projectnaam** — het TFS-project waaruit gegevens worden geladen.

Wijzigingen in de TFS-verbinding vereisen een nieuwe synchronisatie (via de **Handmatig synchroniseren**-knop op de Startpagina of via de Sync Gate).

---

## 14. Validatieregels — uitleg

De applicatie valideert werkitems automatisch op basis van drie categorieën en negen regels. Hieronder vind je een uitleg van elke regel.

### Structural Integrity (SI) — Structurele integriteit

Deze regels detecteren hiërarchieproblemen die de betrouwbaarheid van de backlog ondermijnen. Ze zijn **geen planningsblokkade** maar worden apart bijgehouden als onderhoudssignaal.

| Regel-ID | Omschrijving | Wie lost het op? |
|---|---|---|
| **SI-1** | Een bovenliggend item in de *Done*-status heeft nakomelingen die nog niet klaar zijn. | Product Owner of Scrum Master: sluit de nakomelingen af of heropent het bovenliggende item. |
| **SI-2** | Een bovenliggend item in de *Removed*-status heeft nakomelingen die niet de *Removed*-status hebben. | Product Owner: verwijder de nakomelingen of herstel het bovenliggende item. |
| **SI-3** | Een bovenliggend item in de *New*-status heeft nakomelingen die al *In Progress* of *Done* zijn. | Product Owner: zet het bovenliggende item op de juiste status. |

### Refinement Readiness (RR) — Verfijningsgereedheid

Deze regels detecteren items die de verfijningsstroom blokkeren. Ze zijn in primair de verantwoordelijkheid van de **Product Owner**.

| Regel-ID | Omschrijving | Oplossing |
|---|---|---|
| **RR-1** | Een Epic heeft een lege of te korte beschrijving. | Voeg een beschrijving toe die de strategische intent van de epic uitlegt. |
| **RR-2** | Een Feature heeft een lege of te korte beschrijving. | Voeg een beschrijving toe die het doel en de scope van de feature uitlegt. |
| **RR-3** | Een Epic heeft geen Feature-kinderen. | Maak ten minste één Feature aan onder de epic, of verwijder de epic als deze niet meer relevant is. |

### Refinement Completeness (RC) — Verfijningsvolledigheid

Deze regels detecteren items die onvolledig zijn en daarmee implementatiegereedheid blokkeren. Ze zijn gedeelde verantwoordelijkheid van **Product Owner en Team**.

| Regel-ID | Omschrijving | Oplossing |
|---|---|---|
| **RC-1** | Een PBI heeft geen beschrijving. | Voeg een acceptatiecriteria of beschrijving toe. |
| **RC-2** | Een werkitem heeft geen inspanningsschatting. | Voeg een Story Point-schatting toe (typisch tijdens refinementsessies). |
| **RC-3** | Een Feature heeft geen PBI-kinderen. | Maak ten minste één PBI aan onder de feature, of verwijder de feature als die niet meer relevant is. |

---

## 15. Sneltoetsen

De applicatie ondersteunt een reeks sneltoetsen voor snellere navigatie en interactie. Druk op **?** op elk scherm om het sneltoetsenvenster te openen.

Veelgebruikte sneltoetsen in de Work Item Explorer:

| Toets | Actie |
|---|---|
| **↑ / ↓** | Navigeer omhoog/omlaag in de werkitemboom |
| **← / →** | Klap een item in/uit |
| **Shift+klik** | Selecteer een reeks items |
| **Ctrl+klik** | Selecteer/deselecteer individuele items |

---

## 16. Veelgestelde vragen

**V: Ik zie lege of verouderde gegevens. Wat moet ik doen?**  
A: Klik op **Handmatig synchroniseren** op de Startpagina. Als de synchronisatie mislukt, controleer dan de TFS-verbindingsinstellingen onder **Instellingen → TFS**.

---

**V: De verfijningsscore van een Epic staat op 0%, maar ik weet dat er features zijn. Waarom?**  
A: Controleer of de backlog-rootwerkitems voor het product correct zijn ingesteld in **Instellingen → Producten**. Als de Epic buiten de geconfigureerde roots valt, wordt hij niet meegenomen in de productanalyse.

---

**V: Er staan Structural Integrity-fouten in mijn backlog. Blokkeert dat de planning?**  
A: Nee. SI-bevindingen beïnvloeden de verfijningsscores **niet** en zijn geen planningsblokkade. Ze zijn een apart onderhoudssignaal en worden weergegeven in de sectie *Integriteitsonderhoud*. Je kunt ze op je eigen tempo oplossen.

---

**V: Wat is het verschil tussen Refinement Readiness (RR) en Refinement Completeness (RC)?**  
A: RR-regels detecteren items die **verfijning blokkeren** — typisch ontbrekende beschrijvingen op Epic- of Feature-niveau (verantwoordelijkheid van de PO). RC-regels detecteren items waarbij de verfijning **onvolledig** is — typisch ontbrekende PBI-beschrijvingen of inspanningsschattingen (gedeelde verantwoordelijkheid PO en Team).

---

**V: Hoe stel ik de productfilter opnieuw in?**  
A: Op de Startpagina klik je op de **Alle producten**-chip naast de productselector. Op andere pagina's klik je op het kruisje (×) naast de productcontext-chip in de paginakop.

---

**V: Kan ik werkitems direct vanuit de applicatie bewerken in TFS?**  
A: Nee. PO Companion is een analyse- en inzichttool. Wijzigingen aan werkitems doe je in Azure DevOps zelf. Na je wijzigingen voer je een nieuwe synchronisatie uit om de cache bij te werken.

---

**V: Wat betekent de eigenaar-badge (PO / Team / Klaar) bij een Feature in het Backlog Overzicht?**  
A: De badge geeft aan wie aan zet is:  
- **PO** — de Product Owner moet actie ondernemen (Refinement Readiness-probleem).  
- **Team** — het team moet actie ondernemen (Refinement Completeness-probleem).  
- **Klaar** — de feature is volledig verfijnd.

---

**V: Wat is het verschil tussen de Validatie-fixsessie en de Work Item Explorer?**  
A: De Fixsessie is een **begeleide, gefocuste flow** voor één validatieregel: je werkt item voor item door de lijst en markeert items als afgehandeld. De Work Item Explorer is een **vrij te verkennen** hiërarchisch overzicht van alle werkitems, bruikbaar voor inspectie en filteren. Voor validatiewerk is de Fixsessie de aanbevolen route.

---

*Einde van de gebruikershandleiding*
