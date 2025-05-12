---
uid: generate-window-sprite
---

# Generate window

The **Generate** window enables you to generate a new sprite asset or modify an existing sprite asset with generative models.  

To generate or modify an asset, provide input text prompts and (optional) reference images. You can then import the generated or modified asset into your project's **Asset** folder for use in a game.

Generated assets are saved as `.png` files by default. Unity lets you import `.png`, `.jpg`, and `.exr` files to work with different file formats as needed.

The following table describes the options available on the **Generate** window:

| Option | Description |
| ------ | ----------- |
| **Prompt** | Enter a text description of the sprite you want to create. |
| **Negative prompt** | Enter elements you want to exclude from the sprite. |
| **Model selection** | Select **Change** to choose the AI model used for sprite generation. |
| **Reference options** | Select [**Add More Controls to Prompt**](xref:reference-sprite) to define how reference images influence the generated sprite. |
| **Images** | Set the number of sprites to generate. |
| **Dimensions** | Set the size of the sprite output. |
| **Custom seed** | Set a fixed seed for consistent sprite generation. <br>For more information on custom seed, refer to [Use custom seed to generate consistent sprites](xref:custom-seed).|
| **Generate** | Generate the sprite. |

For more information, refer to [Generate Sprite asset with a prompt](xref:generate-sprite).

## Additional resources

* [Generate sprite with a prompt](xref:generate-sprite)
* [Generate sprite from a reference images](xref:reference-sprite)
* [Manage generated sprites](xref:manage-sprite)