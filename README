# ⭐ Doofus Adventure (Unity Mini-Game)

**Doofus Adventure** is a grid-based survival mini-game built in Unity. The player is stranded on disappearing platforms ("pulpits") that spawn, move, and vanish based on a strict timed lifecycle. The core challenge is to stay alive by moving onto the next safe pulpit before the current one disappears.

## 🚀 Project Highlights

This project demonstrates robust C# scripting and scalable game architecture:

*   ✔ **Procedural Grid Spawning** – Deterministic generation of platforms on a grid.
    
*   ✔ **JSON-Driven Configuration** – Game balance (speed, timers) is loaded from external files.
    
*   ✔ **Lifecycle Management** – Complex coordination between object destruction and instantiation.
    
*   ✔ **Robust Architecture** – Centralized `GameManager` handling state, decoupled from UI.
    
*   ✔ **Scene Management** – Clean reset flows and memory management.
    

## 🎮 Gameplay Overview

1.  **The Start:** The game begins with one starting pulpit and the player placed on top.
    
2.  **The Loop:** \* Each pulpit has a randomized lifetime (based on JSON config).
    
    *   A new pulpit is spawned exactly `spawnInterval` seconds after the previous one.
        
    *   At any time, a maximum of **2 platforms** exist.
        
3.  **The Threat:** When a pulpit’s timer hits 0, it destroys itself.
    
4.  **The Goal:** The player must move to the newly spawned pulpit to survive.
    
5.  **Game Over:** If the player falls or fails to move before the platform vanishes, the Game Over panel appears with a Retry option.
    

## 🧩 Core Architecture

### 1\. GameManager.cs

_The central orchestrator of the game loop._

*   **Responsibilities:**
    
    *   Loads `game_config.json` from `StreamingAssets`.
        
    *   Manages the "Max 2 Platforms" rule.
        
    *   Aligns spawns to a specific grid system.
        
    *   Schedules the next platform spawn based on the `spawnInterval`.
        
    *   Provides public methods for the UI to reset the scene (`ResetSceneClean()`).
        

### 2\. Pulpit.cs (Platform)

_Self-managed platform logic._

*   **Responsibilities:**
    
    *   Maintains its own lifetime countdown.
        
    *   Updates the floating UI text timer.
        
    *   **Logic:** When lifetime ≤ 60%, it requests the `GameManager` to prepare the next spawn.
        
    *   **Destruction:** Calls `NotifyDestroyed` on the Manager before destroying itself.
        

### 3\. PlayerController.cs

_Physics-based movement._

*   **Responsibilities:**
    
    *   Reads WASD/Arrow keys input.
        
    *   Moves the player using `Rigidbody` physics.
        
    *   Fetches movement speed dynamically from the JSON config at runtime.
        

### 4\. UIManager.cs

_Interface flow._

*   **Responsibilities:**
    
    *   Toggles the Game Over panel.
        
    *   Handles "Retry" button events.
        
    *   Communicates directly with `GameManager` to reset the game state.
        

### 5\. CameraFollow.cs

*   Smoothly tracks the player's position to keep the action centered.
    

## ⚙️ JSON Configuration

The game does not rely on hardcoded Inspector values. It loads settings from `StreamingAssets/game_config.json` on startup.

**File Location:** `Assets/StreamingAssets/game_config.json`

    {
      "player_data": {
        "speed": 3
      },
      "pulpit_data": {
        "min_pulpit_destroy_time": 4,
        "max_pulpit_destroy_time": 5,
        "pulpit_spawn_time": 2.5
      }
    }
    

_Benefits: Allows for game balancing and tuning without recompiling code._

## 🔄 Spawning & Grid Logic

The system uses a **Grid-Based Deterministic approach** to ensure platforms always align.

**Spawn Priority:** When a new platform is needed, the system checks adjacent grid cells in this order:

1.  Forward (+Z)
    
2.  Right (+X)
    
3.  Left (-X)
    
4.  Back (-Z)
    

**Logic Flow:**

*   **`SpotOccupied()`** ensures a platform never spawns on top of another.
    
*   **`GridAlign()`** snaps all positions to integer coordinates (e.g., 9x9) to ensure perfect movement.
    
*   The system guarantees: `[Current safe platform] <--> [Next spawned platform]`.
    

## 🧪 Development Notes

_(Technical challenges addressed during development)_

The biggest challenge was synchronizing platform destruction with scheduled spawning so that players never get stuck without a reachable platform.

**Solutions Implemented:**

*   **Preventing Overlaps:** Used grid keys to track occupied spaces.
    
*   **Timing Issues:** Implemented a strict scheduler to ensure the next platform appears exactly as the previous one begins to fade.
    
*   **Scene Cleanup:** The Retry system ensures all delegates are unsubscribed and objects destroyed before reloading the scene to prevent memory leaks or "ghost" logic.
    

## 📁 Folder Structure

    Assets/
     └── Game/
          ├── Scripts/
          │     ├── GameManager.cs
          │     ├── PlayerController.cs
          │     ├── Pulpit.cs
          │     ├── UIManager.cs
          │     ├── CameraFollow.cs
          │     └── GameConfig.cs
          ├── Prefabs/
          │     └── Pulpit.prefab
          └── UI/
                ├── GameOverPanel
                ├── CountdownText
                └── RetryButton
    StreamingAssets/
     └── game_config.json
    

_Created for Unity Project Portfolio._
