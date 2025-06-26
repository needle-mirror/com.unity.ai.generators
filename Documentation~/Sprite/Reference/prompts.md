---
uid: prompts
---

# Prompt guidelines for asset generation

Use this guidance to write effective prompts that improve asset generation results across different Generator tools.

When you generate assets with Generators, the prompt you provide is important in the quality of the output. Generators transform text prompts into sprites, textures, materials, animations, sounds, and terrain layers.

To write good prompts, you need clarity, detail, and structure. This page explains general writing guidelines for all Generators and also includes Generator-specific advice. It also highlights common mistakes to avoid.

## General guidelines

The following guidelines apply across all Generator tools.

### Use natural language

Write prompts as clear descriptive sentences or phrases. Natural language helps Generators understand the context and relationships between elements.

For example:

* Instead of: `Blue dragon, fire, wings, dark sky`
* Use: `Create a blue dragon with large wings, breathing fire against a dark sky backdrop.`

### Avoid double negatives

Double negatives can confuse Generators and lead to undesired results. Be direct and positive in your phrases. In the **Negative Prompt** field, don't use `no` or `not.

For example:

* Instead of: `No birds`
* Use: `birds`

For more information on how to use a negative prompt, refer to [Remove unwanted elements with negative prompts](xref:negative-prompt).

### Be specific

If you add details, it provides the Generators with more control over the generated output. Include information about the subject matter, style, color, lighting, perspective, and atmosphere.

For example:

`A futuristic city skyline at night with glowing neon lights and flying cars in the style of cyberpunk.`

### Specify the frame and composition

Define the layout, perspective, scale, or focus of your scene to ensure the Generators place the objects accurately.

For example:

`Create a close-up view of a medieval knight standing in the center of the frame, with a castle visible in the background.`

### Break down complex requests

When you need complex designs, break the request into smaller descriptive parts to clarify intent.

For example:

`Design a futuristic cityscape at night. Include tall skyscrapers with neon lights, flying vehicles, and a glowing moon in the sky.`

## Generator-specific guidelines

Different Generators support additional prompt details. Use these specific guidelines depending on the asset type you want to generate.

> [!NOTE]
> For Sound Generator, Animation Generator, Texture Generator, and Material Generator, short prompts of three to four descriptive keywords often produce the best results. Longer sentences might not improve the generation quality for these generators. Sprite Generator and Terrain Layer Generator generally support more detailed natural language prompts.

### Sound Generator

Clarify sound type and intended mood.

   * Indicate type (ambient, sound effect, or music).
   * Include mood or emotional tone.
   * Example: `Gentle forest ambiance with birds chirping, distant stream flowing, and soft wind in trees.`

### Animation Generator

Describe the motion and context for the animation.

   * Include subject, movement type, and pace.
   * Specify action context (attack, idle, walk, or cycle).
   * Example: `A humanoid robot performing a smooth walk with fast arm swings.`

### Texture Generator

Describe surface appearance and intended usage.

   * Include surface properties (roughness, glossiness, or patterns).
   * State the surface type (floor, wall, or object surface).
   * Example: `Create a seamless wooden floor texture with a polished finish, featuring natural wood grain patterns.`

### Material Generator

Explain the physical material properties that define the surface look.

Example: `Polished marble surface with subtle veins in white and grey, glossy finish.`

### Sprite Generator

Provide visual details and style preferences for character sprites or objects.

   * Specify character traits (age, gender, clothing, or pose).
   * Define the art style (pixel art, hand-drawn, or 3D style).
   * Example: `Design a sprite of a young female warrior with braided hair, wearing leather armor and holding a glowing sword. Use a pixel-art style.`

### Terrain Layer Generator

Provide terrain types and textures.

Example: `Generate a grass terrain layer on sand dunes.`

## Example of effective prompts

Here are examples that demonstrate how prompt clarity affects the results.

**Good prompt**: `Create a sprite of a medieval archer with a bow drawn, wearing a green cloak. The background should be transparent, and the art style should be pixel art.`

**Bad prompt**: `Medieval archer green bow pixel.`

## Common mistakes to avoid

Before you write your final prompt, avoid these common issues:

   * **Vague language**: Avoid general terms such as `cool` or `nice`. Be specific about what you want.
   * **Overloading the prompt**: Avoid cramming too many unrelated ideas into a single prompt. Break it into multiple requests if needed.
   * **Ignoring context**: Failure to provide context leads to misinterpretation. Always explain the purpose of the asset you want to generate.

## Additional resources

* [Generate sprite with a prompt](xref:generate-sprite)
* [Generate Texture2D asset with a prompt](xref:generate-texture2d)
* [Generate sound asset with a prompt](xref:sound-prompt)
* [Generate material with a prompt](xref:material-generate-prompt)