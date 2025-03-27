---
uid: sound-prompt
---

# Generate sound assets with a prompt

Use the **Sound Generator** tool to create custom audio clips from scratch with a text prompt. 

To generate audio from natural language prompts, follow these steps:

1. To open the **Generate New Audio Clip** window, right-click an empty area in the **Project** window.
1. Select **Create** > **Audio** > **Generate Audio Clip**.
1. To choose a model, select **Change** > **Text to Sound**. 

   > [!NOTE]
   > Currently, only one AI model is available for sound generation. After you select it, it will remain the default model for future generations.
   
1. In the **Prompt** field, describe the sound effect you want to generate, such as `jungle ambiance` or `robotic beep`.

1. To exclude specific elements from the generated sound, enter keywords in the **Negative Prompt** field. For example, `no echo`.
1. Use the **Duration** slider to specify the length of the generated audio clip in seconds.

   The model’s reference duration is 10 seconds, which produces the best results.
1. Use the **Count** slider to specify the number of variations of the audio clip to generate in a single request.
1. To specify a custom seed to generate consistent results, enable **Custom Seed** and enter a seed number.
1. Select **Generate**.

The generated audio clip appears in the **Generations** panel. Hover over the audio clip to play it and view details, such as the model used and prompt settings.

## Additional resources

* [Generate sound assets with a sound reference](xref:sound-reference)
* [Record your own sound](xref:sound-record)