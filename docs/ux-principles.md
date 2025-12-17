## UX Principles

The application uses a fixed and predictable layout with a clear separation between navigation and content.
A permanent left-side navigation menu provides access to all main views.
The right side is the primary workspace and always displays the currently selected view.
Navigation never replaces the layout.

The UX is minimal, clean, and functional.
Components must appear finished and deliberate, never raw or technical.
Visual noise is actively reduced.
Interactive elements are used only when they add clarity, speed, or decision support.

All views follow the same structural pattern:
1. A top header providing context
2. A primary interaction area
3. Optional supporting information below

Navigation always flows from overview to detail.
Overviews use:
- interactive charts
- sortable and filterable datatables
- hierarchical treeviews

Selecting an item opens a detail view as a right-side panel or modal overlay.
Detail views MUST NOT trigger full-page navigation.
Returning to an overview MUST preserve filters, selections, and scroll position.

Charts MUST be clickable and support filtering.
Charts MUST be implemented using approved open-source Blazor components.
JavaScript-based charting libraries are not allowed.

Treeviews represent hierarchical structures such as Epics → Features → Stories → Bugs.
Datatables present larger datasets and support:
- column selection
- sorting
- filtering
- context-sensitive inline actions

Inline actions appear only when relevant to the selected item.

The interface must scale gracefully as new views are added to the left navigation.
All screens share the same interaction patterns, layout structure, and visual language.
The system must feel unified:
- one component set
- one navigation model
- one approach to detail exploration

Simplicity, predictability, and reduced cognitive load guide all UX decisions.
