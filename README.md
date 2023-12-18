# DSPAssemblerUI
 A Dyson Sphere Program Mod to enhance the Assembler (and Miner) UI Window. 
 
 It shows the production speeds of the produced items below the corresponding output slots in the window, 
 as well as the consumption speed of the input items above the input slots.
 This way, you can plan your factory setup more easily without having to calculate your in- and outputs yourself.

 The speed info for both input and output can be disabled, should you prefer not showing one of them. 
 Or both of them - even though I don't understand why you would install this mod in the first place then...
 
 You can also decide whether you want to display the speed with which the device currently creates or consumes resources, 
 or if you want to display the recipe speed for the device without impact of power and full or empty slots.
 
 Speeds can also be configured to show in an items/s scale instead of items/min, and can be configured to use 0 to 3 decimal places.


## Version History
- v1.0.0 First Version. Shows Production Speeds below Items.
- v1.1.0 Updated to also show input consumption speeds. Added configuration to enable/disable input and output speeds separately.
- v1.2.0 Added a config option to configure whether to always show the full speed of the selected recipe or whether to show the current live speed (affected by power for example)
- v2.0.0 Updated To current Game version (Post May/June Updates), Added Miner UI Speeds (Original idea: tyr0wl), added Option to show speed per second
- v2.1.0 Updated to be compatible with new game version and be more resistant to future updates. Restructured Project files and code.
- v2.2.0 Updated to avoid a rare error when pasting recipes under specific circumstances. Should also ensure more compatibility with Nebula when another player changes the recipe of an open Assembler Window.
- v2.2.1 Updated for compatibility with the Mecha Customization Update due to the changes added to production buffs.
- v2.3.0 Updated to allow for configurable decimal places and adaptive text positioning. The release for this version will be built against the Dark Fog Update. It should also be possible to manually build it against older versions of the game, if needed.
