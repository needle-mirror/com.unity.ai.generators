# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

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
