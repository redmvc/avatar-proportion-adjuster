# Proportion Adjuster

Reshapes a VRChat avatar's legs and arms to your own proportions when the avatar is built, so in
full-body tracking the feet meet your real floor. The scene is never changed.

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
   - **Eye height**: floor to your eyes.
   - **Hip height**: floor to the hip joint, the bony bump on the side of your hip.
   - **Knee height**: floor to the knee joint, the gap at the side of your knee.
   - **Shoulder to elbow** and **elbow to wrist**: joint centre to joint centre.

   Type in your measurements.
3. Check the numbers in the inspector, or click **Preview** to see the result in the Scene view.
   The preview shows a temporary copy of the avatar. The avatar itself is only hidden, and the
   preview ends when you select something else, enter play mode or build.
4. If limbs look too thin or too thick, adjust the **Thickness** sliders.

**Reset** in the component's ⋮ menu measures the avatar again, which overwrites your numbers.

## Limits

- Left and right are measured on the left side and get the same changes.
- The humanoid Avatar is rebuilt from the reshaped armature, so animations that place the hips
  use the new proportions. Turn off **Rebuild Humanoid Avatar** if that causes problems.
