# Proportion Adjuster

Reshapes a VRChat avatar's legs and arms to your own proportions when the avatar is built, so in
full-body tracking the feet meet your real floor, and can shorten or lengthen the neck. The scene
is never changed.

VRChat scales your avatar so its eyes match yours, so only proportions relative to eye height
matter. The hips come down until hip height / eye height matches yours, and the thighs, shins,
upper arms and forearms are resized to match your ratios.

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
   - **Eye height**: floor to your eyes. This only turns the other measurements into
     proportions. The avatar isn't scaled to this height, though shortening its legs does make
     it shorter, because everything above the hips comes down with them. To set your height in
     game, use VRChat's avatar scaling.
   - **Hip height**: floor to the hip joint, the bony bump on the side of your hip.
   - **Knee height**: floor to the knee joint, the gap at the side of your knee.
   - **Shoulder to elbow** and **elbow to wrist**: joint centre to joint centre.

   Type in your measurements.
3. Check the numbers in the inspector, or click **Preview** to see the result in the Scene view.
   The preview shows a temporary copy of the avatar. The avatar itself is only hidden, and the
   preview ends when you select something else, enter play mode or build.
4. If limbs look too thin or too thick, adjust the **Thickness** sliders.
5. To change the neck, set **Neck Length** (1 = as modelled, 0.7 = 30% shorter). The head keeps
   its height: what the neck loses, the spine, chest and upper chest gain in proportion, so the
   eyes and hips stay where they are and the view position does not move. The head itself is not
   scaled, only moved. **Neck Thickness** works like the limb sliders.

The round arrow next to an input takes just that input back to the avatar's own value: its measured
size in cm for the measurements, and 1 for the sliders. The other inputs don't change, and the
inspector warns you if the combination doesn't work. **Reset** in the component's ⋮ menu measures
the avatar again and overwrites all the measurements at once.

## Limits

- Left and right are measured on the left side and get the same changes.
- The humanoid Avatar is rebuilt from the reshaped armature, so animations that place the hips
  use the new proportions. Turn off **Rebuild Humanoid Avatar** if that causes problems.
- Neck length needs a Neck bone in the humanoid mapping, with the Head as its direct child. Bones
  parented to the spine, chest or neck (shoulders, breasts, helpers) move with the longer torso but
  their own skin is not stretched.
