# About AI Generators

AI Generators is a package that provides tools for adding generative AI to Unity workflows.

# How to get started

Use the project found in `Projects/Development Unity 6`.

# Front-end
- Principled Approach:
  - com.unity.ai.generators is designed to be principled rather than opinionated.
- Modular Components:
  - Uses modular UXML components bound to a Unity asset-aware Redux store.
- Scalability:
  - Achieved through implicit sharing and caching mechanisms.
- Type Usage Hierarchy:
  - Prioritizes common Unity types first.
  - Uses extension classes second.
  - Employs new lightweight plain-old-data (POD) types last.
- Maintainability:
  - Facilitates easy maintenance by:
    - Adding new components instead of requiring to modify existing ones.
    - Adding new selectors and reducers without breaking existing ones.
- Design Principles:
  - Internally favors:
    - Composition over Inheritance.
    - Functions over Objects.
    - UXML and USS over C# Code.
    - Selectors and Reducers over Events.
    - Async thunks for long running operations.
- External Flexibility:
  - Externally, the system allows for any implementation approach ("anything goes").
- State Management:
  - Store is sliced by scope (session, settings, results) and indexed by Unity asset.
  - Persists through domain reloads.

# Interface with Backend
- Asynchronous Operations:
  - Utilizes a single asynchronous thunk for backend calls.
  - Scalable to any number of concurrent asset generations.
  - Services the Front-end through the Store.
- Programming Style:
  - Written in a procedural/imperative style with linear control flow.
- Task Execution:
  - Employs globally synchronized delayed task execution with priority scheduling.
  - Ensures that all asset tasks are executed globally at a fixed frequency tailored to the backend server.
- Priority Scheduling:
  - Maintains FIFO (First-In-First-Out) order for tasks with the same priority.
- Task Priorities:
  - Retrieving a URL is the highest priority.
  - Posting a job is high priority.
  - Checking completion is low priority.
- Progress Tracking:
  - Includes progress markers at each significant step.
  - Provides continuous progress updates.
- State Management:
  - State is read at start and updated at end to prevent side-effects (excluding progress updates).
