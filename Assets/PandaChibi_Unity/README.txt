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
  Dive_Start      11      0.37 s  OFF         drops into a crouch, leans forward, pushes off (feet leave the floor on frame 8 = 0.23 s,
                                              toes last), arms swing forward and out, stretches into the dive; ends exactly on Dive_Loop's
                                              first pose
  Dive_Loop       20      0.67 s  ON          airborne, head first: body extended forward, arms spread out to the sides (flying-squirrel),
                                              legs trailing, gentle alternating paddle and kick, ears and tail streaming back. No take-off,
                                              impact or landing in it, so it also works for a dive started in mid-air
  Dive_End        47      1.57 s  OFF         belly landing: chest and belly touch on frame 5 (0.13 s) with a little squash, the heavy head
                                              dips, arms bounce; then slides flat on its stomach - arms reaching out with the paws just off
                                              the floor, legs trailing, a slight rock - slowing to a stop by frame ~30 (1.0 s); a last small
                                              nod, then lies completely still on its stomach
  Dive_GetUp      80      2.67 s  OFF         lifts its head, brings the paws in and plants them beside the chest (frame 12), presses with
                                              bent elbows, draws the knees under into a hands-and-knees kneel (frames 18-31), lets go and sits
                                              back over its feet (frames 36-42), crouches, pushes up (head last), two small balance steps;
                                              ends exactly on Idle's first pose
  Explosion_Start 8       0.27 s  OFF         blast from the front: body snaps back, head tips back, arms fling up and out, feet leave the floor
  Explosion_Loop  20      0.67 s  ON          airborne knock-back: torso tilted back, legs lifted forward, light alternating arm flail; no spin
  Explosion_End   47      1.57 s  OFF         lands flat on its back (bottom touches on frame 4, back and hood on frame 8; the heavy head
                                              lags, then settles with a small neck bend), arms and legs bounce out, slides on its back and
                                              slows to a full stop by frame ~36 (1.2 s), then lies still, face up
  Explosion_GetUp 85      2.83 s  OFF         reacts, rolls onto its right side (chest first, head following), plants the right paw and pulls
                                              the knees in, pushes up into a side-sit, shifts its weight over its feet into a crouch, rises,
                                              rebalances with two small steps; ends exactly on Idle's first pose
  Every loop is seamless (first and last frame identical, no velocity pop at the seam).
  The Dash clips keep both feet off the floor the whole time (no take-off, landing or ground contact),
  so they work from the ground and in mid-air; the controller supplies the movement (no root translation).
  Hand-offs inside each action are exact: the last frame of _Start = the first frame of _Loop = the first
  frame of _End (ears and tail included), so Start -> Loop, Loop -> End and Start -> End need no crossfade.
  The same holds for Dive_Start / _Loop / _End and Explosion_Start / _Loop / _End; Dive_End's last frame =
  Dive_GetUp's first frame and Explosion_End's last frame = Explosion_GetUp's first frame.
  Dive_Start and Explosion_Start begin on Idle's first pose; Dive_GetUp and Explosion_GetUp end on it exactly.
  No root motion in any of them: the dive arc, the knock-back path and both slide distances are yours
  (server / controller).

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
       (keeps the small built-in hip lift of the air poses, about 2.5 cm - 6 cm in Dive_Loop, which keeps the
       belly just clear of the root height - so the feet read as off the ground)
     - Root Transform Position (XZ):  Bake Into Pose on, Based Upon: Original (all clips are in place)
       (Original matters for Dive_End / Dive_GetUp and Explosion_End / Explosion_GetUp: "Center of Mass" would
       re-centre the lying body under the root and make it drift)
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
     Dash movement: start the dash speed when Dash_Start begins, hold it during Dash_Loop and ease it off during
     Dash_End (the poses are animated for that: accelerate - sustain - recover).
   DIVE (evasive dive: launch - parabola - belly slide - get-up):
     Idle, Run -> Dive_Start              (trigger "Dive" while grounded; no exit time; transition ~0.05 s)
     Jump_Airborne, Fall -> Dive_Start    (trigger "Dive" while airborne; no exit time; transition ~0.1 s with
                                           Transition Offset 0.65 - it joins at the lift-off, skipping the crouch,
                                           and stretches into the dive). Or go straight to Dive_Loop with ~0.15 s.
     Dive_Start -> Dive_Loop     (exit time 1.0, transition 0 - poses match exactly)
     Dive_Start -> Dive_End      (exit time 1.0, transition 0, condition: already on the ground - a very flat dive;
                                  keep it ABOVE Start -> Loop in the transition list)
     Dive_Loop  -> Dive_End      (belly contact / grounded; no exit time; transition ~0.1 s - any point of the loop).
                                  Dive_End's belly touches on its frame 5 (0.13 s): fire it ~0.1 s before the
                                  controller lands for exact contact, or at contact for a harder impact.
     Dive_End   -> Dive_GetUp    (exit time 1.0 AND the slide is over, e.g. bool "Diving" = false; transition 0 -
                                  poses match exactly). Until then Dive_End holds its last, completely still pose.
     Dive_GetUp -> Idle          (exit time 1.0, transition 0 - exact), or -> Run (exit time ~0.85, transition ~0.15 s)
     Dive movement (controller): the clips never move the root - you drive the whole arc.
       Grounded start: apply the launch velocity (forward + up) on Dive_Start's frame 8 (0.23 s), when the feet leave
       the floor; before that the panda is still crouching and pushing. Mid-air start: keep the current velocity
       and add the dive impulse.
       Flight: plain ballistic parabola (your gravity) while Dive_Loop plays, for as long as it lasts.
       Slide: on belly contact keep the horizontal speed and ease it to zero over ~0.85 s (Dive_End's own slide
       settles by its frame 30, 1.0 s into the clip). For a longer or shorter slide, scale the Dive_End state's speed
       instead of cutting it. Do not move the character during Dive_GetUp - it rises on the spot and finishes
       standing over the root.
       Note: inside Dive_End the lying body travels a further ~0.17 m forward relative to the root during the slide
       (frames 11-30); Dive_GetUp sits back onto its feet by the same amount, which is why it ends exactly over the root.
       While lying the body spans from the root to ~0.95 m in front of it (pelvis ~0.17 m ahead, the head end
       furthest), ~0.7 m wide with the arms out and ~0.7 m tall at the hood - size and offset the prone collider /
       hit box accordingly.
   Mutual exclusion: add NO transition between any Dash_* and Dive_* state, set Interruption Source = None
     on the transitions into Dash_Start / Dive_Start, and only fire the Dash/Dive trigger from code when
     neither action is playing (e.g. a single "isActing" flag cleared when Dash_End or Dive_GetUp finishes). Reset
     the unused trigger on fire so a queued Dive cannot start straight after a Dash (and vice versa).
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
     While lying the pelvis stays within ~0.12 m of the root, but the lying body reaches up to ~0.55 m from it
     (the head end points backward), so size the knocked-down collider / hit box accordingly.

4. MATERIALS - the model imports 10 materials named PandaChibi_*.
   - Face, Hood and Body use the PNGs in Textures/. If they appear untextured, go to the
     Materials tab and press Extract Materials, then assign the matching *_Albedo.png.
   - PandaChibi_Outline is an inverted-hull outline: give it a plain black Unlit material.
     It works with Unity's default back-face culling.
   - For the flat cartoon look of the previews, use any toon/unlit shader on the other materials.

The editable Blender source, previews and reference image are in ../PandaChibi_Blender
(do not copy that folder into Unity).

