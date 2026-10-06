PANDA CHIBI - UNITY PACKAGE
===========================
Drag this whole "PandaChibi_Unity" folder into your Unity project's Assets folder.

CONTENTS
  Models/PandaChibi.fbx                    Character: mesh + skeleton, rest pose, no animation
  Animations/PandaChibi@<Clip>.fbx         One file per clip (skeleton + that clip only)
  Textures/PandaChibi_*_Albedo.png         Colour maps for the Face, Hood and Body materials

CLIPS (30 fps, all in place - move the character from script / CharacterController)
  Clip            Frames  Length  Loop Time   Notes
  Idle            120     4.00 s  ON          layered breathing / sway / look, so the repeat is hard to spot; feet locked
  Run             16      0.53 s  ON          feet travel ~0.28 m/s (match move speed to avoid sliding)
  Jump_Start      10      0.33 s  OFF         crouch -> push -> feet leave the ground at ~0.23 s; ends on Jump_Airborne's first pose
  Jump_Airborne   20      0.67 s  ON          compact tucked pose with gentle float; loops for any air time
  Fall            20      0.67 s  ON          legs reaching down, arms out, ears/tail lifted by the air; loops until grounded
  Land            15      0.50 s  OFF         starts from the Fall pose, contact on its 2nd frame, squash, ends on Idle's first pose
  Every loop is seamless (first and last frame identical, no velocity pop at the seam).

EARS AND TAIL
  Ear_L, Ear_R and Tail carry baked secondary motion in every clip (bounce and lag from the body,
  float when airborne, lift in the fall, flop on landing). Humanoid clips only keep these extra bones
  if they are in the clip's mask - see step 2.

CHARACTER FACTS
  Height 1.0 m (import scale 1). 22 bones with Unity Humanoid names
  (Hips, Spine, Chest, Neck, Head, Left/Right Shoulder, UpperArm, LowerArm, Hand,
  UpperLeg, LowerLeg, Foot) plus extra bones Ear_L, Ear_R, Tail.
  About 41.5k vertices (includes the black ink-outline shell). Max 3 bone weights per vertex.

SETUP IN UNITY
1. MODEL - select Models/PandaChibi.fbx
   Rig tab: Animation Type = Humanoid, Avatar Definition = Create From This Model -> Apply.
   Press Configure: every bone maps automatically. The rest pose has the arms down; if Unity
   warns that the character is not in T-pose, choose Pose > Enforce T-Pose, then Done.

2. ANIMATIONS - select all files in Animations/
   Rig tab: Animation Type = Humanoid, Avatar Definition = Copy From Other Avatar,
   Source = PandaChibiAvatar (created in step 1) -> Apply.
   Animation tab, for each clip:
     - Loop Time: as in the table above (Loop Pose on for the looping clips)
     - Root Transform Rotation:       Bake Into Pose on (Based Upon: Original)
     - Root Transform Position (Y):   Bake Into Pose on (Based Upon: Original)
       (keeps the small built-in hip lift of the air poses, about 2.5 cm, so the feet read as off the ground)
     - Root Transform Position (XZ):  Bake Into Pose on (all clips are in place)
     - Ears and tail: Mask > Definition = Create From This Model > Transform,
       tick Ear_L, Ear_R and Tail.
   Apply.

3. ANIMATOR - suggested state machine:
     Idle <-> Run                     (by speed; transition ~0.1 s)
     Idle/Run -> Jump_Start           (jump pressed; transition ~0.05 s)
     Jump_Start -> Jump_Airborne      (exit time 1.0, transition 0 - poses match exactly)
     Jump_Airborne -> Fall            (vertical velocity < 0; transition ~0.2 s)
     Fall -> Land                     (grounded; transition ~0.05 s, any point of the Fall loop)
     Land -> Idle / Run               (exit time ~0.8, or interrupt early when moving)
   Walking off a ledge can go straight Idle/Run -> Fall (transition ~0.2 s).

4. MATERIALS - the model imports 10 materials named PandaChibi_*.
   - Face, Hood and Body use the PNGs in Textures/. If they appear untextured, go to the
     Materials tab and press Extract Materials, then assign the matching *_Albedo.png.
   - PandaChibi_Outline is an inverted-hull outline: give it a plain black Unlit material.
     It works with Unity's default back-face culling.
   - For the flat cartoon look of the previews, use any toon/unlit shader on the other materials.

The editable Blender source, previews and reference image are in ../PandaChibi_Blender
(do not copy that folder into Unity).
