The key principle is:



MFD input is routed through TacticalMapViewModel; UI-level interaction belongs to the relevant control; application state belongs to RouteManager.



So the agreed structure is:



&#x20;                        USER

&#x20;                          │

&#x20;                MFD buttons / trackball

&#x20;                          │

&#x20;                          ▼

&#x20;               TacticalMapViewModel

&#x20;                          │

&#x20;                   MFD commands

&#x20;                          │

&#x20;                          ▼

&#x20;             ┌───────────────────────┐

&#x20;             │   TacticalMapView            │

&#x20;             │                              │

&#x20;             │ ┌───────────────────┐  │

&#x20;             │ │RouteManagerControl      │ │

&#x20;             │ └───────────────────┘  │

&#x20;             │                              │

&#x20;             │    ┌─────────────┐      │

&#x20;             │    │ RouteEditor     │      │

&#x20;             │    └─────────────┘      │

&#x20;             └───────────────────────┘

&#x20;                   │             │

&#x20;                   │             │

&#x20;                   ▼             ▼

&#x20;             RouteManager       Map

&#x20;                   │

&#x20;            route collection

&#x20;            CurrentRoute

&#x20;            persistence



Responsibilities



**TacticalMapViewModel**



Receives MFD input events.

Determines which Tactical Map function should receive the command.

Does not manage routes itself.

Does not own RouteManagerControl or RouteEditor.



**RouteManagerControl**



UI overlay on the Tactical Map.

Displays the route collection.

Owns UI selection/cursor and scrolling state.

Receives route-management commands from the Tactical Map.

Calls RouteManager when a genuine route operation is required.



**RouteManager**



Owns the route collection.

Owns CurrentRoute.

Performs route selection, addition, deletion and persistence.

Eventually handles assignment of a route to Own Vehicle.

Knows nothing about MFD buttons, scrolling or map presentation.



**TacticalMapView**



Owns the map presentation.

Owns RouteEditor.

Owns the RouteManagerControl.

Responds to RouteManager.CurrentRouteChanged to display the selected route.



**RouteEditor**



Manages editing of the currently displayed route.

Handles trackball/map interaction with the route.

Has no knowledge of RouteManager.

Event directions



We should also retain the distinction between commands going down and state changes coming back:



MFD command

&#x20;   ↓

TacticalMapViewModel

&#x20;   ↓

RouteManagerControl

&#x20;   ↓

RouteManager



versus:



RouteManager

&#x20;   │

&#x20;   ├── CurrentRouteChanged ──→ TacticalMapView

&#x20;   │

&#x20;   └── RoutesChanged ────────→ RouteManagerControl

