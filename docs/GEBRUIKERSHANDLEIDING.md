# PO Companion - Gebruikershandleiding

**Versie:** 1.0  
**Datum:** December 2024

---

## Inhoudsopgave

1. [Welkom bij PO Companion](#welkom-bij-po-companion)
2. [Aan de slag](#aan-de-slag)
3. [Hoofdfuncties](#hoofdfuncties)
   - [Startpagina](#startpagina)
   - [TFS/Azure DevOps Configuratie](#tfsazure-devops-configuratie)
   - [Work Item Explorer](#work-item-explorer)
   - [PR Insights Dashboard](#pr-insights-dashboard)
   - [Velocity Dashboard](#velocity-dashboard)
   - [Help & Datavereisten](#help--datavereisten)
4. [Toetsenbordsneltoetsen](#toetsenbordsneltoetsen)
5. [Problemen oplossen](#problemen-oplossen)
6. [Veelgestelde vragen](#veelgestelde-vragen)

---

## Welkom bij PO Companion

PO Companion is uw Azure DevOps work item management companion. De applicatie helpt u om:

- **Work items te beheren** in een hiërarchische boomstructuur
- **Pull request metrics** te analyseren voor betere team inzichten
- **Team velocity** te volgen voor data-gedreven sprint planning
- **Validatieregels** te controleren voor data-kwaliteit

```
┌─────────────────────────────────────────────────────────────┐
│                      PO COMPANION                           │
│                                                             │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  │
│  │  Work    │  │    PR    │  │ Velocity │  │  Config  │  │
│  │  Items   │  │ Insights │  │Dashboard │  │          │  │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘  │
│                                                             │
│              Uw Azure DevOps Companion                      │
└─────────────────────────────────────────────────────────────┘
```

---

## Aan de slag

### Systeemvereisten

- **Besturingssysteem:** Windows, macOS, Android of iOS
- **.NET:** Versie 10 of hoger (automatisch meegeleverd)
- **Internetverbinding:** Vereist voor synchronisatie met Azure DevOps

### Eerste keer opstarten

1. **Start de applicatie**
   - Open PO Companion vanuit uw toepassingen menu
   - De applicatie start automatisch met de ingebouwde API server

2. **Configureer uw verbinding**
   - Ga naar de Configuratie pagina
   - Vul uw Azure DevOps gegevens in
   - Sla de configuratie op

3. **Synchroniseer uw data**
   - Ga naar Work Items pagina
   - Klik op de "Sync" knop
   - Wacht tot de synchronisatie is voltooid

```
    Stap 1          Stap 2          Stap 3
┌──────────┐   ┌──────────┐   ┌──────────┐
│  Start   │──>│ Config   │──>│  Sync    │
│   App    │   │ Azure DO │   │  Data    │
└──────────┘   └──────────┘   └──────────┘
```

---

## Hoofdfuncties

### Startpagina

De startpagina biedt snel toegang tot alle hoofdfuncties van de applicatie.

```
╔═══════════════════════════════════════════════════════════╗
║              Welcome to PO Companion                      ║
║        Your Azure DevOps work item companion              ║
╠═══════════════════════════════════════════════════════════╣
║                                                           ║
║  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ║
║  │ 📋 Work Items│  │ 📊 PR Insight│  │ 📈 Velocity  │  ║
║  │              │  │              │  │              │  ║
║  │ View and     │  │ Analyze pull │  │ Track team   │  ║
║  │ manage work  │  │ request      │  │ velocity     │  ║
║  │ items in     │  │ metrics      │  │              │  ║
║  │ hierarchy    │  │              │  │              │  ║
║  │              │  │              │  │              │  ║
║  │ [Go to Work  │  │ [Go to PR    │  │ [View        │  ║
║  │  Items]      │  │  Insights]   │  │  Velocity]   │  ║
║  └──────────────┘  └──────────────┘  └──────────────┘  ║
║                                                           ║
║  ┌──────────────┐  ┌──────────────┐                     ║
║  │ ⚙️ Config    │  │ ℹ️ Help      │                     ║
║  │              │  │              │                     ║
║  │ Configure    │  │ Learn about  │                     ║
║  │ Azure DevOps │  │ data         │                     ║
║  │ connection   │  │ requirements │                     ║
║  │              │  │              │                     ║
║  │ [Go to       │  │ [View Help]  │                     ║
║  │  Config]     │  │              │                     ║
║  └──────────────┘  └──────────────┘                     ║
║                                                           ║
╠═══════════════════════════════════════════════════════════╣
║ Getting Started:                                          ║
║ ✓ Configure your Azure DevOps connection                 ║
║ ✓ View and explore your work items                       ║
║ ✓ Use filters to find specific work items quickly        ║
╚═══════════════════════════════════════════════════════════╝
```

**Functionaliteit:**
- **Feature Cards:** Snelle navigatie naar de belangrijkste functies
- **Duidelijke beschrijvingen:** Elke kaart toont wat de functie doet
- **Getting Started:** Snelle gids om snel aan de slag te gaan

---

### TFS/Azure DevOps Configuratie

Configureer hier uw verbinding met Azure DevOps of TFS.

```
╔═══════════════════════════════════════════════════════════╗
║               TFS Configuration                           ║
╠═══════════════════════════════════════════════════════════╣
║                                                           ║
║  Configure your Azure DevOps / TFS connection settings.   ║
║  Your PAT will be stored securely on your device.        ║
║                                                           ║
║  ┌─────────────────────────────────────────────────┐    ║
║  │ Organization URL *                              │    ║
║  │ https://dev.azure.com/yourorg                   │    ║
║  └─────────────────────────────────────────────────┘    ║
║    The URL of your Azure DevOps organization            ║
║                                                           ║
║  ┌─────────────────────────────────────────────────┐    ║
║  │ Project Name *                                  │    ║
║  │ MyProject                                       │    ║
║  └─────────────────────────────────────────────────┘    ║
║    The name of your project                             ║
║                                                           ║
║  ┌─────────────────────────────────────────────────┐    ║
║  │ Authentication Mode ▼                           │    ║
║  │ Personal Access Token (PAT)                     │    ║
║  └─────────────────────────────────────────────────┘    ║
║                                                           ║
║  ┌─────────────────────────────────────────────────┐    ║
║  │ Personal Access Token (PAT) *                   │    ║
║  │ •••••••••••••••••••••••••                       │    ║
║  └─────────────────────────────────────────────────┘    ║
║    Stored securely on your device (not on server)       ║
║                                                           ║
║  ┌─────────────────────────────────────────────────┐    ║
║  │ Timeout (seconds)                               │    ║
║  │ 60                                              │    ║
║  └─────────────────────────────────────────────────┘    ║
║    HTTP request timeout (5-300 seconds)                 ║
║                                                           ║
║  [💾 Save]  [🔄 Test Connection]  [✕ Clear]            ║
║                                                           ║
╚═══════════════════════════════════════════════════════════╝
```

**Vereiste velden:**
- **Organization URL:** De URL van uw Azure DevOps organisatie
- **Project Name:** De naam van uw project
- **Authentication Mode:** Kies tussen PAT of NTLM
- **Personal Access Token:** Uw persoonlijke toegangstoken (alleen voor PAT mode)

**Authenticatie Modi:**

1. **Personal Access Token (PAT):**
   - Veilig opgeslagen op uw apparaat
   - Werkt met Azure DevOps cloud
   - Vereist een PAT met werk item lees rechten

2. **NTLM/Windows Authentication:**
   - Gebruikt uw Windows inloggegevens
   - Werkt met on-premises TFS servers
   - Geen PAT nodig

**Hoe maak ik een Personal Access Token?**

```
┌─ Azure DevOps PAT Aanmaken ─────────────────────────────┐
│                                                          │
│  Stap 1: Ga naar Azure DevOps                           │
│  ├─> Klik op uw profiel icoon (rechts boven)            │
│  └─> Selecteer "Security"                               │
│                                                          │
│  Stap 2: Personal Access Tokens                         │
│  ├─> Klik op "Personal access tokens"                   │
│  └─> Klik op "+ New Token"                              │
│                                                          │
│  Stap 3: Token Configureren                             │
│  ├─> Naam: "PO Companion"                               │
│  ├─> Organization: Selecteer uw org                     │
│  ├─> Expiration: Kies verloopdatum                      │
│  └─> Scopes: Selecteer "Work Items (Read)"              │
│                                                          │
│  Stap 4: Kopieer Token                                  │
│  ├─> Klik "Create"                                      │
│  ├─> Kopieer de token (wordt maar 1x getoond!)          │
│  └─> Plak in PO Companion configuratie                  │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

**Tips:**
- ✓ Test de verbinding na het opslaan
- ✓ PAT wordt veilig opgeslagen met platform-native secure storage
- ✓ Timeout verhogen bij trage verbindingen
- ✓ Bewaar een kopie van uw PAT op een veilige plek

---

### Work Item Explorer

De Work Item Explorer toont al uw work items in een hiërarchische boomstructuur.

```
╔═══════════════════════════════════════════════════════════════════════════════════╗
║  Work Item Explorer                                    🔄 Sync  🔍 Filter  ⚙️     ║
╠═══════════════════════════════════════════════════════════════════════════════════╣
║ [🔄 Full Sync] [↻ Incremental] [🗑️ Clear State]                                  ║
║ ┌──────────────────────────────────────────────────────┐                          ║
║ │ 🔍 Filter work items by title...                     │                          ║
║ └──────────────────────────────────────────────────────┘                          ║
║ [✓ Parent Progress Issues: 3] [✓ Missing Effort Issues: 5]                       ║
║                                                                                    ║
║ Last sync: 2024-12-22 10:30 AM                                                   ║
║ ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ ║
║                                                                                    ║
║ Legend: [Epic] [Feature] [PBI] [Bug] [Task]                                      ║
║                                                                                    ║
║ ┌─ Tree View ──────────────────────────┐  ┌─ Detail Panel ──────────────────┐   ║
║ │                                      │  │                                  │   ║
║ │ ▼ [Epic] E-Commerce Platform        │  │ Selected: Website Redesign       │   ║
║ │   ├─ ▼ [Feature] User Authentication│  │                                  │   ║
║ │   │   ├─ [PBI] Login Page ⚠️         │  │ Type: Feature                    │   ║
║ │   │   ├─ [PBI] Password Reset        │  │ State: In Progress               │   ║
║ │   │   └─ [Bug] Login Timeout ❌      │  │ Area: Web/Frontend               │   ║
║ │   └─ ▶ [Feature] Shopping Cart       │  │ Iteration: Sprint 10             │   ║
║ │                                      │  │ Effort: 13 points                │   ║
║ │ ▼ [Epic] Mobile App                  │  │                                  │   ║
║ │   ├─ ▶ [Feature] iOS Version         │  │ Description:                     │   ║
║ │   └─ ▶ [Feature] Android Version     │  │ Redesign the main website...     │   ║
║ │                                      │  │                                  │   ║
║ │ ▶ [Epic] Backend Services            │  │ Validation Issues:               │   ║
║ │                                      │  │ ⚠️ Child item in progress        │   ║
║ │                                      │  │                                  │   ║
║ └──────────────────────────────────────┘  └──────────────────────────────────┘   ║
╚═══════════════════════════════════════════════════════════════════════════════════╝
```

**Belangrijkste functies:**

1. **Hiërarchische Boomstructuur:**
   - Epics → Features → PBIs/Bugs/Tasks
   - Uitklapbare/inklapbare nodes met ▶ en ▼ symbolen
   - Kleurgecodeerde work item types

2. **Synchronisatie Opties:**
   ```
   Full Sync         Incremental Sync      Clear State
   ┌──────────┐     ┌──────────┐          ┌──────────┐
   │ Haalt    │     │ Haalt    │          │ Reset    │
   │ ALLE     │     │ alleen   │          │ expanded │
   │ items op │     │ gewijzigd│          │ status   │
   └──────────┘     └──────────┘          └──────────┘
       Traag           Snel                 Cleanup
   ```

3. **Zoek & Filter:**
   - **Text filter:** Zoek in work item titels
   - **Validation filters:** Filter op issues
   - **Multi-select:** Selecteer meerdere items met Ctrl+klik
   - Filter status blijft behouden bij navigatie

4. **Validatie Indicatoren:**
   ```
   ✓  Geen issues       - Alles is OK
   ⚠️  Waarschuwing      - Let op dit item
   ❌  Error             - Actie vereist!
   ```

5. **Hiërarchie Structuur:**
   ```
   Epic (Grote initiatieven, strategische doelen)
   ├── Feature (Grote functionaliteiten)
   │   ├── PBI (Product Backlog Item - gebruikersverhaal)
   │   ├── Bug (Fout die gerepareerd moet worden)
   │   └── Task (Technische taak)
   └── Feature (Grote functionaliteiten)
       └── PBI (Product Backlog Item)
   ```

**Toetsenbord Navigatie:**
```
╔═══════════════════════════════════════════════════════════╗
║  Toets        Actie                                       ║
╠═══════════════════════════════════════════════════════════╣
║  ↑ / ↓        Navigeer omhoog/omlaag                      ║
║  →            Klap item uit (expand)                      ║
║  ←            Klap item in (collapse)                     ║
║  Enter        Toggle uitklappen                           ║
║  Space        Toggle uitklappen                           ║
║  Ctrl + A     Selecteer alle items                        ║
║  Esc          Deselecteer alle items                      ║
║  Ctrl + F     Focus op zoekbalk                           ║
║  F5           Ververs / Sync                              ║
╚═══════════════════════════════════════════════════════════╝
```

**Validatieregels:**

1. **Parent Progress Issues:**
   ```
   ❌ FOUT: Parent niet in juiste state
   
   Probleem:
   ├─ [Feature] Shopping Cart (State: New)
   │   └─ [PBI] Add to Cart (State: In Progress) ❌
   
   Oplossing:
   ├─ [Feature] Shopping Cart (State: In Progress) ✓
   │   └─ [PBI] Add to Cart (State: In Progress) ✓
   ```
   
   **Regel:** Als een child item "In Progress" is, moet de parent ook "In Progress" of "Done" zijn.

2. **Missing Effort Issues:**
   ```
   ❌ FOUT: Effort ontbreekt
   
   Probleem:
   [PBI] Login Page (State: In Progress, Effort: none) ❌
   
   Oplossing:
   [PBI] Login Page (State: In Progress, Effort: 5 SP) ✓
   ```
   
   **Regel:** Items in "In Progress" moeten een effort estimate hebben.

**Detail Paneel:**

Het detail paneel toont uitgebreide informatie over het geselecteerde work item:

```
┌─ Detail Panel ─────────────────────────────────────┐
│                                                    │
│ Title: Website Redesign                            │
│ ID: 12345                                          │
│ Type: Feature                                      │
│ State: In Progress                                 │
│                                                    │
│ Area Path: Web/Frontend                            │
│ Iteration Path: Project\2024\Q4\Sprint 10          │
│ Effort: 13 story points                            │
│                                                    │
│ Description:                                       │
│ Redesign the main website to improve UX and       │
│ modern look and feel. This includes:               │
│ - New color scheme                                 │
│ - Responsive design                                │
│ - Improved navigation                              │
│                                                    │
│ Validation Issues:                                 │
│ ⚠️ Child work item #12347 is in progress           │
│                                                    │
│ History:                                           │
│ • 2024-12-20: State changed to In Progress         │
│ • 2024-12-18: Created by John Doe                  │
│                                                    │
└────────────────────────────────────────────────────┘
```

**Tips:**
- ✓ Gebruik filters om specifieke issues snel te vinden
- ✓ Klik op validatie iconen voor details
- ✓ Gebruik keyboard shortcuts voor snelle navigatie
- ✓ Clear State om alle uitklapstatus te resetten
- ✓ Multi-select met Ctrl+klik voor bulk operaties

---

### PR Insights Dashboard

Analyseer pull request metrics voor betere team inzichten.

```
╔═══════════════════════════════════════════════════════════════════════════════════╗
║  PR Insight Dashboard                                           🔄 Sync PRs       ║
╠═══════════════════════════════════════════════════════════════════════════════════╣
║                                                                                    ║
║  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐            ║
║  │ Total PRs   │  │ Avg Time    │  │ Avg         │  │ Avg Files   │            ║
║  │     142     │  │ Open        │  │ Iterations  │  │ /PR         │            ║
║  │             │  │   3.2 days  │  │     2.8     │  │     12      │            ║
║  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘            ║
║                                                                                    ║
║  Date Range: [📅 Start Date] ─ [📅 End Date]  [Filter]                          ║
║  Group By: [▼ Author] [⬜ Show Outliers]                                         ║
║                                                                                    ║
║ ┌── PR Time Open Distribution ────────────────────────────────────────────────┐  ║
║ │                                                                              │  ║
║ │  Days                                                                        │  ║
║ │   15 │ █                                                                     │  ║
║ │   10 │ ███                                                                   │  ║
║ │    5 │ ██████████                                                            │  ║
║ │    0 │ ██████████████████                                                    │  ║
║ │      └────────────────────────────────────────────────────────              │  ║
║ │        <1d  1-2d  2-3d  3-5d  >5d                                           │  ║
║ │                                                                              │  ║
║ │  📊 Most PRs close within 2-3 days                                           │  ║
║ └──────────────────────────────────────────────────────────────────────────────┘  ║
║                                                                                    ║
║ ┌── PR Iterations vs Time Open ───────────────────────────────────────────────┐  ║
║ │                                                                              │  ║
║ │  Time                                                                        │  ║
║ │  Open       •        •                                                       │  ║
║ │    15d │         •                                                           │  ║
║ │    10d │    •  •     •                                                       │  ║
║ │     5d │ •  •••• •••••••                                                     │  ║
║ │        └──────────────────────────────                                       │  ║
║ │          1   2   3   4   5+ Iterations                                       │  ║
║ │                                                                              │  ║
║ │  💡 More iterations correlate with longer PR time                            │  ║
║ └──────────────────────────────────────────────────────────────────────────────┘  ║
║                                                                                    ║
║ ┌── Top Contributors ──────────────────────────────────────────────────────────┐  ║
║ │  Author         │ PRs │ Avg Open │ Avg Iterations │ Avg Files │             │  ║
║ │ ────────────────┼─────┼──────────┼────────────────┼───────────┼─────        │  ║
║ │  John Doe       │  25 │  2.1 d   │      2.3       │    15     │             │  ║
║ │  Jane Smith     │  22 │  3.5 d   │      3.1       │    18     │             │  ║
║ │  Bob Johnson    │  18 │  2.8 d   │      2.7       │    12     │             │  ║
║ │  Alice Williams │  15 │  4.2 d   │      3.5       │    22     │             │  ║
║ └──────────────────────────────────────────────────────────────────────────────┘  ║
╚═══════════════════════════════════════════════════════════════════════════════════╝
```

**Key Metrics Uitgelegd:**

1. **Total PRs** - Totaal aantal pull requests in de periode
2. **Avg Time Open** - Gemiddelde tijd dat PRs open staan (van create tot merge)
3. **Avg Iterations** - Gemiddeld aantal review rondes per PR
4. **Avg Files/PR** - Gemiddeld aantal gewijzigde bestanden per PR

**Wat is een PR Iteration?**

```
┌─ PR Lifecycle met Iteraties ────────────────────────────┐
│                                                          │
│  Iteration 1:                                            │
│  ├─> Developer creates PR                               │
│  ├─> Initial code push                                  │
│  └─> Reviewers add comments                             │
│                                                          │
│  Iteration 2:                                            │
│  ├─> Developer addresses feedback                       │
│  ├─> New commits pushed                                 │
│  └─> Reviewers re-review                                │
│                                                          │
│  Iteration 3:                                            │
│  ├─> Final changes                                      │
│  └─> PR approved and merged                             │
│                                                          │
│  Total: 3 iterations                                     │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

**Grafieken Interpretatie:**

1. **Time Open Distribution:**
   - Toont hoeveel PRs sluiten in welke tijd periode
   - Helpt bij het identificeren van normale review tijd
   - Outliers kunnen bottlenecks indiceren

2. **Iterations vs Time Open:**
   - Correlatie tussen review rondes en tijd
   - Meer iteraties = meestal langere open tijd
   - Helpt bij proces optimalisatie

3. **Files Changed vs Time Open:**
   - Grotere PRs (meer files) duren vaak langer
   - Kleine PRs zijn sneller te reviewen
   - Motivatie voor kleinere, focused PRs

**Filters & Grouping:**

```
╔═══════════════════════════════════════════════════════════╗
║  Filter Opties:                                           ║
╠═══════════════════════════════════════════════════════════╣
║                                                           ║
║  📅 Date Range                                            ║
║  ├─> Filter op specifieke periode                        ║
║  ├─> Gebruik voor sprint analyse                         ║
║  └─> Default: Laatste 90 dagen                           ║
║                                                           ║
║  👤 Group By Author                                       ║
║  ├─> Zie metrics per developer                           ║
║  ├─> Identificeer patterns per persoon                   ║
║  └─> Vergelijk team members                              ║
║                                                           ║
║  📊 Group By Iteration Path                               ║
║  ├─> Analyse per sprint                                  ║
║  ├─> Track velocity per iteratie                         ║
║  └─> Identificeer problematische sprints                 ║
║                                                           ║
║  🎯 Show Outliers                                         ║
║  ├─> Toon/verberg extreme waarden                        ║
║  ├─> PRs die extreem lang open staan                     ║
║  └─> Help bij bottleneck identificatie                   ║
║                                                           ║
╚═══════════════════════════════════════════════════════════╝
```

**Hoe PR's linken aan Work Items:**

In Azure DevOps, gebruik deze syntax in commits of PR beschrijving:

```
Commit message voorbeelden:
─────────────────────────────
"Fix login bug AB#12345"
"Implement shopping cart AB#12346"
"Refactor authentication AB#12347 AB#12348"

PR beschrijving voorbeelden:
─────────────────────────────
Title: Implement User Profile Page
Description:
This PR implements the user profile feature.

Related work items:
- AB#12345 (Feature: User Profile)
- AB#12346 (PBI: Edit Profile)
```

**Inzichten die u kunt krijgen:**

1. **Team Performance:**
   - Wie levert de meeste PRs?
   - Gemiddelde review tijd per persoon
   - Kwaliteit (iteraties) per developer

2. **Process Bottlenecks:**
   - PRs die te lang open staan
   - Te veel iteraties wijzen op onduidelijke requirements
   - Te grote PRs vertragen het proces

3. **Best Practices Validatie:**
   - Optimale PR grootte (10-20 files)
   - Ideale review tijd (1-3 dagen)
   - Minimale iteraties (1-2 rondes)

**Tips:**
- ✓ Gebruik date range voor sprint retrospectives
- ✓ Group by author om individuele coaching te geven
- ✓ Monitor outliers voor escalatie
- ✓ Kleinere PRs = snellere reviews
- ✓ Link PRs aan work items voor betere traceability

---

### Velocity Dashboard

Track team velocity en sprint metrics voor data-gedreven planning.

```
╔═══════════════════════════════════════════════════════════════════════════════════╗
║  Velocity Dashboard                                                               ║
╠═══════════════════════════════════════════════════════════════════════════════════╣
║                                                                                    ║
║  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐            ║
║  │ Average     │  │ Last 3      │  │ Total       │  │ Total       │            ║
║  │ Velocity    │  │ Sprints     │  │ Sprints     │  │ Completed   │            ║
║  │   42.5 SP   │  │   45.2 SP   │  │     12      │  │   510 SP    │            ║
║  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘            ║
║                                                                                    ║
║ ┌── Velocity Trend ────────────────────────────────────────────────────────────┐  ║
║ │                                                                              │  ║
║ │   SP                                                                         │  ║
║ │   60 │                                                   ▄▄▄                 │  ║
║ │   50 │                          ▄▄▄                ▄▄▄  █ █                 │  ║
║ │   40 │         ▄▄▄        ▄▄▄   █ █          ▄▄▄   █ █  █ █  ▄▄▄           │  ║
║ │   30 │   ▄▄▄   █ █  ▄▄▄   █ █   █ █    ▄▄▄   █ █   █ █  █ █  █ █           │  ║
║ │   20 │   █ █   █ █  █ █   █ █   █ █    █ █   █ █   █ █  █ █  █ █           │  ║
║ │   10 │   █ █   █ █  █ █   █ █   █ █    █ █   █ █   █ █  █ █  █ █           │  ║
║ │    0 └───────────────────────────────────────────────────────────────        │  ║
║ │       S1   S2   S3   S4   S5   S6   S7   S8   S9   S10  S11  S12            │  ║
║ │                                                                              │  ║
║ │   ─── Completed    ─── Planned    ─── 3-Sprint Average                      │  ║
║ └──────────────────────────────────────────────────────────────────────────────┘  ║
║                                                                                    ║
║ ┌── Sprint Details ────────────────────────────────────────────────────────────┐  ║
║ │  Sprint      │ Planned │ Completed │ Delta │ % Complete │ PBIs │            │  ║
║ │ ─────────────┼─────────┼───────────┼───────┼────────────┼──────┼─────       │  ║
║ │  Sprint 12   │   50    │    48     │  -2   │    96%     │ 12/13│            │  ║
║ │  Sprint 11   │   45    │    52     │  +7   │   116%     │ 13/12│            │  ║
║ │  Sprint 10   │   50    │    42     │  -8   │    84%     │ 10/13│            │  ║
║ │  Sprint 9    │   45    │    45     │   0   │   100%     │ 11/11│            │  ║
║ │  Sprint 8    │   40    │    38     │  -2   │    95%     │  9/10│            │  ║
║ └──────────────────────────────────────────────────────────────────────────────┘  ║
║                                                                                    ║
║ ┌── Effort Distribution ───────────────────────────────────────────────────────┐  ║
║ │         PBI: ██████████████████████████████ 65%                             │  ║
║ │         Bug: ███████████ 25%                                                 │  ║
║ │        Task: ████ 10%                                                        │  ║
║ │  Total: 510 story points across all work items                              │  ║
║ └──────────────────────────────────────────────────────────────────────────────┘  ║
╚═══════════════════════════════════════════════════════════════════════════════════╝
```

**Wat is Velocity?**

```
┌─ Velocity Concept ───────────────────────────────────────┐
│                                                          │
│  Velocity = Story points voltooid per sprint            │
│                                                          │
│  Sprint 10:                                              │
│  ├─ [PBI] Login (5 SP) ✓ Done                           │
│  ├─ [PBI] Profile (8 SP) ✓ Done                         │
│  ├─ [Bug] Fix bug (3 SP) ✓ Done                         │
│  ├─ [PBI] Search (13 SP) ✗ Not Done                     │
│  └─────────────────────────────────                      │
│      Total: 5 + 8 + 3 = 16 SP                           │
│      Velocity Sprint 10: 16 SP                           │
│                                                          │
│  Average Velocity (laatste 3 sprints):                   │
│  ├─ Sprint 10: 16 SP                                     │
│  ├─ Sprint 11: 20 SP                                     │
│  ├─ Sprint 12: 18 SP                                     │
│  └─────────────────────────────────                      │
│      Average: (16 + 20 + 18) / 3 = 18 SP               │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

**Key Metrics:**

1. **Average Velocity:**
   - Gemiddelde story points over alle sprints
   - Baseline voor lange termijn planning
   
2. **Last 3 Sprints:**
   - Recent velocity average
   - Meest accurate voor korte termijn forecasting
   - Reageert snel op team changes

3. **Total Sprints:**
   - Aantal sprints met data
   - Meer data = betrouwbaardere metrics

4. **Total Completed:**
   - Cumulatieve story points
   - Shows team output over tijd

**Velocity Trend Grafiek Lezen:**

```
Patroon Herkenning:
──────────────────

  Stabiel (Goed):              Stijgend (Verbetering):      Dalend (Waarschuwing):
  ▄▄▄ ▄▄▄ ▄▄▄                 ▄▄▄                          ▄▄▄
  █ █ █ █ █ █                 █ █ ▄▄▄                      █ █
  █ █ █ █ █ █                 █ █ █ █ ▄▄▄                  █ █ ▄▄▄
  ✓ Voorspelbaar              ✓ Team groeit                ⚠️ Investigate!

  Volatiel (Probleem):         Over-committen:
      ▄▄▄                      ▄▄▄                 ╱ Planned
  ▄▄▄ █ █     ▄▄▄              █ █                 ╲ Completed
  █ █ █ █ ▄▄▄ █ █              █ █
  ⚠️ Inconsistent              ⚠️ Under-delivering
```

**Sprint Details Tabel:**

```
╔═══════════════════════════════════════════════════════════╗
║  Kolom         Betekenis                                  ║
╠═══════════════════════════════════════════════════════════╣
║  Planned       Story points gecommit bij sprint start    ║
║  Completed     Story points daadwerkelijk afgerond       ║
║  Delta         Verschil (+ = over, - = under)            ║
║  % Complete    Percentage van plan behaald               ║
║  PBIs          Aantal completed / total PBIs             ║
╚═══════════════════════════════════════════════════════════╝
```

**Completion Percentage Interpretatie:**

```
100%+  ✓ Over-delivered - team was efficient
 90-99% ✓ Good - realistic planning
 80-89% ⚠️ Under - maar nog acceptabel
 <80%   ❌ Significant under-delivery - investigate!
```

**Effort Distribution:**

Toont hoe story points verdeeld zijn over work item types:

```
┌─ Ideale Verdeling ───────────────────────────────────────┐
│                                                          │
│  PBI:  60-70%  ✓ Features en user stories               │
│  Bug:  20-30%  ✓ Onderhoud en fixes                     │
│  Task: 10-20%  ✓ Technical work                         │
│                                                          │
│  Te veel bugs?   ⚠️ Quality issues                       │
│  Te veel tasks?  ⚠️ Te technical, minder value           │
│  Te weinig PBIs? ⚠️ Niet genoeg features                 │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

**Forecasting - Planning Gebruiken:**

```
╔═══════════════════════════════════════════════════════════╗
║  Gegeven: 3-sprint average = 45 SP                       ║
╠═══════════════════════════════════════════════════════════╣
║                                                           ║
║  Backlog Item:  100 SP                                    ║
║  ├─> 100 / 45 = 2.2 sprints                              ║
║  └─> Verwacht: ~2-3 sprints                              ║
║                                                           ║
║  Epic Planning: 300 SP epic                               ║
║  ├─> 300 / 45 = 6.7 sprints                              ║
║  └─> Verwacht: ~7 sprints (3.5 maanden)                  ║
║                                                           ║
║  Release Planning: 500 SP voor Q1                         ║
║  ├─> 500 / 45 = 11.1 sprints                             ║
║  └─> Q1 = 6 sprints → niet haalbaar!                     ║
║      Moet scoped worden of team uitbreiden                ║
║                                                           ║
╚═══════════════════════════════════════════════════════════╝
```

**Best Practices:**

1. **Voor Sprint Planning:**
   - Gebruik last 3 sprints average
   - Plan 80-90% van velocity (buffer voor unexpected)
   - Monitor commitment vs completion

2. **Voor Release Planning:**
   - Gebruik overall average voor lange termijn
   - Include buffer voor onzekerheden
   - Re-evaluate elke sprint

3. **Voor Team Health:**
   - Stabiele velocity is beter dan hoge velocity
   - Monitor completion percentage
   - Check effort distribution balance

**Data Requirements:**

Voor accurate velocity metrics, zorg dat work items hebben:

```
✓ Iteration Path    - Koppelt items aan sprints
✓ Effort (SP)       - Story point estimates
✓ State = Done      - Voltooide items
✓ Work Item Type    - PBI, Bug, Task classificatie
```

**Tips:**
- ✓ Gebruik 3-sprint average voor meest accurate forecasts
- ✓ Monitor completion % om over/under commitment te tracken
- ✓ Check effort distribution voor team health
- ✓ Stable velocity > high velocity
- ✓ Re-evaluate velocity na team changes

---

### Backlog Health Dashboard

Monitor de gezondheid van uw backlog over meerdere iteraties en identificeer trends en problemen vroegtijdig.

```
╔═══════════════════════════════════════════════════════════════════════════════════╗
║  Backlog Health Dashboard                                           🔄 Refresh    ║
╠═══════════════════════════════════════════════════════════════════════════════════╣
║                                                                                    ║
║  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐            ║
║  │ Total       │  │ Total       │  │ Area Paths  │  │ Iterations  │            ║
║  │ Work Items  │  │ Issues      │  │ Analyzed    │  │ Tracked     │            ║
║  │    425      │  │     23      │  │      8      │  │      6      │            ║
║  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘            ║
║                                                                                    ║
║  Trend Summary: Health improving across recent sprints                            ║
║  ├─ 📈 Effort Trend: Stable                                                       ║
║  └─ 📉 Validation Trend: Improving                                                ║
║                                                                                    ║
║ ┌── Health per Sprint ─────────────────────────────────────────────────────────┐  ║
║ │  Sprint      │ Work Items │ Issues │ Health %│ Trend                         │  ║
║ │ ─────────────┼────────────┼────────┼─────────┼─────────                      │  ║
║ │  Sprint 12   │    85      │   2    │   98%   │ ✅ Excellent                  │  ║
║ │  Sprint 11   │    78      │   4    │   95%   │ ✅ Good                       │  ║
║ │  Sprint 10   │    72      │   8    │   89%   │ ⚠️ Fair                       │  ║
║ │  Sprint 9    │    68      │   5    │   93%   │ ✅ Good                       │  ║
║ │  Sprint 8    │    65      │   9    │   86%   │ ⚠️ Fair                       │  ║
║ │  Sprint 7    │    57      │   12   │   79%   │ ❌ Needs Attention            │  ║
║ └───────────────────────────────────────────────────────────────────────────────┘  ║
║                                                                                    ║
║ ┌── Issue Types Breakdown ──────────────────────────────────────────────────────┐  ║
║ │  Missing Effort:        ████████ 35% (8 items)                                │  ║
║ │  Parent Progress Issue: ██████ 26% (6 items)                                  │  ║
║ │  Missing Iteration:     █████ 22% (5 items)                                   │  ║
║ │  Other Issues:          ████ 17% (4 items)                                    │  ║
║ └───────────────────────────────────────────────────────────────────────────────┘  ║
╚═══════════════════════════════════════════════════════════════════════════════════╝
```

**Belangrijkste Functies:**

1. **Multi-Iteratie Analyse:**
   - Track backlog health over meerdere sprints
   - Identificeer trends en patronen
   - Zie verbetering of verslechtering in de tijd

2. **Health Score:**
   ```
   Health Score Berekening:
   ─────────────────────────
   100% = Geen issues
    90%+ = Excellent (✅)
    80-89% = Good (✅)
    70-79% = Fair (⚠️)
    <70% = Needs Attention (❌)
   
   Health = (Items zonder issues / Totaal items) × 100
   ```

3. **Issue Categorisatie:**
   - **Missing Effort:** Items zonder effort estimate
   - **Parent Progress Issue:** Parent-child state inconsistenties
   - **Missing Iteration:** Items zonder sprint toewijzing
   - **Other Issues:** Overige validatieproblemen

**Vereiste TFS Data:**

Voor een accurate Backlog Health analyse moet de volgende data aanwezig zijn in TFS:

```
╔═══════════════════════════════════════════════════════════╗
║  Veld              │ Vereist │ Gebruikt voor              ║
╠═══════════════════════════════════════════════════════════╣
║  Iteration Path    │    ✓    │ Sprint groepering          ║
║  Effort (SP)       │    ✓    │ Detectie missing effort    ║
║  State             │    ✓    │ Parent-child validatie     ║
║  Parent Link       │    ✓    │ Hiërarchie validatie       ║
║  Area Path         │    ○    │ Optioneel voor filtering   ║
║  Type              │    ✓    │ Work item classificatie    ║
╚═══════════════════════════════════════════════════════════╝

Legenda: ✓ = Verplicht, ○ = Optioneel
```

**Data Kwaliteit Impact:**

```
Scenario 1: Incomplete Effort Data
┌──────────────────────────────────────────────────────┐
│ Probleem: 15 PBIs in "In Progress" zonder effort    │
│ Impact:   Health score daalt van 95% naar 82%       │
│ Oplossing: Vul effort in voor "In Progress" items   │
└──────────────────────────────────────────────────────┘

Scenario 2: Missing Iteration Paths
┌──────────────────────────────────────────────────────┐
│ Probleem: 20 items zonder iteration path            │
│ Impact:   Items niet zichtbaar in sprint analyse    │
│ Oplossing: Wijs items toe aan sprints               │
└──────────────────────────────────────────────────────┘

Scenario 3: Parent-Child Inconsistency
┌──────────────────────────────────────────────────────┐
│ Probleem: Child "In Progress", parent "New"         │
│ Impact:   Workflow inconsistentie, health issues    │
│ Oplossing: Update parent state naar "In Progress"   │
└──────────────────────────────────────────────────────┘
```

**Trends Interpretatie:**

```
Trend Indicators:
─────────────────
📈 Improving   - Health score stijgt over tijd
📊 Stable      - Health score consistent
📉 Declining   - Health score daalt over tijd
⚠️ Volatile    - Grote schommelingen tussen sprints
```

**Best Practices:**

1. **Wekelijkse Review:**
   - Check health dashboard elke sprint planning
   - Identificeer issues vroeg
   - Fix issues voordat sprint start

2. **Trend Monitoring:**
   - Monitor trends over 3-4 sprints
   - Declining trends → Proces review nodig
   - Stable trends → Goede discipline

3. **Issue Resolution:**
   - Prioriteer "Missing Effort" issues
   - Fix parent-child inconsistenties direct
   - Wijs items toe aan juiste sprint

**Tips:**
- ✓ Health score van 90%+ is uitstekend
- ✓ Declining trends vroeg detecteren voorkomt problemen
- ✓ Fix issues direct in Azure DevOps en sync
- ✓ Gebruik voor sprint readiness checks

---

### Dependency Graph Visualization

Visualiseer afhankelijkheden tussen work items en identificeer kritieke paden en blokkerende relaties.

```
╔═══════════════════════════════════════════════════════════════════════════════════╗
║  Dependency Chain Visualization                                                   ║
╠═══════════════════════════════════════════════════════════════════════════════════╣
║                                                                                    ║
║  [Area Path Filter: Web/Frontend     ] [Work Item IDs: 12345, 12348] [Load]     ║
║                                                                                    ║
║  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐            ║
║  │ Total       │  │ Chains      │  │ Critical    │  │ Blocking    │            ║
║  │ Nodes       │  │ Found       │  │ Paths       │  │ Items       │            ║
║  │     42      │  │      8      │  │      3      │  │      5      │            ║
║  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘            ║
║                                                                                    ║
║ ┌── Dependency Chains ──────────────────────────────────────────────────────────┐  ║
║ │                                                                                │  ║
║ │  Chain 1: Epic → Feature → PBI (Length: 3)                                    │  ║
║ │  ┌────────────────────────────────────────────────────────────────────┐       │  ║
║ │  │  [Epic] E-Commerce Platform (#12340) → [Feature] Shopping Cart      │       │  ║
║ │  │    State: In Progress | Effort: 150 SP                              │       │  ║
║ │  │    │                                                                 │       │  ║
║ │  │    └─> [Feature] Cart Management (#12345) ⚠️ BLOCKING               │       │  ║
║ │  │        State: In Progress | Effort: 50 SP                            │       │  ║
║ │  │        │                                                             │       │  ║
║ │  │        └─> [PBI] Add to Cart (#12348)                                │       │  ║
║ │  │            State: New | Effort: 8 SP                                 │       │  ║
║ │  └────────────────────────────────────────────────────────────────────┘       │  ║
║ │                                                                                │  ║
║ │  Chain 2: Critical Path (Length: 4) ❌                                         │  ║
║ │  ┌────────────────────────────────────────────────────────────────────┐       │  ║
║ │  │  [Epic] User Management → [Feature] Authentication                  │       │  ║
║ │  │    → [PBI] Login → [Task] DB Schema                                 │       │  ║
║ │  │  Total Effort: 45 SP | Estimated: 2 sprints                         │       │  ║
║ │  └────────────────────────────────────────────────────────────────────┘       │  ║
║ └────────────────────────────────────────────────────────────────────────────────┘  ║
║                                                                                    ║
║ ┌── Blocking Items Analysis ────────────────────────────────────────────────────┐  ║
║ │  ID     │ Title              │ Blocks │ State        │ Action Required       │  ║
║ │ ────────┼────────────────────┼────────┼──────────────┼──────────────────────│  ║
║ │  12345  │ Cart Management    │   3    │ In Progress  │ ❌ Priority 1        │  ║
║ │  12350  │ API Integration    │   2    │ New          │ ⚠️ Start ASAP        │  ║
║ │  12367  │ DB Migration       │   4    │ Blocked      │ ❌ Unblock first     │  ║
║ └────────────────────────────────────────────────────────────────────────────────┘  ║
╚═══════════════════════════════════════════════════════════════════════════════════╝
```

**Belangrijkste Functies:**

1. **Dependency Chain Detectie:**
   - Automatische detectie van parent-child relaties
   - Visualisatie van volledige dependency chains
   - Identificatie van diepte en complexiteit

2. **Critical Path Analyse:**
   ```
   Critical Path = Langste dependency chain
   
   Voorbeeld:
   Epic (150 SP) → Feature (50 SP) → PBI (13 SP) → Task (5 SP)
   Total: 218 SP
   Met velocity 40 SP/sprint = ~6 sprints critical path
   ```

3. **Blocking Items Detectie:**
   - Items die meerdere andere items blokkeren
   - Impact analyse van blokkades
   - Prioriteit indicatoren voor actie

**Vereiste TFS Data:**

Voor accurate dependency analyse moet de volgende data aanwezig zijn in TFS:

```
╔═══════════════════════════════════════════════════════════╗
║  Veld              │ Vereist │ Gebruikt voor              ║
╠═══════════════════════════════════════════════════════════╣
║  Parent Link       │    ✓    │ Hiërarchie detectie        ║
║  Related Links     │    ○    │ Dependency relaties        ║
║  State             │    ✓    │ Blokkade detectie          ║
║  Effort (SP)       │    ✓    │ Critical path berekening   ║
║  Area Path         │    ○    │ Filtering en groepering    ║
║  Iteration Path    │    ○    │ Sprint planning            ║
║  Type              │    ✓    │ Chain structuur            ║
╚═══════════════════════════════════════════════════════════╝

Legenda: ✓ = Verplicht, ○ = Optioneel
```

**Dependency Types in TFS:**

Azure DevOps ondersteunt verschillende link types voor dependencies:

```
Link Types:
───────────
1. Parent-Child (automatisch gedetecteerd)
   Epic → Feature → PBI → Task

2. Predecessor-Successor (handmatig)
   PBI #123 "blocks" → PBI #124
   
3. Related (handmatig)
   PBI #123 "relates to" → Bug #125

Setup in Azure DevOps:
─────────────────────
1. Open work item
2. Klik "Add link" in Related Work sectie
3. Selecteer link type: "Predecessor"
4. Zoek en selecteer dependent work item
5. Save
```

**Critical Path Gebruik:**

```
Scenario: Release Planning
──────────────────────────
Epic: "New Payment Gateway" (200 SP)
├─> Feature: "Payment API" (80 SP) ← Critical Path starts
│   └─> PBI: "Stripe Integration" (21 SP)
│       └─> Task: "API Keys Setup" (3 SP)
├─> Feature: "Payment UI" (60 SP) (depends on Payment API)
└─> Feature: "Testing" (40 SP) (depends on Payment UI)

Critical Path Length: 80 + 60 + 40 = 180 SP
Parallel Work: 20 SP (non-critical)
Total: 200 SP

Met velocity 40 SP/sprint:
- Critical path: 180/40 = 4.5 sprints
- Minimum release time: 5 sprints
```

**Blocking Items Impact:**

```
High Impact Blocker (blocks 3+ items):
┌────────────────────────────────────────────────┐
│ [Feature] Authentication (#12345)             │
│ Blocks:                                        │
│  ├─> PBI: User Login (#12346)                 │
│  ├─> PBI: Password Reset (#12347)             │
│  └─> PBI: User Profile (#12348)               │
│                                                │
│ Action: Prioritize #12345 to unblock 3 items  │
│ Impact: 3 sprints delay if not started        │
└────────────────────────────────────────────────┘
```

**Best Practices:**

1. **Dependency Management:**
   - Minimaliseer dependencies tussen teams
   - Maak dependencies expliciet in TFS
   - Review critical paths bij release planning

2. **Blokkade Preventie:**
   - Identificeer blocking items vroeg
   - Prioriteer items met hoge blokkade impact
   - Monitor blokkades wekelijks

3. **Chain Optimalisatie:**
   - Breek lange chains op waar mogelijk
   - Parallelliseer werk waar mogelijk
   - Reduce chain depth voor snellere delivery

**Tips:**
- ✓ Check dependency graph bij start van elke sprint
- ✓ Blocking items met 3+ dependencies = hoogste prioriteit
- ✓ Critical path bepaalt minimum release tijd
- ✓ Use related links in TFS voor expliciete dependencies

---

### Epic/Feature Completion Forecast

Voorspel wanneer een Epic of Feature voltooid zal zijn op basis van historische velocity data.

```
╔═══════════════════════════════════════════════════════════════════════════════════╗
║  Epic/Feature Completion Forecast                                                 ║
╠═══════════════════════════════════════════════════════════════════════════════════╣
║                                                                                    ║
║  [Epic/Feature ID: 12340    ] [Historical Sprints: 5 ] [Calculate]               ║
║                                                                                    ║
║ ┌── Epic/Feature Details ───────────────────────────────────────────────────────┐  ║
║ │                                                                                │  ║
║ │  [Epic] E-Commerce Platform (#12340)                                          │  ║
║ │  State: In Progress | Area: Web/Frontend                                      │  ║
║ │                                                                                │  ║
║ │  Total Effort: 250 story points                                               │  ║
║ │  ├─ Completed: 120 SP (48%)                                                   │  ║
║ │  └─ Remaining: 130 SP (52%)                                                   │  ║
║ │                                                                                │  ║
║ │  Child Work Items: 18 total                                                   │  ║
║ │  ├─ Done: 8 items (44%)                                                       │  ║
║ │  ├─ In Progress: 4 items (22%)                                                │  ║
║ │  └─ Not Started: 6 items (34%)                                                │  ║
║ │                                                                                │  ║
║ └────────────────────────────────────────────────────────────────────────────────┘  ║
║                                                                                    ║
║ ┌── Velocity Analysis ──────────────────────────────────────────────────────────┐  ║
║ │                                                                                │  ║
║ │  Historical Velocity (last 5 sprints):                                        │  ║
║ │  Sprint 12: 45 SP                                                             │  ║
║ │  Sprint 11: 42 SP                                                             │  ║
║ │  Sprint 10: 38 SP                                                             │  ║
║ │  Sprint 9:  44 SP                                                             │  ║
║ │  Sprint 8:  41 SP                                                             │  ║
║ │                                                                                │  ║
║ │  Average Velocity: 42 SP per sprint                                           │  ║
║ │  Standard Deviation: ±3 SP                                                    │  ║
║ │                                                                                │  ║
║ └────────────────────────────────────────────────────────────────────────────────┘  ║
║                                                                                    ║
║ ┌── Completion Forecast ────────────────────────────────────────────────────────┐  ║
║ │                                                                                │  ║
║ │  Remaining Work: 130 SP                                                       │  ║
║ │  Average Velocity: 42 SP/sprint                                               │  ║
║ │                                                                                │  ║
║ │  📊 Forecast:                                                                  │  ║
║ │  ├─ Best Case (45 SP/sprint):  ~3 sprints (6 weeks)                          │  ║
║ │  ├─ Likely Case (42 SP/sprint): ~4 sprints (8 weeks)  ⭐ Most probable       │  ║
║ │  └─ Worst Case (38 SP/sprint):  ~4 sprints (8-9 weeks)                       │  ║
║ │                                                                                │  ║
║ │  📅 Estimated Completion: Sprint 16 (Mid February 2025)                       │  ║
║ │  🎯 Confidence: 75% (based on consistent velocity)                            │  ║
║ │                                                                                │  ║
║ └────────────────────────────────────────────────────────────────────────────────┘  ║
║                                                                                    ║
║ ┌── Sprint-by-Sprint Projection ───────────────────────────────────────────────┐  ║
║ │  Sprint │ Projected Work │ Cumulative │ Remaining │ Status                   │  ║
║ │ ────────┼────────────────┼────────────┼───────────┼─────────────────         │  ║
║ │  S13    │    42 SP       │   162 SP   │   88 SP   │ 🔄 Current               │  ║
║ │  S14    │    42 SP       │   204 SP   │   46 SP   │ 📅 Next sprint           │  ║
║ │  S15    │    42 SP       │   246 SP   │    4 SP   │ 📅 Planned               │  ║
║ │  S16    │     4 SP       │   250 SP   │    0 SP   │ ✅ Completion!           │  ║
║ └────────────────────────────────────────────────────────────────────────────────┘  ║
╚═══════════════════════════════════════════════════════════════════════════════════╝
```

**Belangrijkste Functies:**

1. **Velocity-Based Forecasting:**
   - Gebruikt historische velocity data
   - Berekent best/likely/worst case scenarios
   - Geeft confidence level op basis van consistentie

2. **Progress Tracking:**
   ```
   Progress Berekening:
   ───────────────────
   Total Effort: 250 SP
   Completed: 120 SP
   Remaining: 130 SP
   
   % Complete = (120 / 250) × 100 = 48%
   
   Met velocity 42 SP/sprint:
   Remaining sprints = 130 / 42 = 3.1 sprints
   ```

3. **Sprint-by-Sprint Projection:**
   - Detailleerde breakdown per sprint
   - Cumulatieve effort tracking
   - Remaining work per sprint

**Vereiste TFS Data:**

Voor accurate forecasting moet de volgende data aanwezig zijn in TFS:

```
╔═══════════════════════════════════════════════════════════╗
║  Veld              │ Vereist │ Gebruikt voor              ║
╠═══════════════════════════════════════════════════════════╣
║  Effort (SP)       │    ✓    │ Total en remaining work    ║
║  State             │    ✓    │ Completed vs remaining     ║
║  Parent Link       │    ✓    │ Child work items ophalen   ║
║  Iteration Path    │    ✓    │ Velocity berekening        ║
║  Completed Date    │    ○    │ Historische velocity       ║
║  Area Path         │    ○    │ Context informatie         ║
╚═══════════════════════════════════════════════════════════╝

Legenda: ✓ = Verplicht, ○ = Optioneel
```

**Data Kwaliteit voor Accurate Forecasts:**

```
✓ Alle child items hebben effort estimates
✓ Historische sprints hebben consistente velocity
✓ Completed items zijn gemarkeerd als "Done"
✓ Iteration paths zijn correct ingevuld
✓ Geen grote effort schommelingen tussen sprints

Example Impact:
──────────────
Scenario A: Perfect Data
- 5 sprints met velocity 40, 42, 41, 43, 40
- Forecast: 4 sprints ± 0.5 sprints
- Confidence: 90%

Scenario B: Inconsistent Data  
- 5 sprints met velocity 20, 50, 35, 45, 25
- Forecast: 4 sprints ± 2 sprints
- Confidence: 60%
```

**Forecasting Scenarios:**

```
Scenario 1: On-Track Epic
──────────────────────────
Total: 200 SP | Completed: 100 SP (50%)
Velocity: 40 SP/sprint (consistent)
Forecast: 2.5 sprints remaining
Confidence: HIGH ✅

Action: Continue current pace

Scenario 2: Behind Schedule Epic
─────────────────────────────────
Total: 200 SP | Completed: 40 SP (20%)
Velocity: 25 SP/sprint (declining)
Forecast: 6.4 sprints remaining
Confidence: MEDIUM ⚠️

Action: Review scope or increase capacity

Scenario 3: Scope Creep Epic
───────────────────────────
Total: 300 SP (was 200 SP) | Completed: 80 SP
Velocity: 40 SP/sprint
Forecast: 5.5 sprints (was 3 sprints)
Confidence: LOW ❌

Action: Re-evaluate scope and priorities
```

**Confidence Level Interpretatie:**

```
╔═══════════════════════════════════════════════════════════╗
║  Confidence │ Velocity Pattern    │ Forecast Reliability  ║
╠═══════════════════════════════════════════════════════════╣
║  90%+       │ Std dev < 5% avg    │ ✅ Zeer betrouwbaar  ║
║  75-89%     │ Std dev 5-15% avg   │ ✅ Betrouwbaar       ║
║  60-74%     │ Std dev 15-25% avg  │ ⚠️ Matig             ║
║  < 60%      │ Std dev > 25% avg   │ ❌ Onbetrouwbaar     ║
╚═══════════════════════════════════════════════════════════╝
```

**Best Practices:**

1. **Velocity Stabiliteit:**
   - Gebruik minimaal 3-5 sprints voor velocity berekening
   - Meer sprints = betrouwbaardere forecast
   - Monitor voor grote velocity swings

2. **Scope Management:**
   - Freeze scope voor accurate forecasts
   - Track scope changes apart
   - Re-forecast bij significante scope wijzigingen

3. **Regular Updates:**
   - Update forecast elke sprint
   - Communiceer forecast wijzigingen
   - Gebruik voor stakeholder management

**Tips:**
- ✓ Best case = optimistisch (max velocity)
- ✓ Likely case = realistisch (gemiddelde velocity)
- ✓ Worst case = conservatief (min velocity)
- ✓ Plan op likely case, buffer voor worst case
- ✓ Update forecast wekelijks voor accuracy

---

### State Timeline Analysis

Analyseer de state transitions van work items en identificeer bottlenecks in uw workflow.

```
╔═══════════════════════════════════════════════════════════════════════════════════╗
║  Work Item State Timeline                                                         ║
╠═══════════════════════════════════════════════════════════════════════════════════╣
║                                                                                    ║
║  [Work Item ID: 12345    ] [Analyze]                                              ║
║                                                                                    ║
║ ┌── Work Item Information ──────────────────────────────────────────────────────┐  ║
║ │  [PBI] Implement Shopping Cart (#12345)                                       │  ║
║ │  Type: Product Backlog Item | Area: Web/Frontend                              │  ║
║ │  Current State: Done | Effort: 13 SP                                          │  ║
║ │  Created: 2024-11-15 | Completed: 2024-12-18                                  │  ║
║ │  Total Lead Time: 33 days                                                     │  ║
║ └────────────────────────────────────────────────────────────────────────────────┘  ║
║                                                                                    ║
║ ┌── State Timeline ─────────────────────────────────────────────────────────────┐  ║
║ │                                                                                │  ║
║ │  New            In Progress      Code Review       Testing          Done      │  ║
║ │  ●───────3d─────●──────12d──────●──────5d────────●─────8d──────────●         │  ║
║ │  Nov 15         Nov 18           Nov 30           Dec 5             Dec 13    │  ║
║ │  │              │                │                │                 │         │  ║
║ │  Created        Started          Review           Testing           Closed    │  ║
║ │                                                                                │  ║
║ │  State Durations:                                                              │  ║
║ │  ├─ New:          3 days (9%)                                                 │  ║
║ │  ├─ In Progress: 12 days (36%) ⚠️ BOTTLENECK                                  │  ║
║ │  ├─ Code Review:  5 days (15%)                                                │  ║
║ │  ├─ Testing:      8 days (24%)                                                │  ║
║ │  └─ Done:        ~5 days (remaining to today)                                 │  ║
║ │                                                                                │  ║
║ │  Total Cycle Time: 28 days (from start to done)                               │  ║
║ │  Total Lead Time: 33 days (from created to done)                              │  ║
║ │                                                                                │  ║
║ └────────────────────────────────────────────────────────────────────────────────┘  ║
║                                                                                    ║
║ ┌── Bottleneck Analysis ────────────────────────────────────────────────────────┐  ║
║ │                                                                                │  ║
║ │  🚨 Bottleneck Detected: "In Progress" State                                   │  ║
║ │                                                                                │  ║
║ │  Duration: 12 days (36% of total time)                                        │  ║
║ │  Team Average: 6 days for PBIs                                                │  ║
║ │  Deviation: +100% (2x longer than average)                                    │  ║
║ │                                                                                │  ║
║ │  Possible Causes:                                                              │  ║
║ │  • Scope larger than estimated (13 SP)                                        │  ║
║ │  • Technical complexity                                                        │  ║
║ │  • Dependencies on external teams                                              │  ║
║ │  • Developer capacity issues                                                   │  ║
║ │                                                                                │  ║
║ │  Recommendations:                                                              │  ║
║ │  ✓ Break down large PBIs (>8 SP)                                              │  ║
║ │  ✓ Identify dependencies earlier                                              │  ║
║ │  ✓ Review estimation accuracy                                                 │  ║
║ │                                                                                │  ║
║ └────────────────────────────────────────────────────────────────────────────────┘  ║
║                                                                                    ║
║ ┌── Comparison with Team Averages ──────────────────────────────────────────────┐  ║
║ │  State        │ This Item │ Team Avg │ Difference │ Status                    │  ║
║ │ ──────────────┼───────────┼──────────┼────────────┼───────────────            │  ║
║ │  New          │   3 days  │  2 days  │   +1 day   │ ✅ Normal                 │  ║
║ │  In Progress  │  12 days  │  6 days  │   +6 days  │ ❌ SLOW (2x)              │  ║
║ │  Code Review  │   5 days  │  3 days  │   +2 days  │ ⚠️ Slower                 │  ║
║ │  Testing      │   8 days  │  4 days  │   +4 days  │ ⚠️ Slower                 │  ║
║ └────────────────────────────────────────────────────────────────────────────────┘  ║
╚═══════════════════════════════════════════════════════════════════════════════════╝
```

**Belangrijkste Functies:**

1. **State Transition Tracking:**
   - Volledige historie van state changes
   - Exacte timestamps van elke transition
   - Tijd in elke state gemeten

2. **Cycle Time vs Lead Time:**
   ```
   Definities:
   ───────────
   Lead Time = Van "Created" tot "Done"
              (Totale tijd inclusief wachttijd)
   
   Cycle Time = Van "In Progress" tot "Done"
               (Actieve werk tijd)
   
   Voorbeeld:
   Created: Nov 15
   Started: Nov 18 (3 dagen wachttijd)
   Done:    Dec 13
   
   Lead Time = 28 dagen (Nov 15 - Dec 13)
   Cycle Time = 25 dagen (Nov 18 - Dec 13)
   ```

3. **Bottleneck Detection:**
   - Automatische identificatie van langzame states
   - Vergelijking met team gemiddeldes
   - Aanbevelingen voor verbetering

**Vereiste TFS Data:**

Voor State Timeline analyse moet de volgende data aanwezig zijn in TFS:

```
╔═══════════════════════════════════════════════════════════╗
║  Veld              │ Vereist │ Gebruikt voor              ║
╠═══════════════════════════════════════════════════════════╣
║  State History     │    ✓    │ State transitions          ║
║  Changed Date      │    ✓    │ Timestamps van changes     ║
║  State             │    ✓    │ Huidige state              ║
║  Created Date      │    ✓    │ Lead time berekening       ║
║  Closed Date       │    ○    │ Completion tracking        ║
║  Type              │    ✓    │ Team averages per type     ║
║  Effort (SP)       │    ○    │ Complexity analyse         ║
╚═══════════════════════════════════════════════════════════╝

Legenda: ✓ = Verplicht, ○ = Optioneel
```

**State History in TFS:**

Azure DevOps tracked automatisch alle state changes:

```
Work Item History bevat:
───────────────────────
✓ Welke velden zijn gewijzigd
✓ Oude waarde → Nieuwe waarde
✓ Wanneer (datum + tijd)
✓ Door wie (user)

Toegang via:
1. Open work item in Azure DevOps
2. Ga naar "History" tab
3. Filter op "State" changes
4. Zie volledige state transition log
```

**Bottleneck Patronen:**

```
Pattern 1: Development Bottleneck
──────────────────────────────────
State: "In Progress"
Duration: 2-3x team average
Oorzaak: Technical complexity, scope creep
Actie: Better estimation, smaller PBIs

Pattern 2: Review Bottleneck
────────────────────────────
State: "Code Review"
Duration: 5+ dagen
Oorzaak: Reviewer capacity, large PRs
Actie: Smaller PRs, dedicated review time

Pattern 3: Testing Bottleneck
─────────────────────────────
State: "Testing"
Duration: 1+ week
Oorzaak: Test environment issues, complex scenarios
Actie: Test automation, better test data

Pattern 4: Handoff Delays
─────────────────────────
Long transitions between states
Oorzaak: Communication gaps, unclear handoffs
Actie: Better team coordination, clear DoD
```

**Metrics Interpretatie:**

```
╔═══════════════════════════════════════════════════════════╗
║  Metric          │ Good      │ Warning   │ Action Needed  ║
╠═══════════════════════════════════════════════════════════╣
║  Lead Time       │ < 2 weeks │ 2-4 weeks │ > 4 weeks      ║
║  Cycle Time      │ < 1 week  │ 1-2 weeks │ > 2 weeks      ║
║  Time in State   │ < avg     │ 1-2x avg  │ > 2x avg       ║
║  State Changes   │ 3-5       │ 6-8       │ > 8 (churn)    ║
╚═══════════════════════════════════════════════════════════╝
```

**Use Cases:**

```
Use Case 1: Sprint Retrospective
─────────────────────────────────
Analyse work items uit vorige sprint
Identificeer gemeenschappelijke bottlenecks
Bespreek in retro en maak verbeteracties

Use Case 2: Process Improvement
───────────────────────────────
Track meerdere items over tijd
Identificeer systematische delays
Optimize workflow states/policies

Use Case 3: Estimation Calibration
──────────────────────────────────
Vergelijk effort met cycle time
Items met 13 SP → gemiddeld X dagen
Update estimation baseline

Use Case 4: Individual Item Analysis
────────────────────────────────────
Waarom duurde dit item zo lang?
Lessons learned voor toekomst
Document in work item notes
```

**Best Practices:**

1. **Regular Analysis:**
   - Analyseer items na completion
   - Identificeer patterns over meerdere sprints
   - Track improvements over tijd

2. **Team Averages:**
   - Gebruik voor relative comparison
   - Niet voor absolute benchmarks
   - Update periodiek (elke quarter)

3. **Bottleneck Resolution:**
   - Focus op grootste bottlenecks eerst
   - Meet impact van wijzigingen
   - Iterate op proces verbeteringen

**Tips:**
- ✓ Analyse volledige sprints voor patterns
- ✓ 2x team average = significant bottleneck
- ✓ Short cycle times = efficient workflow
- ✓ Gebruik voor proces optimalisatie
- ✓ Document learnings in retrospectives

---

### Effort Distribution Heat Map

Visualiseer effort distributie over area paths en iteraties voor capaciteitsplanning.

```
╔═══════════════════════════════════════════════════════════════════════════════════╗
║  Effort Distribution Heat Map                                     🔄 Refresh       ║
╠═══════════════════════════════════════════════════════════════════════════════════╣
║                                                                                    ║
║  [Area Path Filter: ___________] [Max Iterations: 6 ] [Default Capacity: 50 ]   ║
║                                                                                    ║
║  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐            ║
║  │ Total       │  │ Area Paths  │  │ Iterations  │  │ Avg         │            ║
║  │ Effort      │  │ Tracked     │  │ Analyzed    │  │ Utilization │            ║
║  │   450 SP    │  │      8      │  │      6      │  │    82%      │            ║
║  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘            ║
║                                                                                    ║
║ ┌── Effort Heat Map (Area Path × Iteration) ───────────────────────────────────┐  ║
║ │                                                                                │  ║
║ │  Area Path       │ S10  │ S11  │ S12  │ S13  │ S14  │ S15  │ Total │ Avg   │  ║
║ │ ─────────────────┼──────┼──────┼──────┼──────┼──────┼──────┼───────┼─────  │  ║
║ │  Web/Frontend    │  45  │  48  │  42  │  50  │  47  │  45  │  277  │  46   │  ║
║ │                  │ 🟢90%│ 🟢96%│ 🟢84%│ 🔴100│ 🟢94%│ 🟢90%│       │       │  ║
║ │                  │      │      │      │      │      │      │       │       │  ║
║ │  Web/Backend     │  38  │  42  │  40  │  35  │  38  │  40  │  233  │  39   │  ║
║ │                  │ 🟢76%│ 🟢84%│ 🟢80%│ 🟢70%│ 🟢76%│ 🟢80%│       │       │  ║
║ │                  │      │      │      │      │      │      │       │       │  ║
║ │  Mobile/iOS      │  25  │  28  │  30  │  32  │  28  │  25  │  168  │  28   │  ║
║ │                  │ 🟡50%│ 🟡56%│ 🟡60%│ 🟡64%│ 🟡56%│ 🟡50%│       │       │  ║
║ │                  │      │      │      │      │      │      │       │       │  ║
║ │  Mobile/Android  │  22  │  25  │  28  │  30  │  26  │  24  │  155  │  26   │  ║
║ │                  │ 🟡44%│ 🟡50%│ 🟡56%│ 🟡60%│ 🟡52%│ 🟡48%│       │       │  ║
║ │                  │      │      │      │      │      │      │       │       │  ║
║ │  Infrastructure  │  15  │  18  │  20  │  15  │  18  │  16  │  102  │  17   │  ║
║ │                  │ 🟢30%│ 🟢36%│ 🟡40%│ 🟢30%│ 🟢36%│ 🟢32%│       │       │  ║
║ │ ─────────────────┼──────┼──────┼──────┼──────┼──────┼──────┼───────┼─────  │  ║
║ │  Total per Sprint│ 145  │ 161  │ 160  │ 162  │ 157  │ 150  │  935  │       │  ║
║ │  Capacity        │ 250  │ 250  │ 250  │ 250  │ 250  │ 250  │ 1500  │       │  ║
║ │  % Utilization   │  58% │  64% │  64% │  65% │  63% │  60% │  62%  │       │  ║
║ │                                                                                │  ║
║ └────────────────────────────────────────────────────────────────────────────────┘  ║
║                                                                                    ║
║  Legend: 🔴 At/Over Capacity (>95%) | 🟢 Healthy (60-95%) | 🟡 Under-utilized (<60%) │
║                                                                                    ║
║ ┌── Capacity Planning Insights ─────────────────────────────────────────────────┐  ║
║ │                                                                                │  ║
║ │  ⚠️ Overallocation Detected:                                                   │  ║
║ │  • Web/Frontend in Sprint 13: 50 SP (100% capacity)                           │  ║
║ │    Action: Consider redistributing 5-10 SP to other sprints                   │  ║
║ │                                                                                │  ║
║ │  ℹ️ Under-utilization:                                                         │  ║
║ │  • Mobile teams averaging 50-55% capacity                                     │  ║
║ │    Action: Consider additional work or cross-training                         │  ║
║ │                                                                                │  ║
║ │  ✓ Balanced Areas:                                                             │  ║
║ │  • Web/Backend: Consistent 76-84% utilization                                 │  ║
║ │  • Infrastructure: Stable 30-40% (appropriate for support work)               │  ║
║ │                                                                                │  ║
║ └────────────────────────────────────────────────────────────────────────────────┘  ║
╚═══════════════════════════════════════════════════════════════════════════════════╝
```

**Belangrijkste Functies:**

1. **Heat Map Visualisatie:**
   - Matrix van Area Path × Iteration
   - Kleurcodering voor capacity utilization
   - Snelle identificatie van over/under allocation

2. **Capacity Utilization:**
   ```
   Utilization Berekening:
   ──────────────────────
   Utilization % = (Planned Effort / Capacity) × 100
   
   Voorbeeld:
   Area: Web/Frontend
   Sprint 13 Planned: 50 SP
   Sprint 13 Capacity: 50 SP
   Utilization: (50 / 50) × 100 = 100% 🔴
   
   Interpretatie:
   🔴 >95%    = At/over capacity (risk!)
   🟢 60-95%  = Healthy utilization
   🟡 <60%    = Under-utilized (opportunity)
   ```

3. **Multi-Sprint Planning:**
   - Zie effort distributie over meerdere sprints
   - Identificeer overload in toekomstige sprints
   - Balance work load vooraf

**Vereiste TFS Data:**

Voor Effort Distribution analyse moet de volgende data aanwezig zijn in TFS:

```
╔═══════════════════════════════════════════════════════════╗
║  Veld              │ Vereist │ Gebruikt voor              ║
╠═══════════════════════════════════════════════════════════╣
║  Iteration Path    │    ✓    │ Sprint groepering          ║
║  Area Path         │    ✓    │ Team/component groepering  ║
║  Effort (SP)       │    ✓    │ Capacity berekening        ║
║  State             │    ○    │ Planned vs done filtering  ║
║  Type              │    ○    │ Work type analyse          ║
╚═══════════════════════════════════════════════════════════╝

Legenda: ✓ = Verplicht, ○ = Optioneel
```

**Capacity Planning in TFS:**

TFS/Azure DevOps ondersteunt team capacity settings:

```
Setup Team Capacity:
───────────────────
1. Azure DevOps → Boards → Sprints
2. Selecteer sprint (bijv. Sprint 13)
3. Klik "Capacity" tab
4. Voor elk team member:
   ├─ Capacity per day: 6 hours
   ├─ Days off: Mark vakantie
   └─ Activity split: Development 80%, Testing 20%

5. Team total capacity:
   ├─ 5 developers × 6 hours × 10 days = 300 hours
   ├─ Convert to SP: 300h / 6h per SP = 50 SP
   └─ Set as sprint capacity

PO Companion gebruikt:
─────────────────────
• Default capacity (configureerbaar)
• Of haalt capacity uit TFS API (indien beschikbaar)
• Vergelijkt planned effort met capacity
```

**Capacity Planning Scenarios:**

```
Scenario 1: Balanced Load
─────────────────────────
Area: Web/Backend
6 sprints averaging 38-42 SP
Team capacity: 50 SP
Utilization: 76-84%
Status: ✅ Healthy - goed gebalanceerd

Scenario 2: Overallocation
──────────────────────────
Area: Web/Frontend  
Sprint 13: 50 SP planned
Team capacity: 50 SP
Utilization: 100%
Status: 🔴 Risk - geen buffer voor unexpected work
Action: Reduce to 45 SP (90% utilization)

Scenario 3: Under-utilization
─────────────────────────────
Area: Mobile/Android
6 sprints averaging 25 SP
Team capacity: 50 SP
Utilization: 50%
Status: 🟡 Opportunity - kan meer werk aan
Action: Evaluate if capacity is correct, or add work

Scenario 4: Uneven Distribution
───────────────────────────────
Sprints: 30, 60, 35, 55, 40, 45 SP
Capacity: 50 SP
Status: ⚠️ Volatile - re-balance needed
Action: Move work from high sprints to low sprints
```

**Heat Map Patronen:**

```
Pattern: Even Horizontal Distribution
─────────────────────────────────────
Same area has consistent utilization across sprints
Example: Backend → 80%, 82%, 79%, 81%, 80%, 82%
Meaning: Stable team, predictable work
Action: Continue current planning

Pattern: Spike Detection
───────────────────────
One sprint significantly higher than others
Example: S10:40, S11:40, S12:70, S13:40, S14:40
Meaning: Overload in S12 (release sprint?)
Action: Redistribute work or adjust expectations

Pattern: Under-utilization Trend
────────────────────────────────
Decreasing utilization over sprints
Example: 80% → 75% → 65% → 55% → 50%
Meaning: Team capacity not fully utilized
Action: Add work or review team size

Pattern: Cross-Area Imbalance
─────────────────────────────
One area consistently high, others low
Example: Frontend 90%, Backend 45%, Mobile 40%
Meaning: Uneven team sizes or work distribution
Action: Cross-training or rebalancing teams
```

**Capacity Optimization:**

```
Rule 1: Sweet Spot Utilization
──────────────────────────────
Target: 75-85% capacity utilization
Reason: 
├─ Buffer for unexpected work
├─ Time for code reviews, meetings
└─ Prevents burnout

Rule 2: Never Plan to 100%
──────────────────────────
Max planning: 90% of capacity
Reason:
├─ Bugs and support work
├─ Technical debt
└─ Team meetings and planning

Rule 3: Balance Across Sprints
──────────────────────────────
Avoid: 50%, 50%, 100%, 40%, 50%
Better: 65%, 70%, 75%, 70%, 65%
Reason:
└─ Consistent velocity is sustainable

Rule 4: Area-Specific Targets
─────────────────────────────
Development teams: 75-85%
Infrastructure teams: 50-70% (on-call, support)
New teams: 60-70% (learning curve)
```

**Best Practices:**

1. **Sprint Planning:**
   - Check heat map voor volgende 2-3 sprints
   - Identificeer overallocations vroeg
   - Rebalance work voordat sprint start

2. **Release Planning:**
   - Use heat map voor multi-sprint visibility
   - Balance work evenly over release
   - Avoid last-sprint cramming

3. **Team Balancing:**
   - Monitor utilization per area
   - Identify under/over utilized teams
   - Consider cross-training of reorg

**Tips:**
- ✓ 🟢 Groen = gezonde utilization (60-95%)
- ✓ 🔴 Rood = risico van overload (>95%)
- ✓ 🟡 Geel = onder-utilized (<60%)
- ✓ Plan op 80-85% voor optimale buffer
- ✓ Update capacity settings in TFS voor accuracy

---

### Help & Datavereisten

Uitleg over data requirements en validatieregels.

De Help pagina in de applicatie legt uit welke data vereist is voor elke feature.

```
╔═══════════════════════════════════════════════════════════════════════════════════╗
║  Help & Data Requirements                                                         ║
╠═══════════════════════════════════════════════════════════════════════════════════╣
║  ℹ️ Why Data Quality Matters                                                      ║
║  PO Companion provides powerful analytics. Voor betrouwbare metrics moet          ║
║  bepaalde data consistent ingevuld zijn in Azure DevOps.                          ║
║ ┌─ Feature Requirements ────────────────────────────────────────────────────────┐ ║
║ │ ▼ Velocity Dashboard                                                          │ ║
║ │   Required Fields:                                                            │ ║
║ │   ✓ Iteration Path (alle work items)                                         │ ║
║ │   ✓ Effort/Story Points (PBIs en tasks)                                      │ ║
║ │   ✓ State (Done/Closed/Completed/Resolved)                                   │ ║
║ │   ✓ Type (PBI/Bug/Task)                                                      │ ║
║ │                                                                               │ ║
║ │ ▼ Work Item Explorer                                                          │ ║
║ │   Required Fields:                                                            │ ║
║ │   ✓ Title (duidelijk, beschrijvend)                                          │ ║
║ │   ✓ Area Path (voor filtering)                                               │ ║
║ │   ✓ Parent Work Item (voor hiërarchie)                                       │ ║
║ │   ✓ State (huidige status)                                                   │ ║
║ │                                                                               │ ║
║ │   Validation Rules:                                                           │ ║
║ │   ❌ Error: In Progress items MOETEN effort hebben                            │ ║
║ │   ❌ Error: Als child In Progress, parent moet In Progress of Done zijn       │ ║
║ └───────────────────────────────────────────────────────────────────────────────┘ ║
║ ┌─ Common Validation Rules ─────────────────────────────────────────────────────┐ ║
║ │  Regel                      │ Severity │ Impact                              │ ║
║ │ ────────────────────────────┼──────────┼─────────────────────────────────    │ ║
║ │  In Progress → Effort Req'd │  ❌ Error │ Prevent incomplete velocity data   │ ║
║ │  Parent-Child State Consist │  ❌ Error │ Maintains hierarchy consistency    │ ║
║ │  Iteration Path Format      │  ⚠️ Warn  │ Improves sprint grouping          │ ║
║ │  Effort Value Range (1-13)  │  ⚠️ Warn  │ Ensures realistic estimates       │ ║
║ └───────────────────────────────────────────────────────────────────────────────┘ ║
╚═══════════════════════════════════════════════════════════════════════════════════╝
```

**Required Fields per Work Item Type:**

```
╔═══════════════╦═══════════════╦═══════════════╗
║     Epic      ║ Feature / PBI ║   Bug / Task  ║
╠═══════════════╬═══════════════╬═══════════════╣
║ • Title       ║ • Title       ║ • Title       ║
║ • Area Path   ║ • Area Path   ║ • Area Path   ║
║ • State       ║ • Iteration   ║ • Iteration   ║
║ • Iteration   ║ • State       ║ • State       ║
║   (optional)  ║ • Effort (SP) ║ • Effort      ║
║               ║ • Parent      ║ • Parent      ║
╚═══════════════╩═══════════════╩═══════════════╝
```

---

## Toetsenbordsneltoetsen

Verhoog uw productiviteit met keyboard shortcuts.

```
╔═══════════════════════════════════════════════════════════╗
║              Keyboard Shortcuts                           ║
╠═══════════════════════════════════════════════════════════╣
║  Global (werkt overal):                                   ║
║    Ctrl + /       Open shortcuts help                     ║
║    Ctrl + ,       Open settings                           ║
║                                                           ║
║  Navigation:                                              ║
║    Alt + 1        Ga naar Home                            ║
║    Alt + 2        Ga naar Work Items                      ║
║    Alt + 3        Ga naar PR Insights                     ║
║    Alt + 4        Ga naar Velocity Dashboard              ║
║    Alt + 5        Ga naar Configuration                   ║
║                                                           ║
║  Work Item Explorer:                                      ║
║    ↑ / ↓          Navigeer omhoog/omlaag                  ║
║    → / ←          Expand/collapse node                    ║
║    Enter / Space  Toggle expand                           ║
║    Ctrl + A       Selecteer alles                         ║
║    Esc            Deselecteer alles                       ║
║    Ctrl + F       Focus zoekbalk                          ║
║    F5             Refresh / Sync                          ║
║    /              Focus filter box                        ║
╚═══════════════════════════════════════════════════════════╝
```

---

## Problemen oplossen

### Verbindingsproblemen

**❌ Kan geen verbinding maken met Azure DevOps**

```
Checklist:
├─ ✓ Is Organization URL correct formaat?
│    https://dev.azure.com/yourorg
├─ ✓ Is PAT geldig en niet verlopen?
├─ ✓ Heeft PAT juiste permissions (Work Items Read)?
├─ ✓ Is netwerk toegankelijk?
└─ ✓ Firewall settings correct?

Oplossingen:
1. Test connection in Config pagina
2. Ververs PAT in Azure DevOps
3. Verhoog timeout waarde
4. Check firewall rules
```

**❌ NTLM authenticatie werkt niet**

```
Checklist:
├─ ✓ Is TFS on-premises (niet cloud)?
├─ ✓ Windows credentials correct?
├─ ✓ "Use Default Credentials" aangevinkt?
└─ ✓ Domain toegang beschikbaar?
```

### Synchronisatie Problemen

**⚠️ Work items synchroniseren niet**

```
Troubleshooting:
├─ 1. Check TFS configuratie
├─ 2. Test connection
├─ 3. Verifieer area path
├─ 4. Check API rate limits
└─ 5. Try incremental sync

Quick Fix:
[Config] → [Test Connection] → [Save] → [Sync]
```

**⚠️ Validation errors na sync**

```
Stappen:
1. Open Work Item Explorer
2. Klik op validation filter buttons
3. Bekijk detail panel voor specifieke issues
4. Fix data in Azure DevOps
5. Re-sync in PO Companion
```

### Performance Problemen

**🐌 Applicatie is traag**

```
Performance Tips:
┌──────────────────────────────────────────────────────┐
│ 1. Clear expanded state                              │
│    [Work Items] → [Clear State]                      │
│                                                      │
│ 2. Gebruik filters                                   │
│    Beperk data met text/validation filters          │
│                                                      │
│ 3. Incremental sync                                  │
│    [↻ Incremental] in plaats van [🔄 Full Sync]     │
│                                                      │
│ 4. Configure Goals                                   │
│    [Settings] → Selecteer specifieke Goals          │
│                                                      │
│ 5. Restart applicatie                               │
│                                                      │
└──────────────────────────────────────────────────────┘
```

**⏱️ Sync duurt te lang**

```
Optimalisatie:
├─ Use incremental sync (sneller)
├─ Verhoog timeout in config
├─ Filter op specific area paths
├─ Configure Goals in settings
└─ Check netwerk snelheid
```

### Data Kwaliteit Problemen

**📊 Velocity metrics zijn niet accuraat**

```
Data Requirements Check:
┌─────────────────────────────────────────────────────┐
│ ✓ Effort ingevuld voor alle PBIs?                   │
│ ✓ Iteration paths correct?                          │
│ ✓ State values consistent (Done/Closed)?            │
│ ✓ Completed items hebben "Done" state?              │
│                                                     │
│ Na correcties: Re-sync work items                   │
└─────────────────────────────────────────────────────┘
```

**🌳 Hiërarchie is niet correct**

```
Fix Hierarchy:
1. Check parent-child links in Azure DevOps
2. Verify work item types (Epic/Feature/PBI)
3. Check voor circular dependencies
4. Re-sync work items in PO Companion
```

---

## Veelgestelde vragen

### Algemeen

**Q: Is mijn data veilig?**

```
A: Ja! ✓ PAT opgeslagen met native secure storage
         ✓ Data blijft lokaal op uw apparaat
         ✓ Geen externe servers
         ✓ Platform-native encryptie

   Windows: Data Protection API (DPAPI)
   macOS:   Keychain
   Android: KeyStore
   iOS:     Keychain
```

**Q: Waar wordt mijn data opgeslagen?**

```
A: Lokaal SQLite database:
   Windows: %LOCALAPPDATA%/PoCompanion/potool.db
   macOS:   ~/Library/Application Support/PoCompanion/potool.db
   Android: App private storage
   iOS:     App private storage
```

**Q: Kan ik offline werken?**

```
A: Gedeeltelijk:
   ✓ View cached work items
   ✓ Use filters and search
   ✓ View cached metrics
   ✗ Sync nieuwe data (needs internet)
   ✗ Refresh PR insights (needs internet)
```

### Work Items

**Q: Waarom zie ik niet alle work items?**

```
A: Mogelijke redenen:
   1. Area Path filter actief
   2. Goals configured in settings
   3. Items niet gesynchroniseerd
   4. Text filter actief

   Fix: Check filters → Clear filters → Re-sync
```

**Q: Hoe fix ik "Parent Progress Issues"?**

```
A: Probleem: Child "In Progress", parent niet
   
   Fix in Azure DevOps:
   ┌────────────────────────────────────────────┐
   │ Voor: Parent (New) → Child (In Progress) ❌│
   │  Na: Parent (In Progress) → Child (In Pr) ✓│
   └────────────────────────────────────────────┘
   
   Dan: Re-sync in PO Companion
```

**Q: Hoe fix ik "Missing Effort Issues"?**

```
A: Probleem: Item "In Progress" zonder effort
   
   Fix in Azure DevOps:
   ┌────────────────────────────────────────────┐
   │ Voor: [PBI] Login (In Progress, Effort: -)❌│
   │  Na: [PBI] Login (In Progress, Effort: 5)✓ │
   └────────────────────────────────────────────┘
   
   Dan: Re-sync in PO Companion
```

### Velocity

**Q: Waarom is mijn velocity 0?**

```
A: Check deze punten:
   ├─ ✓ Work items hebben effort?
   ├─ ✓ Items zijn "Done"/"Closed"?
   ├─ ✓ Iteration paths ingevuld?
   └─ ✓ Data gesynchroniseerd?

   Zie Help pagina voor data requirements
```

**Q: Wat is een goede velocity?**

```
A: Er is geen "goede" absolute velocity!
   
   Belangrijk is:
   ✓ Consistentie (stabiele trend)
   ✓ Voorspelbaarheid
   ✓ Realistic planning
   
   Velocity is relatief per team.
   Gebruik voor planning, niet voor vergelijking tussen teams!
```

### Configuratie

**Q: Hoe maak ik een Personal Access Token?**

```
A: In Azure DevOps:
   
   Stap 1: Profile → Security → Personal Access Tokens
   Stap 2: Click "New Token"
   Stap 3: Configureer:
           ├─ Name: "PO Companion"
           ├─ Expiration: Kies datum
           └─ Scopes: "Work Items (Read)"
   Stap 4: Create → Copy token
   Stap 5: Plak in PO Companion Config
```

**Q: Mijn PAT is verlopen, wat nu?**

```
A: PAT Vernieuwen:
   ┌──────────────────────────────────────────────┐
   │ 1. Azure DevOps → Security → New Token      │
   │ 2. PO Companion → Config → Update PAT field │
   │ 3. Save → Test Connection                   │
   └──────────────────────────────────────────────┘
```

---

## Snelle Referentie

### Workflow Diagram

```
┌─ Typische Workflow ──────────────────────────────────────┐
│                                                          │
│  1. Start PO Companion                                   │
│     │                                                    │
│     ▼                                                    │
│  2. Configureer Azure DevOps verbinding                  │
│     ├─> Organization URL                                │
│     ├─> Project Name                                    │
│     └─> PAT                                             │
│     │                                                    │
│     ▼                                                    │
│  3. Test Connection                                      │
│     │                                                    │
│     ▼                                                    │
│  4. Sync Work Items (eerste keer: Full Sync)            │
│     │                                                    │
│     ▼                                                    │
│  5. Explore Work Items                                   │
│     ├─> Use filters                                     │
│     ├─> Check validations                               │
│     └─> Fix issues in Azure DevOps                      │
│     │                                                    │
│     ▼                                                    │
│  6. Analyse Metrics                                      │
│     ├─> PR Insights voor team improvement               │
│     └─> Velocity voor sprint planning                   │
│     │                                                    │
│     ▼                                                    │
│  7. Dagelijkse gebruik                                   │
│     ├─> Incremental sync 's ochtends                    │
│     ├─> Monitor validations                             │
│     └─> Track metrics                                   │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

### Snelle Troubleshooting Flowchart

```
         Probleem?
            │
     ┌──────┴──────┐
     │             │
Connection    Sync Failed
Failed         │
  │            ├─> Test connection → Fix config
  │            └─> Check area path → Re-sync
  │
  ├─> Check URL format
  ├─> Verify PAT
  └─> Test connection

Data Issues           Performance
  │                      │
  ├─> Check validation   ├─> Clear state
  ├─> Fix in Azure DO    ├─> Use filters
  └─> Re-sync            └─> Incremental sync
```

---

## Ondersteuning & Contact

```
╔═══════════════════════════════════════════════════════════╗
║                   Support Opties                          ║
╠═══════════════════════════════════════════════════════════╣
║  📖 Documentation                                         ║
║     └─> Deze handleiding                                 ║
║     └─> Help pagina in applicatie                        ║
║                                                           ║
║  🐛 Issues Rapporteren                                    ║
║     └─> GitHub Issues voor bugs en feature requests      ║
║                                                           ║
║  💬 Community                                             ║
║     └─> Deel tips en best practices                      ║
╚═══════════════════════════════════════════════════════════╝
```

---

## Appendix: ASCII Art Referentie

### Symbolen Gebruikt

```
Box Drawing:
┌ ┐ └ ┘ ├ ┤ ┬ ┴ ┼ ─ │

Double Line:
═ ║ ╔ ╗ ╚ ╝ ╠ ╣ ╦ ╩ ╬

Pijlen:
▲ ▼ ► ◄ ↑ ↓ → ← ↔

Bullets:
● ○ • ∙ ▪ ▫

Check Marks:
✓ ✗ ✕ ✔ ❌ ⚠️ ℹ️ ✅

Grafiek Blokken:
█ ▓ ▒ ░ ▀ ▄ ▌ ▐

Iconen:
🔄 🔍 ⚙️ 📊 📈 📋 ℹ️ ⚠️ ❌ ✅
```

### Voorbeeld Diagrammen

**Work Item Hiërarchie:**
```
▼ Epic
  ├─ ▼ Feature
  │   ├─ PBI ✓
  │   ├─ PBI ⚠️
  │   └─ Bug ❌
  └─ ▶ Feature
```

**Velocity Grafiek:**
```
   50 │         ▄▄▄
   40 │   ▄▄▄   █ █
   30 │   █ █   █ █
   20 │   █ █   █ █
   10 │   █ █   █ █
    0 └───────────────
       S1   S2   S3
```

**Process Flow:**
```
┌─────┐   ┌─────┐   ┌─────┐
│Start│──>│Work │──>│Done │
└─────┘   └─────┘   └─────┘
```

---

**Einde van de Gebruikershandleiding**

*Versie 1.0 - December 2024*  
*PO Companion - Azure DevOps Management Tool*

Voor de nieuwste updates en informatie, bezoek de project repository.

---

## Snelle Start Checklist

Voor nieuwe gebruikers, volg deze checklist:

```
□ Installeer PO Companion
□ Start de applicatie
□ Ga naar Configuration pagina
□ Vul Organization URL in
□ Vul Project Name in
□ Kies Authentication Mode
□ Vul PAT in (of gebruik NTLM)
□ Test de verbinding
□ Save configuratie
□ Ga naar Work Items pagina
□ Klik op Full Sync
□ Wacht op synchronisatie
□ Explore uw work items!
□ Check validation issues
□ View PR Insights
□ Check Velocity Dashboard
```

**Gefeliciteerd! U bent klaar om PO Companion te gebruiken.**

---

*Deze handleiding is gemaakt voor Nederlandse gebruikers van PO Companion.*  
*Voor vragen of feedback, raadpleeg de support opties hierboven.*
