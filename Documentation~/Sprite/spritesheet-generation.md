---
uid: spritesheet-generation
---

# Generate a spritesheet

Generate an animation-ready spritesheet from an artificial intelligence (AI)-generated image. Sprite Generator creates a short animation video from your image, samples evenly spaced frames, and arranges them into a 4×4 grid (16 frames total).

The generated animation video defaults to 5 seconds.

A **spritesheet** is a single texture that contains multiple animation frames arranged in a grid. You can use the spritesheet to create 2D animations. You can also generate [spritesheets directly from Unity Assistant](xref:spritesheet-assistant).

A single image doesn’t contain enough visual information for animation. For example, the back of an object isn’t visible in a front-facing image. To solve this, the Sprite Generator first creates a short turntable-style video, then extracts animation frames from that video.

To generate a spritesheet, follow these steps:

1. [Generate a source image.](#generate-the-source-image)
2. [Generate the spritesheet from the source image.](#generate-the-spritesheet)

## Generate the source image

   1. In the Unity Editor, select **AI** > **Generate New** > **Sprite**.
      The Sprite Generator opens.
   2. Select the **Generate** tab.
   3. Enter a prompt that describes your object or character.
   4. Select **Generate**.
   5. In the **Remove BG** tab, review the **Base Image**, and then select **Remove BG** to remove the background.

## Generate the spritesheet

   1. Select the **Spritesheet** tab.
   2. In the **Prompt** field, specify a motion type. For example: `Turntable`.
   3. Provide a reference image that acts as the first frame of the generated spritesheet.
   4. Select **Spritesheet**.

   The generator creates the following:
   - A 5 s animation video labeled in the Generations panel.
   - A corresponding spritesheet in the **Inspector** window.
   - The video is saved as an `.mp4` file in your project’s **GeneratedAssets** folder.

To use the generated spritesheet, select the spritesheet in the **Project** window and drop it into the **Scene**. When prompted, create and save an animation clip (`.anim`).

If your animation contains a background, open the Sprite Generator again and use the **Remove BG** tab to remove the background.

## Additional resources

* [Generate sprite asset with a prompt](xref:generate-sprite)
* [Manage generated sprites](xref:manage-sprite)