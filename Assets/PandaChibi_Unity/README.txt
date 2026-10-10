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
  Run             18      0.60 s  ON          long, bouncy strides; planted feet travel 0.41 m/s (match move speed or scale the clip)
  Jump_Start      10      0.33 s  OFF         crouch -> push -> feet leave the ground at ~0.23 s; ends on Jump_Airborne's first pose
  Jump_Airborne   20      0.67 s  ON          compact tucked pose with gentle float; loops for any air time
  Fall            20      0.67 s  ON          legs reaching down, arms out, ears/tail lifted by the air; loops until grounded
  Land            15      0.50 s  OFF         starts from the Fall pose, contact on its 2nd frame, squash, ends on Idle's first pose
  Dash_Start      5       0.17 s  OFF         quick lean-in: arms whip back, legs kick back; ends exactly on Dash_Loop's first pose
  Dash_Loop       12      0.40 s  ON          sustained dash: low forward lean, arms swept back, legs trailing, light flutter
  Dash_End        10      0.33 s  OFF         controlled recovery: body straightens, arms come forward, ends near Idle's first pose
  Dive_Start      7       0.23 s  OFF         evasive tuck, then stretch into the dive; ends exactly on Dive_Loop's first pose
  Dive_Loop       16      0.53 s  ON          sustained dive: stretched low lunge, paws reaching forward-down, legs streaming back
  Dive_End        10      0.33 s  OFF         recovery: pulls out of the stretch, arms and legs come under, ends near Idle's first pose
  Explosion_Start 8       0.27 s  OFF         blast from the front: body snaps back, head tips back, arms fling up and out, feet leave the floor
  Explosion_Loop  20      0.67 s  ON          airborne knock-back: torso tilted back, legs lifted forward, light alternating arm flail; no spin
  Explosion_End   47      1.57 s  OFF         lands bottom-first on its 4th frame, rolls onto its back/side and slides; legs and arms drag,
                                              slows to a full stop by frame ~36 (1.2 s), then holds a still lying pose
  Explosion_GetUp 54      1.80 s  OFF         rolls onto its side, pushes up on the right paw, sits, rocks over onto the left paw, gets
                                              its feet under, stands with a wobble and two little steps; ends exactly on Idle's first pose
  Every loop is seamless (first and last frame identical, no velocity pop at the seam).
  The Dash and Dive clips keep both feet off the floor the whole time (no take-off, landing or ground contact),
  so they work from the ground and in mid-air; the controller supplies the movement (no root translation).
  Hand-offs inside each action are exact: the last frame of _Start = the first frame of _Loop = the first
  frame of _End (ears and tail included), so Start -> Loop, Loop -> End and Start -> End need no crossfade.
  The same holds for Explosion_Start / _Loop / _End, and Explosion_End's last frame = Explosion_GetUp's first frame.
  Explosion_Start begins on Idle's first pose and Explosion_GetUp ends on it exactly. No root motion in any of them:
  the knock-back path and the slide distance are yours (server / controller).

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
     - Root Transform Position (XZ):  Bake Into Pose on, Based Upon: Original (all clips are in place)
       (Original matters for Explosion_End / Explosion_GetUp: "Center of Mass" would re-centre the lying body
       under the root and make it drift)
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
   DASH / DIVE (one action at a time) - put each in its own sub-state machine, "Dash" and "Dive":
     Idle, Run, Jump_Airborne, Fall -> Dash_Start   (trigger "Dash"; no exit time; transition ~0.08 s)
     Dash_Start -> Dash_Loop     (exit time 1.0, transition 0 - poses match exactly)
     Dash_Start -> Dash_End      (exit time 1.0, transition 0, condition: dash already over - for very short dashes;
                                  keep it ABOVE Start -> Loop in the transition list so it is checked first)
     Dash_Loop  -> Dash_End      (dash over, e.g. bool "Dashing" = false; no exit time; transition ~0.1 s,
                                  so it can leave at any point of the loop - at the loop's end it is exact)
     Dash_End   -> Idle / Run    (grounded; exit time ~0.85, transition ~0.15 s)
     Dash_End   -> Jump_Airborne / Fall  (airborne; exit time ~0.8, transition ~0.2 s)
     Dive_Start / Dive_Loop / Dive_End: the same, with trigger "Dive" and bool "Diving".
     Movement: start the dash/dive speed when _Start begins, hold it during _Loop and ease it off during _End
     (the poses are animated for that: accelerate - sustain - recover).
     Mutual exclusion: add NO transition between any Dash_* and Dive_* state, set Interruption Source = None
     on the transitions into Dash_Start / Dive_Start, and only fire the Dash/Dive trigger from code when
     neither action is playing (e.g. a single "isActing" flag cleared when _End finishes). Reset the unused
     trigger on fire so a queued Dive cannot start straight after a Dash (and vice versa).
   EXPLOSION KNOCK-BACK - put the four states in a sub-state machine "Explosion":
     Any State -> Explosion_Start   (trigger "Explode"; no exit time; transition 0-0.05 s; Can Transition To Self off)
                                    (Any State, so it also interrupts Run, jumps, Dash and Dive - standing, running or airborne)
     Explosion_Start -> Explosion_End   (exit time 1.0, transition 0, condition: already grounded - very short knock-back;
                                         keep it ABOVE Start -> Loop in the transition list)
     Explosion_Start -> Explosion_Loop  (exit time 1.0, transition 0 - poses match exactly)
     Explosion_Loop  -> Explosion_End   (grounded; no exit time; transition ~0.1 s - any point of the loop)
     Explosion_End   -> Explosion_GetUp (exit time 1.0 AND the knock-back is over, e.g. bool "KnockedBack" = false;
                                         transition 0 - poses match exactly). Until then End simply holds its last,
                                         completely still lying pose, so a longer slide or a wait on the floor is fine.
     Explosion_GetUp -> Idle            (exit time 1.0, transition 0 - exact), or -> Run (exit time ~0.85, transition ~0.15 s)
     Timing inside Explosion_End: the bottom touches on frame 4 (0.1 s), back and hood on frame 8, slide frames 8-36,
     a last little rock and still from frame ~43. Fire End ~0.1 s before the controller lands for exact contact.
     Movement (server): apply the knock-back velocity when Explosion_Start begins; let it carry through the loop; after
     touchdown slide the character backward and slow it to zero over ~1.0 s (the clip's slide eases out by frame 36).
     For a different slide time, scale the End state's speed (speed = 1.0 s / your slide time) rather than cutting it.
     Do not move the character during Explosion_GetUp - it gets up on the spot and finishes standing over the root.
     While lying, the body sits up to ~0.35 m behind / to the right of the root (in-pose offset; GetUp brings it back),
     so size the knocked-down collider / hit box accordingly.

4. MATERIALS - the model imports 10 materials named PandaChibi_*.
   - Face, Hood and Body use the PNGs in Textures/. If they appear untextured, go to the
     Materials tab and press Extract Materials, then assign the matching *_Albedo.png.
   - PandaChibi_Outline is an inverted-hull outline: give it a plain black Unlit material.
     It works with Unity's default back-face culling.
   - For the flat cartoon look of the previews, use any toon/unlit shader on the other materials.

The editable Blender source, previews and reference image are in ../PandaChibi_Blender
(do not copy that folder into Unity).

