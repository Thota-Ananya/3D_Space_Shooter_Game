3D SPACE SHOOTER - Unity project
=================================

OPTION A - open this folder directly
1. Unzip SpaceShooter3D.zip.
2. Unity Hub > Projects > Add (or "Add project from disk") > pick the SpaceShooter3D folder.
3. Open it with any Unity 2022.3 LTS or Unity 6 editor (accept the upgrade prompt if asked).
4. Press Play. The game builds itself in the open scene.

OPTION B - drop into your own project
1. Unity Hub > New project > "3D (Core)" or "Universal 3D".
2. Copy Assets/Scripts/SpaceGame.cs into the new project's Assets folder.
3. Press Play.

CONTROLS
  WASD / Arrow keys ... move
  Space / Left mouse .. fire twin lasers
  P ................... pause
  R ................... restart after game over

GAMEPLAY
  - Shoot asteroids (bigger = more HP, more points) and red enemy fighters (50 pts) that track and shoot at you.
  - 3 lives, brief invulnerability after each hit. Difficulty ramps up over time.
  - High score is saved between sessions.

To build a playable .exe: File > Build Settings > Add Open Scenes (save the scene first) > Build.
