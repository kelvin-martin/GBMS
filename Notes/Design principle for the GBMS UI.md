Design principle for the GBMS UI components:



* Controls own simple local presentation state when appropriate.
* Avoid introducing a ViewModel merely to wrap a control's local UI behaviour.
* Avoid INotifyPropertyChanged, Avalonia properties, commands, etc. unless they provide a genuine benefit.
* Keep domain/application state in the appropriate domain/service owner — e.g. RouteManager.
* Keep UI navigation state in the control — e.g. RouteManagerControl.\_selectedIndex.
* Use simple event/callback mechanisms where they are sufficient.
* Keep interactions explicit and easy to follow.
* Don't abstract something merely because it could be abstracted.

