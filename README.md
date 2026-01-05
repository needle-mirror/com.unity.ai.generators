# Unity AI Generators Package

This package provides a suite of tools for integrating generative AI directly into Unity Editor workflows. It enables the creation and refinement of various asset types—including **images/sprites**, **materials**, **meshes**, **sounds**, and **animations**—using text prompts and other creative inputs.

The package is designed with a modular and scalable architecture, ensuring a consistent user experience and a maintainable codebase across all its features.

## Core Features

The AI Generators package offers a wide range of capabilities across multiple creative domains:

#### Multi-Modal Asset Generation
-   **Text-to-Asset**: Generate a variety of assets directly from descriptive text prompts.
    -   **Images & Sprites**: Create 2D art, icons, character sprites, and skybox cubemaps (as equirectangular images).
    -   **PBR Materials**: Generate a full set of PBR texture maps for materials and terrain layers.
    -   **Meshes**: Create static 3D models as `GameObject` prefabs.
    -   **Sound Effects**: Produce sound effects and ambient audio.
    -   **Humanoid Animations**: Generate `AnimationClip` assets from motion descriptions.
-   **Reference-Driven Generation**: Guide the AI's output using existing assets as a reference for style, composition, color, or motion.
    -   **Image-to-Image/Material**: Use source images to guide style, pose, depth, or line art.
    -   **Sound-to-Sound**: Generate variations from a source `AudioClip` or microphone input.
    -   **Video/FBX-to-Animation**: Extract motion from video files or existing FBX animations.

#### Advanced Refinement & Editing Tools
-   **Image Editing**: A powerful suite of tools for refining images, including AI-powered **in-painting**, **background removal**, **upscaling**, **recoloring**, and **pixelation**.
-   **Audio Editing**: A dedicated "Trim" window with an interactive oscillogram for destructively trimming audio clips and shaping volume with a multi-point envelope.
-   **Animation Post-Processing**: Utilities for automatically finding optimal loop points and normalizing the root motion of an `AnimationClip`.

#### Seamless Editor Integration
-   **Dedicated Generator Windows**: Each asset type has a dedicated, non-blocking editor window that serves as the primary interface for generation and refinement.
-   **Inspector & Context Menus**: "Generate" and other relevant action buttons are added directly to the inspector headers and project context menus for `Texture2D`, `Material`, `AudioClip`, `AnimationClip`, and `Cubemap` assets.
-   **Interactive Previews**: All generator windows feature live previews and generation histories, allowing for rapid iteration and comparison of results.
-   **Drag-and-Drop Workflow**: Promote generated assets into your project by simply dragging them from the generator window into the Project view or Scene.

#### Centralized AI Model Management
-   **Model Selector**: A shared, unified window for browsing, searching, and filtering all available AI models across every modality.
-   **Favorites & Auto-Selection**: Users can mark preferred models as favorites for quick access, and the system can intelligently recommend a default model to streamline the workflow.

#### Robust Workflow Features
-   **Generation History**: All generations are saved to a local cache, allowing users to revisit and reuse previous results for any given asset.
-   **Download Recovery**: If the editor is closed during a download, the system will prompt to resume the interrupted generations upon reopening the project.
-   **Asset Management**: Generated assets and their dependencies (like texture maps for a material) are neatly organized into dedicated subfolders next to the source asset.

## Package Structure

The package is organized into a set of distinct modules. The four main **Generator Modules** provide the user-facing features, while the **Foundational Modules** contain the shared architecture and services they all depend on.

### Generator Modules
These modules contain the specific logic and UI for each asset type.

-   **[`/Unity.AI.Animate/README.md`](Modules/Unity.AI.Animate/README.md)**: Logic for Text-to-Motion and Video-to-Motion, processing animation data, and exporting to `AnimationClip`.
-   **[`/Unity.AI.Image/README.md`](Modules/Unity.AI.Image/README.md)**: Logic for all 2D image generation (including `Cubemap` skyboxes) and refinement modes, including the Doodle Pad and an extensibility API.
-   **[`/Unity.AI.Pbr/README.md`](Modules/Unity.AI.Pbr/README.md)**: Logic for generating PBR texture sets for `Material` and `TerrainLayer` assets, including shader property auto-detection.
-   **[`/Unity.AI.Mesh/README.md`](Modules/Unity.AI.Mesh/README.md)**: Logic for generating static 3D models as `GameObject` prefabs from text prompts.
-   **[`/Unity.AI.Sound/README.md`](Modules/Unity.AI.Sound/README.md)**: Logic for generating `AudioClip` assets and the destructive trimming/enveloping editor.

### Foundational Modules
These modules provide the architectural backbone and shared services for the entire package.

-   **`Unity.AI.Generators.Asset`**: Manages asset identification via GUID-based `AssetReference`s and handles all interactions with the `AssetDatabase`.
-   **`Unity.AI.Generators.Contexts`**: A simple dependency injection system for `UIToolkit`.
-   **`Unity.AI.Generators.Redux`**: The foundational Redux-like state management library, including the `AsyncThunk` pattern for asynchronous operations.
-   **[`/Unity.AI.Generators.Tools/README.md`](Modules/Unity.AI.Generators.Tools/README.md)**: A high-level C# API providing a unified entry point for agentic systems (like the Unity AI Assistant) to programmatically drive asset generation.
-   **`Unity.AI.Generators.UI`**: Contains shared UI components, manipulators (e.g., drag-and-drop), and common Redux-like actions used by all generator windows.
-   **`Unity.AI.Generators.IO`**: Provides shared file and asset I/O utilities (temporary asset management, resilient file ops, file-type detection, image format support, and long-path handling). See [`Modules/Unity.AI.Generators.IO/README.md`](Modules/Unity.AI.Generators.IO/README.md) for details.
-   **`Unity.AI.Generators.Sdk`**: The client-side SDK for authenticating and communicating with all backend AI services.
-   **`Unity.AI.ModelSelector`**: The shared service and UI for discovering, filtering, and selecting AI models.

---

## Architectural Overview (For Developers)

The entire AI Generators package is built on a unified and scalable architecture designed for maintainability and a consistent developer experience.

### The Redux-like Backbone

At the heart of the architecture is a **Redux** state management pattern. This provides a single, predictable source of truth for the entire application state.

-   **Unidirectional Data Flow**: The data flow is strictly one-way, which makes the application easy to reason about and debug.
    `UI Interaction` -> `Dispatch(Action)` -> `Middleware (Thunk)` -> `Reducer` -> `New State` -> `UI Update (via Selectors)`
-   **State is Read-Only & Immutable**: Reducers are pure functions that take the current state and an action, and return a *new* state object. This prevents side effects and ensures predictability.
-   **Slices**: The state is organized into modular `Slices`, each managing a specific domain (e.g., `generationSettings`, `generationResults`, `modelSelector`).

### The Architectural Stack

The modules are organized into clear layers of dependency:

1.  **Redux (`Unity.AI.Generators.Redux`)**: The foundational Redux-like state management library. It is pure C# and has no dependencies on Unity's UI or asset systems.
2.  **UI & Contexts (`Unity.AI.Generators.Contexts`, `Unity.AI.Generators.UI`)**: This layer connects Redux to `UIToolkit`. It provides the `UseContext` and `Use` hooks that allow UI components to reactively subscribe to state changes. It also contains common UI components and manipulators.
3.  **Services (`Unity.AI.Generators.Asset`, `Unity.AI.Generators.Sdk`)**: These modules provide essential, decoupled services for asset handling and backend communication.
4.  **Shared Features (`Unity.AI.ModelSelector`)**: A specialized module that provides a complete, reusable feature (the model selection window) to all other modules.
5.  **Generator Modules (`Image`, `Material`, `Sound`, `Animate`, `Mesh`)**: The top-level modules that consume all the foundational layers to build their specific, user-facing generator tools.

### Key Architectural Concepts

-   **Asset Identification via `AssetReference`**: Throughout the Redux-like state, assets are never referenced directly. Instead, a serializable `AssetReference` record containing the asset's **GUID** is used. This makes the system resilient to assets being moved or renamed in the project. UI components use selectors to get the `AssetReference` and then resolve it to a path for loading.

-   **Asynchronous Operations via `AsyncThunk`s**: All side effects, especially communication with the backend, are handled by `AsyncThunk`s. A thunk is an action that can perform asynchronous work and dispatch other actions. This pattern keeps reducers pure and centralizes all async logic, complete with standardized `pending`, `fulfilled`, and `rejected` states.

-   **Shared Services & UI**: To ensure consistency and reduce code duplication, common functionalities are abstracted into shared modules. The `ModelSelector` is the prime example, providing a single, consistent UI for model selection across all generators. The `Unity.AI.Generators.UI` module similarly provides shared actions, manipulators, and utilities.

-   **The Adapter Pattern (`IMaterialAdapter`)**: To handle different but conceptually similar asset types, the system uses the Adapter pattern. The `IMaterialAdapter` interface provides a common API for interacting with both `UnityEngine.Material` and `UnityEngine.TerrainLayer` assets, allowing the rest of the system to treat them identically.

-   **Resilience and Caching**: The architecture includes robust, shared mechanisms for workflow resilience. The `GenerationRecovery` utility in each module allows users to resume interrupted downloads. The `TextureCache`, `AudioClipCache`, and `AnimationClipDatabase` provide a two-tier caching system (in-memory for the session, on-disk for persistence) to optimize performance and reduce redundant downloads.
