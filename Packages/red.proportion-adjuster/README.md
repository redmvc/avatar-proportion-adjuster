# Proportion Adjuster

Reshapes a VRChat avatar's legs, arms and torso to your own measurements when the avatar is built, so in
full-body tracking the feet meet your real floor, and can shorten or lengthen the neck. The scene
is never changed.

Eye height scales the whole armature evenly, head, hands and feet included, until the eyes are at
the height you enter. At that size, the hips, knees and arm segments go straight to their cm values
by stretching lengthwise, and the torso is stretched lengthwise to keep the eyes where they are.
Turn off **Adjust Torso** to let the eyes move with the hips instead.

Bones are never scaled. Joints are moved along the limb and the skin is stretched separately, so
SPS sockets keep working, and length and thickness can be set independently. VRCFury clothing
armatures get the same changes.

## Requirements

- VRChat Avatars SDK 3.7 or later
- [NDMF](https://github.com/bdunderscore/ndmf) 1.8 or later (Modular Avatar includes it)

Without NDMF the component shows an error, and any build of an avatar that has it is stopped.

## Use

1. Drag `Proportion Adjuster.prefab` onto your avatar, or use **Add Component > Proportion
   Adjuster** on the avatar or any object inside it. The fields fill in with the avatar's current
   proportions, so on its own it changes nothing.
2. Measure yourself standing, in cm. All heights are from the floor:
   - **Eye height**: floor to your eyes.
   - **Hip height**: floor to where the avatar's Hips bone should end up. This is the Hips bone
     itself, not the top of the thigh, which usually sits several cm lower.
   - **Knee height**: floor to the knee joint, the gap at the side of your knee.
   - **Shoulder to elbow** and **elbow to wrist**: joint centre to joint centre.

   Type in your measurements.
3. The inspector lists the final measurements. Check them, or click **Preview** to see the result in the Scene view.
   The preview shows a temporary copy of the avatar. The avatar itself is only hidden, and the
   preview ends when you select something else, enter play mode or build.
4. If limbs look too thin or too thick, adjust the **Thickness** sliders. The torso has its own
   **Torso thickness** sliders, one per part, shown for the parts the avatar has:
   - **Lower Belly**: the belly from the Hips bone up to the Spine bone, clothing included. It fades
     out from the Hips bone down to the top of the thighs, so the pelvis, genitals and legs stay as
     they are. It's unavailable if the Spine bone isn't a direct child of the Hips, or isn't above them.
   - **Spine**, **Chest** and **Upper Chest**: the skin on that bone. Whichever one holds the shoulders
     also moves them out, so the arms stay attached and the shoulders widen.

   Each slider acts on its bone's share of the skin, so skin shared with other bones grows by less
   than the number, and skin on shoulder and chest helper bones doesn't grow at all.
5. To change the neck, set **Neck Length** (1 = as modelled, 0.7 = 30% shorter). The head keeps
   its height: what the neck loses, the spine, chest and upper chest gain in proportion, so the
   eyes and hips stay where they are and the view position does not move. The head itself is not
   scaled, only moved. **Neck Thickness** works like the limb sliders.

The round arrow next to an input takes just that input back to the avatar's own value: its measured
size in cm for the measurements, and 1 for the sliders. The other inputs don't change, and the
inspector warns you if the combination doesn't work. **Reset** in the component's ⋮ menu measures
the avatar again and overwrites all the measurements at once.

The **Torso stretch** sliders decide how the torso's stretch is divided between the spine, chest and
upper chest, for the ones the avatar has. At 1 they're all stretched by the same factor; higher takes
more, and 0 keeps that bone's length. **Hips Stretch** adds the gap from the Hips bone to the Spine
bone to the mix. It starts at 0, and it only moves the spine up: the pelvis and legs stay where they
are, and the belly stretches through its existing weights instead of scaled skin. It's unavailable if
the Spine bone isn't a direct child of the Hips, or isn't above them. The total stays what the eye height needs, so the
eyes don't move. The inspector shows what each part gets in cm.

The thickness and neck sliders are factors applied on top of the overall scale, so a neck at 1 is
the modelled neck at the new size. The build logs the final
measurements it produced.

## Limits

- Left and right are measured on the left side and get the same changes.
- The humanoid Avatar is rebuilt from the reshaped armature, so animations that place the hips
  use the new proportions. Turn off **Rebuild Humanoid Avatar** if that causes problems.
- Neck length needs a Neck bone in the humanoid mapping, with the Head as its direct child.
- Bones parented to the spine, chest or neck (shoulders, breasts, helpers) move with the torso but
  their own skin is not stretched, which can show when the torso changes a lot.
- Lower Belly changes copies of the meshes at build time. Objects parented rigidly to the hips don't
  move out with it, and in play mode it skips meshes imported without Read/Write. Uploads aren't affected
  by that.
