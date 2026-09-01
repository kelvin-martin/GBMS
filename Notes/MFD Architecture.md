**1. Overall structure**



┌──────────────────────────────────────────────┐

│                  MainWindow                                │

│                                                            │

│              hosts MfdView                                 │

└──────────────────────┬───────────────────────┘

&#x20;                      │

&#x20;                      ▼

┌──────────────────────────────────────────────┐

│                   MfdView                                  │

│                                                            │

│  GVA buttons       ContentControl                          │

│  F1 ... F12       ┌──────────────────────┐          │

│                   │ Current Content View        │         │

│                   └──────────────────────┘         │

└──────────────┬───────────────────┬───────────┘

&#x20;              │                   │

&#x20;              ▼                   ▼

&#x20;       MfdViewModel        CurrentContentViewModel

&#x20;              │                   │

&#x20;              │                   │

&#x20;              │          ┌────────┴─────────┐

&#x20;              │          │                  │

&#x20;              │          ▼                  ▼

&#x20;              │   Placeholder VM    TacticalMapViewModel

&#x20;              │                             │

&#x20;              │                             │

&#x20;              │                    semantic events

&#x20;              │                             │

&#x20;              │                             ▼

&#x20;              │                     TacticalMapView

&#x20;              │                             │

&#x20;              │                             ▼

&#x20;              │                          Mapsui





**2. MFD navigation**



MfdViewModel is currently the coordinator for the top-level GVA functional areas.



GVA button

&#x20;   │

&#x20;   ▼

MfdView

&#x20;   │

&#x20;   ▼

MfdViewModel.SelectFunctionalArea()

&#x20;   │

&#x20;   ▼

CurrentFunctionalArea

&#x20;   │

&#x20;   ▼

CurrentContentViewModel

&#x20;   │

&#x20;   ▼

ContentControl



For example:



SA button

&#x20;  ↓

MfdFunctionalArea.SA

&#x20;  ↓

TacticalMapViewModel

&#x20;  ↓

DataTemplate

&#x20;  ↓

TacticalMapView



The DataTemplate mechanism is particularly useful here because MfdView doesn't have to contain explicit knowledge such as:



if SA → create TacticalMapView

if WPN → create WeaponView

...





**3. The generic subsystem input interface**



The most important architectural piece we've added is: IMfdInputReceiver

with: HandleFunctionKey(MfdFunctionKey key)



The communication is:



GVA F-key

&#x20;   ↓

MfdView

&#x20;   ↓

MfdViewModel.HandleFunctionKey()

&#x20;   ↓

CurrentContentViewModel

&#x20;   ↓

IMfdInputReceiver.HandleFunctionKey()



The key piece of code is effectively:



if (CurrentContentViewModel is IMfdInputReceiver receiver)

{

&#x20;   receiver.HandleFunctionKey(key);

}



**4. Tactical Map's responsibility**



TacticalMapViewModel is therefore the first concrete example of a subsystem receiving MFD input.



It interprets the generic function key:



F1

F7

F8

F9

F10

F11

F12



and converts it into semantic subsystem actions.



This is an important distinction.



The MFD framework communicates:



"The operator pressed F9."



The Tactical Map communicates internally:



"The operator requested a pan left of this magnitude."



The MFD doesn't need to understand either the meaning of F9 or how a pan is implemented.





**5. ViewModel → View communication**



We deliberately chose simple events rather than introducing another messaging or command framework.



Currently we effectively have:



TacticalMapViewModel

&#x20;       │

&#x20;       ├── CentreMapOnVehicleRequested

&#x20;       │

&#x20;       ├── ZoomLevelChanged

&#x20;       │

&#x20;       └── PanRequested

&#x20;               │

&#x20;               ▼

&#x20;        TacticalMapView



That gives us a clean division:



**TacticalMapViewModel**



Responsible for:



* interpreting F-key inputs
* deciding what operation they represent
* maintaining the selected zoom level
* calculating the pan distance
* supplying vehicle position information
* raising semantic events



**TacticalMapView**



Responsible for:



* Mapsui
* map projection
* map centre
* map extents
* rendering
* translating geographic movement into Mapsui operations



That's a strong separation without excessive architecture.



**6. Mapsui has deliberately remained outside the MFD framework**



This is particularly important.



**7. Mouse/touch input is independent**



Another good characteristic of the current design is that Mapsui's native interaction isn't being routed through our GVA mechanism.



We therefore have two independent input paths:



&#x20;            Operator

&#x20;             /     \\

&#x20;            /       \\

&#x20;       GVA buttons   Mouse / Touch

&#x20;            │           │

&#x20;            ▼           ▼

&#x20;      MfdViewModel    Mapsui

&#x20;            │

&#x20;            ▼

&#x20;     TacticalMapView



**8. Logging is now cross-cutting**



Rather than injecting: ILogger<T> into every class, we now have: GBMS.Services.Logger as the application-wide logging facility.



MfdViewModel

TacticalMapViewModel

TacticalMapView

DataManager

...

&#x20;     │

&#x20;     ▼

&#x20;GBMS.Services.Logger

&#x20;     │

&#x20;     ▼

&#x20;Serilog

&#x20;     │

&#x20;     ▼

&#x20;text file



**10. Current communication model**



Putting everything together:



&#x20;                        ┌───────────────┐

&#x20;                        │  MainWindow   │

&#x20;                        └───────┬───────┘

&#x20;                                │

&#x20;                                ▼

&#x20;                        ┌───────────────┐

&#x20;                        │    MfdView    │

&#x20;                        └───────┬───────┘

&#x20;                                │

&#x20;                        GVA F-key input

&#x20;                                │

&#x20;                                ▼

&#x20;                        ┌───────────────┐

&#x20;                        │ MfdViewModel  │

&#x20;                        └───────┬───────┘

&#x20;                                │

&#x20;                   IMfdInputReceiver

&#x20;                                │

&#x20;                                ▼

&#x20;                 ┌─────────────────────────┐

&#x20;                 │ TacticalMapViewModel    │

&#x20;                 └────────────┬────────────┘

&#x20;                              │

&#x20;                   semantic events

&#x20;                              │

&#x20;                              ▼

&#x20;                 ┌─────────────────────────┐

&#x20;                 │   TacticalMapView       │

&#x20;                 └────────────┬────────────┘

&#x20;                              │

&#x20;                              ▼

&#x20;                           Mapsui



And independently:



DataManager ───────► TablesView

&#x20;    │

&#x20;    └──────────────► Map-related components





