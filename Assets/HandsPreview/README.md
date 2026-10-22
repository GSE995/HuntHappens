# Cartoon hands — Iteration02

Open `Assets/HandsPreview/HandsPreview.unity` and press Play. The Game view has seven animation buttons, pause, a timeline, speed and Front / Angle / Back views. This is an isolated preview; the existing ForestPrototype scene and gameplay arms are unchanged.

The FBX imports as Generic and contains Hands_Idle, Hands_Grab, Hands_Hold, Hands_Throw, Hands_Weapon, Hands_Shoot and Hands_Trap. The peach material uses URP/Lit. Import and playback were verified in a separate Unity 6000.6.6f1 / URP 17.6.0 project using these same assets.

Blender sources, generation scripts, renders and quality reports are in `Art/FirstPersonHands/Iteration01` and `Art/FirstPersonHands/Iteration02`, outside Assets. The latest Iteration01 retains all approved finger-gap edits. Iteration02 adds the 37-bone skeleton and seven Actions without rebuilding the hand geometry.

These are prototype assets: the FBX has about 87k triangles across both hands, and weapon/hold poses still require fitting to the actual game objects. Gameplay integration is a separate step.
