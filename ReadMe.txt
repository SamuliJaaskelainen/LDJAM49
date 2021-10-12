Fractured Edge
Created for Ludum Dare 49

Game development: Samuli Jääskeläinen @Zhamul
Graphics programming: Petteri Timonen
Music and sounds: Leo Krechmer

The game can be played on a real oscilloscope or with an emulator bundled with the executable.

If you wish to play the game on an oscilloscope, please set game audio to Windows default audio device and oscilloscope audio to Windows default communication device. You can also use command line arguments -scaleX [float] and -scaleY [float] to scale the rendering. Default arguments are -2, -2. So if you want to flip x for example open game with command: "Fractured Edge.exe -scaleX 2"

If your image on the oscilloscope is very smooth, you can check the DAC driver we use from here: https://github.com/tikonen/DACDriver

If you wish to make something like this game, you can check our Unity plugin for oscilloscopes: https://github.com/ptimonen/AudioRenderUnity

Or check the full source code of this game from: https://github.com/SamuliJaaskelainen/LDJAM49

The game window must be in focus to play. Click it.

Controls:
W A S D Ctrl Space: Move
Q E Mouse: Rotate
L Mouse: Shoot
R Mouse: Rocket (needs to be unlocked)
SHIFT: Boost (needs to be unlocked)
F: Tractor beam (needs to be unlocked)
TAB: Map
F1/F2: Change mouse sensitivity
F3/F4: Change field of view
ESC: Defocus game
K: Take damage (debug)
U: Hide UI (debug)

Hints:
You are shown in the map as blinking point
New equipment will unlock new actions
You will automatically heal after 10 seconds of safety
Game rendering becomes more unstable when you are damaged
When you die, you do not lose progress
Hold SHIFT for turbo speed
Throwing boxes does high damage and breaks doors
Hold R. MOUSE to lock targets for rockets

Changelog

1.0.
Initial release

1.1.
Rebuild audio render dll as release.
Enable window resizing.

1.2.
Add oscilloscope scale command line arguments.

