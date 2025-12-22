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
