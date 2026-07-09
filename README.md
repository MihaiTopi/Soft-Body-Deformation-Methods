# Real-Time Soft Body Deformation

A real-time soft-body deformation project developed in Unity that explores and compares multiple approaches for interactive object deformation. The project focuses on balancing visual realism, numerical stability, and computational performance, with each method implemented from scratch under a common framework to enable direct comparison.

---

# Demonstrations

## Mass-Spring System (MSS)

<p align="center">
  <!-- Insert MSS GIF -->
</p>

---

## Position-Based Dynamics (PBD)

<p align="center">
  <!-- Insert PBD GIF -->
</p>

---

## Cluster Shape Matching (CSM)

<p align="center">
  <!-- Insert CSM GIF -->
</p>

---

## Finite Element Method (FEM)

<p align="center">
  <!-- Insert FEM GIF -->
</p>

---

# Implemented Methods

## Mass-Spring System (MSS)

- Structural spring network generated from mesh topology
- Explicit Euler integration
- Velocity damping and shape preservation
- Localized collision response
- Vertex welding for mesh continuity

## Position-Based Dynamics (PBD)

- Edge-based distance constraints
- Iterative constraint projection
- Shape preservation through positional blending
- Collision impulse propagation
- Stable real-time deformation

## Cluster Shape Matching (CSM)

- Overlapping cluster generation
- Quaternion-based rigid transformation approximation
- Averaged goal-position reconstruction
- Rest-shape stabilization
- Meshless geometric deformation

## Finite Element Method (FEM)

- Volumetric tetrahedral simulation
- Physically-based elastic deformation
- Material parameter control
- Higher physical accuracy for comparison

---

# Features

- Four soft-body deformation techniques implemented under a unified framework
- Runtime parameter tuning through custom Unity inspectors
- Mesh vertex welding to eliminate visual seams
- Fixed-timestep simulation for consistent behaviour
- Performance benchmarking and visual comparison
- Modular architecture allowing additional deformation methods to be integrated

---

# Technologies

- Unity
- C#
- Custom physics implementations
- Unity Editor scripting

---

# Project Goals

This project explores several approaches to real-time deformable object simulation, highlighting the trade-offs between physical realism, numerical stability, visual quality, and computational performance. Rather than relying on existing physics middleware, each deformation method was implemented from scratch to better understand the underlying algorithms and their practical behaviour in interactive environments.

Beyond comparing these techniques, this project serves as the foundation for a larger future project focused on developing a more refined, optimized, and production-ready soft-body deformation system. The insights gained from the implementations and benchmarks presented here will be used to select the most suitable approach and guide further research and development.
