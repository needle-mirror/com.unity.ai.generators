# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

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
