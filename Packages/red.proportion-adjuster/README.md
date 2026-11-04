# Proportion Adjuster

Reshapes a VRChat avatar's legs, arms and torso to your own measurements when the avatar is built, so in
full-body tracking the feet meet your real floor, and can shorten or lengthen the neck. The scene
is never changed.

The hips, knees and arm segments go straight to the cm values you enter, and the torso is stretched
lengthwise until the eyes reach theirs. Nothing gets thicker because of this, and the head, hands
and feet keep their size. The avatar's own scale isn't changed, so a big difference from its
modelled size means a lot of stretching: check it in the preview. Turn off **Adjust Torso** to let
the eyes just move with the hips.

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
4. If limbs look too thin or too thick, adjust the **Thickness** sliders.
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
more, and 0 keeps that bone's length. The total stays what the eye height needs, so the eyes don't
move. The inspector shows what each bone gets in cm.

The thickness and neck sliders are factors of the avatar as modelled. The build logs the final
measurements it produced.

## Limits

- Left and right are measured on the left side and get the same changes.
- The humanoid Avatar is rebuilt from the reshaped armature, so animations that place the hips
  use the new proportions. Turn off **Rebuild Humanoid Avatar** if that causes problems.
- Neck length needs a Neck bone in the humanoid mapping, with the Head as its direct child.
- Bones parented to the spine, chest or neck (shoulders, breasts, helpers) move with the torso but
  their own skin is not stretched, which can show when the torso changes a lot.
