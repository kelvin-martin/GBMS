**MFD Design Principles:**

**======================**



**Screen Layout Geography**

The standard GVA display is typically a fixed size screen screen surrounded by physical tactile bezel buttons (soft keys). The screen real estate is strictly partitioned:



1 **The Content Area (Center)**: This is where the active functional application (like the map or weapon feed) is rendered. It must maintain a standard aspect ratio (usually 4:3 or 16:10).



**The Menu Bars (Periphery)**: The edges of the screen align directly with the physical bezel keys.



**Horizontal Rows (Top/Bottom):** Generally used for primary navigation toggles between the 8 functional areas (SA, WPN, DEF, SYS, DRV, STR, COM, BMS).



**Vertical Columns (Left/Right)**: Dynamically change based on the active application to control sub-functions (e.g., zooming a map or selecting a target).



**2. The GVA Alarm and Color Hierarchy**



Military HMI standards strictly dictate color usage to prevent cognitive overload. Do not use random colors for UI styling. Implement this specific color matrix for all UI text, borders, and indicators:



**Red (Flash/Solid):** Category 1 Warning. Immediate threat to life or vehicle survival (e.g., incoming missile lock, engine fire). Accompanied by an audible tone.



**Amber/Yellow:** Category 2 Caution. System malfunction or degradation requiring rapid crew attention (e.g., low fuel, sensor communication failure).



**Green:** Normal Operation. Indicates an active, fully functional system or safe state (e.g., weapon safed, system online).



**White/Light Grey:** Status Information. Passive data displays, secondary labels, or unselected menu text.



**Cyan/Blue:** Generally reserved for specific Blue Force Tracking (BFT) elements on the BMS screen to denote friendly forces.



**Black/Dark Charcoal:** The mandatory background color to preserve the crew's night vision.



**3. The Physical Outer Bezel** (Hardware Controls)



**Top Row**: Dedicated to the 8 GVA Functional Areas. SA, WPN, DEF. SYS, DRV, STR, COM, BMS



**Side Columns (F1–F6 \& F7–F12):** Dynamic Sub-Menu Keys. Labels are drawn on the screen right next to the physical buttons. 



**Bottom Row (F13–F20)**: Assigned to global hardware utilities that never change, regardless of what application is active (e.g., NUC - Non-Uniformity Correction for thermal cameras, PIP - Picture in Picture, PWR - Display Power control).



**4. The Screen Framework (OS \& System Layer)**



**Header Bar:** Reserved for system-critical telemetry that must always remain visible, such as internal computer temperatures, active vehicle network status, system time, and vehicle power rails.



**Footer Alarm Area:** A high-priority banner reserved exclusively for **Category 1 Warnings (Red)** and **Category 2 Cautions (Amber)**. If an engine fire or laser-lock occurs, the central application continues to run, but this footer flashes violently to capture the operator's attention.



**5. The Central Content Canvas (Application Space)**



* This represents the simulation's dynamic view port.
* When a user presses a top bezel key, you rotate this screen segment. For example, triggering (WPN) swaps out a 2D rendering canvas (BMS map) and instantiates a Weaposn Page.





Uploaded is an example of a GVA MFD:



