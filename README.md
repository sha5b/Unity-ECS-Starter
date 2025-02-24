# Network Agents

A Unity-based simulation featuring autonomous agents in a procedurally generated world. The project implements a custom Entity Component System (ECS) architecture to manage complex agent behaviors and world interactions.

## Architecture

### Entity Component System (ECS)

The project uses a custom ECS implementation with three main pillars:

- **WorldManager**: Core orchestrator that manages systems and entities
- **Entities**: Game objects that contain components
- **Components**: Data containers that define entity properties
- **Systems**: Logic processors that operate on entities with specific components

### Core Systems

#### Terrain System
- Procedural terrain generation using multiple noise layers
- Seamless chunk-based world loading
- Multiple biomes (Plains, Forest, Mountains, Desert)
- Smooth transitions between chunks and biomes
- Dynamic LOD and chunk management based on viewer position

#### NPC System
NPCs are autonomous agents with:
- **Needs**: Hunger, Thirst, Energy, Social
- **Personality Traits**: 
  - Sociability: Influences desire for social interaction
  - Bravery: Affects risk-taking behavior
  - Curiosity: Drives exploration
  - Diligence: Impacts work ethic and task focus
- **States**: 
  - Movement: Idle, Moving
  - Activities: Working, Resting, Eating, Drinking
  - Social: Interacting, Socializing
- **Behaviors**:
  - Dynamic decision-making based on needs and personality
  - Pathfinding and terrain-aware movement
  - Resource seeking and consumption
  - Social interaction with other NPCs

#### Spawner System
- NPC Generation:
  - Configurable initial population
  - Random name assignment
  - Customizable spawn radius and positioning
  - Group spawning capabilities
- Initialization:
  - Randomized movement properties (speed, rotation, interaction range)
  - Unique personality trait generation
  - Initial needs randomization
  - Optional visual representation through prefabs

#### Resource System
- Resource Types:
  - Food: Consumable for hunger needs
  - Water: Consumable for thirst needs
  - RestArea: Used for energy recovery
  - WorkArea: Designated work locations
- Resource Properties:
  - Quantity tracking
  - Consumption rates
  - Quality multipliers
  - Automatic replenishment
  - Infinite/finite resources
- Resource Management:
  - Usage tracking
  - Depletion mechanics
  - Replenishment delays
  - Visual feedback through Gizmos

#### Time System
- Configurable day/night cycle:
  - Customizable day length
  - Dawn, Day, Dusk, Night periods
  - Transition periods (dawn/dusk)
- Time Management:
  - Real-time to game-time conversion
  - Day counting and period tracking
  - Progress tracking within each day
- Environmental Impact:
  - Influences NPC behavior patterns
  - Affects resource availability
  - Modifies terrain visualization

## Technical Features

### Terrain Generation
- Uses FastNoiseLite for coherent noise generation
- Multiple noise layers for varied terrain:
  - Base terrain layer
  - Biome variation layer
  - Detail noise layer
- Smooth chunk transitions using edge blending
- Custom terrain shader for biome visualization

### NPC AI
- Needs-based decision making
- Personality-influenced behavior
- Dynamic target selection
- Social interaction system
- Resource seeking and consumption
- Terrain-aware movement

### Performance Optimization
- Chunk-based world loading
- Entity pooling
- Efficient component lookup
- Dependency-aware system initialization

## Project Structure

```
Assets/
├── Scripts/
│   └── ECS/
│       ├── Core/
│       │   ├── WorldManager.cs     # Central ECS orchestrator
│       │   ├── Entity.cs           # Base entity class
│       │   ├── ComponentBase.cs    # Base component class
│       │   └── SystemBase.cs       # Base system class
│       ├── Components/
│       │   ├── NPCComponent.cs     # NPC properties and state
│       │   ├── ResourceComponent.cs # Resource properties
│       │   ├── TerrainDataComponent.cs # Terrain chunk data
│       │   ├── TimeComponent.cs    # Time management
│       │   └── VoxelData.cs        # Voxel-based data structures
│       └── Systems/
│           ├── NPCSystem.cs        # NPC behavior management
│           ├── ResourceSystem.cs    # Resource management
│           ├── TerrainSystem.cs    # Terrain generation and management
│           ├── TimeSystem.cs       # Time simulation
│           └── SpawnerSystem.cs    # Entity spawning
```

## Dependencies

- Unity Universal Render Pipeline (URP)
- Custom TerrainShader for biome visualization
- FastNoiseLite for procedural generation

## Implementation Details

### Entity Management
- Automatic component registration
- System dependency resolution
- Event-based component updates
- Efficient entity querying

### Terrain Generation
- Multi-threaded chunk generation
- Seamless chunk stitching
- Biome blending
- Height-based biome distribution

### NPC Behavior
- State machine-based behavior system
- Need-based decision making
- Dynamic target selection
- Social interaction mechanics
- Resource consumption logic

### Resource Management
- Sophisticated resource type system with multiple categories
- Dynamic usage tracking and state management
- Configurable regeneration mechanics:
  - Customizable replenishment rates
  - Delayed regeneration options
  - Quality-based consumption effects
- Proximity-based discovery and interaction
- Visual debugging through Unity Gizmos
