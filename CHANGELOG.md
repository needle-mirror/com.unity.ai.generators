# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [1.6.0-pre.1] - 2026-02-09

### Changed

- Improved responsiveness of spritesheet generation UI.

### Fixed

- Fixed generated mesh preview lighting under HDRP.

## [1.5.0-pre.2] - 2026-01-15

### Changed

- Sprite generator no longer shows material models. Texture2D and Material generators still shows material models.

### Fixed

- When multiple generator windows are open on different assets, model selection no longer unexpectedly affects multiple windows at once.

## [1.5.0-pre.1] - 2026-01-05

### Added

- Generation workflows integrated into AI Assistant 
- Added spritesheet generation
- Added animation stationary and loop modes
- Added skybox material creation from cubemap
- Added audio clip editing tool for trimming and modifying sounds

## [1.0.0-pre.20] - 2025-08-27

### Added

- Expanded image generation capabilities to include BMP, GIF, TIFF, PSD, and HDR, in addition to existing support for PNG, JPG, and EXR.
- Added pattern search button and window to material generator.
- Added descriptive shader input label names to generated map material assignments.
- Added simple heuristic for message log feedback when a generation was likely filtered by a content filter.

### Changed

- Allow partial completion and recovery of generated assets.
- Improve image preview quality with multi-step blitting.
- When no image model selected, automatically select favorited model, typically Flux 1.dev.

### Fixed

- Generation download recovery waits until connection is made on startup.
- Searching generated assets is faster.
- Remove alpha when sending image references as models do not look at alpha channel.
- Fixed filters in model selector applying instantly to generator window.
- Fixed UI offset in Generation Data window.
- Fixed missing map assignments when promoting between generated material and terrain layer.

## [1.0.0-pre.19] - 2025-08-14

### Added

- Added Asset Reference field in Generation Data for sprite generators.

### Fixed

- Fixed badge visibility on non-upscaled generations.
- Fixed Asset Reference not being cleared correctly in Image references.

## [1.0.0-pre.18] - 2025-08-12

### Added

- Added rounded corners to progress spinners.

### Changed

- Changed text to motion to always orient generated humanoid animation forward.

### Fixed

- Fixed potential null reference when docking and undocking generator panels on Mac.
- Fixed potential render texture cache corruption.
- Fixed ime composition support in prompt fields.
- Fixed some generations stuck at 25% completing only on domain reload (MacOS).

### Removed

- Removed non-functional negative prompt from GPT Image models.
- Removed waiting for server progress dialog shown when Editor Application is out of focus.

## [1.0.0-pre.17] - 2025-07-31

### Added

- Added upscale badge to generated sprites and textures.
- Added preview images and descriptions to image reference operation picker dialog.

### Changed

- Changed animation trim tab loop scoring method when 'Use Best Loop' is unchecked.
- Show favorited models first in model selector. GPT Image models understand text prompts very well and are favorited by default.

### Fixed

- Fixed 'ai' for 'AI' in tooltips.
- Fixed some generations stuck at 25% completing only on domain reload.
- Fixed favorite model icon color and state.
- Fixed obsolete drag and drop handler warning for Unity Editor 6.3.
- Fixed some image reference upload failures when Unity Editor loses focus.
- Fixed checkbox layout in model selector.
- Fixed missing tooltip in add to prompt control.

## [1.0.0-pre.16] - 2025-07-16

### Added

- Added Clear option for Video References in animation generator.
- Added Clear option for Image References in material generator.
- Added badges for upscaled images in material generator.
- Added disclaimers for potential copyright.
- Added base map assignment to material generator.
- Added trim and normalization tab to animation generator.
- Added strength to Generation Data for image references in sprite generator.

### Fixed

- Fixed material preview that was sometimes blank.
- Fixed styling for Pixelate tab.
- Fixed undo-redo that was sometimes unreliable.
- Better .mov conversion and support for animation generator.
- Fixed GeneratedAssets tab title missing a space between the words in Search window.
- Fixed sound playback not stopping when closing sound trim edit window.
- Renamed delighted to base in material generator where applicable.
- Removed emission from default material generator assignments.
- Fixed initial blank texture generation thumbnail to be similar to sprite placeholder.
- Fixed a potential race condition when downloading generations.

### Removed

- Removed the generate button from the inspector of readonly animation clips.

## [1.0.0-pre.15] - 2025-06-27

### Added

- Added automatic model list refresh.

### Changed

- Changed to a soft-dependency pattern to import Plugin attributes from AI Assistant for Generators. 

### Fixed

- Fixed missing placeholder for failed animations.
- Fixed only one progress spinner shown when recovering multiple interrupted generations.

## [1.0.0-pre.14] - 2025-06-26

### Fixed

- Fixed missing null check when parsing invalid recovery files.

## [1.0.0-pre.13] - 2025-06-25

### Added

- Added model favoriting.

### Changed

- Changed default variation count to 1.
- Changed Save icon in the Sound edition tool.

### Fixed

- Fixed selected model when model list changes.
- Fixed Base Models filter.
- Fixed some missing quaternion normalization in generated animations.
- Fixed an issue where generation processes would stall or not progress when the Editor window was out of focus.
- Fixed terrain brushes not refreshing when generated.
- Fixed some light mode styles.
- Fixed missing model name on historical generation data.

### Removed

- Removed model trainer menu item.
- Removed AI Sparkle icon from the window tab of the Sound edition tool.
- Removed unused seed input from video to motion.

## [1.0.0-pre.12] - 2025-06-02

### Fixed

- Fixed image generators not showing all available aspect ratios.
- Fixed clear reference button being active with nothing to clear.
- Fixed reference image dropdown style.

## [1.0.0-pre.11] - 2025-05-29

### Added

- Added Unity Texture 2D model to model selector for textures and images.
- Added points count quote generate button tooltip.

### Changed

- Run Unity Hub if it isn't running when refreshing the cloud access token.
- Disallow remove background on small and empty images.

### Removed

- Removed extra generate sprite menuitem when enhancers package is installed

### Fixed

- Fixed text to motion finger muscle names

## [1.0.0-pre.10] - 2025-05-27

### Changed

- Implemented an attempt to refresh the cloud access token when making generation requests, if Unity Hub is running.

### Fixed

- Corrected display issues with the model selector, particularly in the light UI theme (e.g., background, info display).
- Implemented various UI refinements across light and dark themes for improved visual consistency and usability (e.g., icon colors, hover states, element styling, text capitalization).
- Resolved an issue where generation processes would stall or not progress when the Editor window was out of focus.

## [1.0.0-pre.9] - 2025-05-22

### Added

- Added support for Editor paused play mode.
- Added a 'Promote to Terrain Layer' right-click action for material generations.

### Changed

- The horizontal window splitter is now draggable.
- Reduced minimum reference image size from 32x32 to 2x2. Smaller images are now automatically upscaled before generation.

### Fixed

- Fixed window icon display in the light UI style/theme.
- Fixed issues with resuming interrupted downloads during material generation.
- Fixed the AI button's documentation menu link.
- Optimized generation point cost quote when no model is selected.

### Removed

- Removed resize handles from the model selector.

## [1.0.0-pre.8] - 2025-05-14

### Fixed

- Fixed history of new asset when promoting animation.
- Fixed generate button not disabling itself during server validation.

## [1.0.0-pre.7] - 2025-05-12

### Added

- Added more items to the Model Selector Sidebar.
- Users can now name assets before they are created.
- Added the ability to open all material terrain layers simultaneously from terrain object.
- Added multiple tooltips.
- Added some missing model icons.
- Enabled Sprite Editor customization for opening the 'Promote Asset' generator window.
- Added support for material terrain layers.
- Added documentation regarding generated assets.
- Added documentation for the 'unityai' label.
- Added a new documentation topic on using custom seeds.

### Changed

- Update SDK to version 0.18.0.
- Improved material assignment caching for better UX.
- Updated the AI Flyout menu; 'Generate' buttons are now disabled if AI Generators are turned off.
- Asset confirmation dialog doesn't show on initial replace and is more verbose.

### Fixed

- Fixed drag and drop functionality for material terrain layers, audio clips and images.
- Fixed an issue where the last object was not cleared in the image reference Object Selector.
- Better support for longer package file paths.
- Added additional recolor image validation checks.
- Fixed issues with renaming materials with generated material maps.
- Fixed the audio clip inspector to update correctly when the 'unityai' label is set on an asset.
- Fixed audio clip reference recording on Linux.
- Fixed legal agreement button over generate button.
- Fixed generating animation using video to motion during play mode.
- Fixed audio clip editing window repaint.
- Fixed animation clip window caching.

### Removed

- Removed the 'Generate' button from the AI Toolkit material shader graph interface for clarity.
- Removed model selection from image Upscale.
- Removed reference delete button where not used.

## [1.0.0-pre.6] - 2025-04-30

### Fixed

- Fixed model selection is sometimes blank and button disabled.
- Fixed sprite result selection undo/redo.
- Fixed sprite result promotion to new asset.
- Fixed sound trimming and editor file contention.

## [1.0.0-pre.5] - 2025-04-23

### Added

- Added delete shortcut for Doodle.

## [1.0.0-pre.4] - 2025-04-16

### Changed

- Update SDK to version 0.16.2.

### Fixed

- Image file conversions.
- Image reference aspect ratios.
- Generate new sprite in object picker.

## [1.0.0-pre.3] - 2025-04-09

### Changed

- Update SDK to version 0.15.0

## [1.0.0-pre.2] - 2025-03-27

### Changed

- Moved shared modules to generators namespace.


## [1.0.0-pre.1] - 2025-03-21

### Added

- Initial release of the AI Generators package.
