# Real-Time Soft-Body Deformation Methods

A personal exploration of real-time soft-body deformation methods implemented in Unity.

This project compares several approaches for simulating visually plausible object deformation in an interactive environment. The main focus was not to build a fully physically accurate simulator, but to understand how different real-time deformation techniques behave under similar conditions, what trade-offs they introduce, and which direction would be most promising for a larger future project.

The implemented real-time methods are:

- **Mass-Spring Systems (MSS)**
- **Position-Based Dynamics (PBD)**
- **Cluster Shape Matching (CSM)**

A **Finite Element Method (FEM)** simulation is also included as a qualitative visual reference. The FEM simulation was **not implemented as part of this Unity project** and is not used as a direct performance benchmark. It is included only to provide visual context for how a more physically accurate volumetric deformation method can behave.

## Project Goal

The goal of this project was to build a practical foundation for choosing a deformation approach for a larger future system. Each method was implemented close to its basic formulation, with only the practical adjustments needed to make it usable in a real-time Unity context.

This makes the project a stepping stone toward a future implementation of a more refined, optimised, and advanced version of one of these real-time soft-body methods.

The comparison focuses on:

- runtime performance;
- visual deformation behaviour;
- shape preservation;
- recovery after impact;
- stability under stronger collisions;
- limitations of basic real-time implementations.

## Implemented Methods

### Mass-Spring Systems

The Mass-Spring System represents the mesh as particles connected by structural springs. It is force-based, simple to implement, and computationally efficient. It produces dynamic elastic motion and visible ripple-like behaviour, but it is sensitive to stiffness, damping, and timestep-related stability issues.

### Position-Based Dynamics

The Position-Based Dynamics implementation predicts vertex positions and then directly corrects them using edge-distance constraints. It is generally stable and intuitive to control, but in this simplified implementation it can produce fold-like or cloth-like deformation under stronger impacts because it does not include volumetric constraints.

### Cluster Shape Matching

The Cluster Shape Matching implementation groups vertices into overlapping spatial clusters. Each cluster estimates a local shape-matching goal, and vertices shared by multiple clusters average those goal positions. This produced the most coherent recovery behaviour in the tested scenarios and offered a strong balance between stability and real-time performance.

### FEM Reference

The FEM result is included only as a qualitative visual reference. It was used to show how volumetric deformation can behave under strong impact conditions and to highlight the limitations of the simpler surface-based real-time implementations.

The FEM comparison should not be interpreted as a direct implementation or performance comparison.

## Visual Demonstrations

The following GIFs are located in the `VisualDemonstration` folder.

These demonstrations use a **high-impact-force test context**. The purpose is to exaggerate deformation and mimic the high deformability visible in the FEM reference. This also helps reveal the limitations of the basic, close-to-the-roots real-time implementations.

### Mass-Spring Systems

<img src="VisualDemonstration/MSS.gif" width="420">

### Position-Based Dynamics

<img src="VisualDemonstration/PBD.gif" width="420">

### Cluster Shape Matching

<img src="VisualDemonstration/CSM.gif" width="420">

### FEM Reference

<img src="VisualDemonstration/FEM.gif" width="420">

## Notes on the Comparison

The Unity methods are surface-mesh-based real-time approximations. They are designed for interactive responsiveness and visual plausibility rather than strict physical accuracy.

The FEM reference, by contrast, represents a more physically grounded volumetric simulation. Because of this difference, the FEM result is used only as a qualitative reference point, not as a direct benchmark competitor.

The high-impact demonstrations are intentionally demanding. They are useful for showing how each real-time method responds when pushed beyond gentle deformation conditions.

## Future Direction

This project serves as groundwork for a larger future system where one of the explored real-time methods could be developed further.

Possible future improvements I am considering:

- more advanced constraint formulations;
- deformable collider support;
- volumetric or pseudo-volumetric behaviour;
- Unity Job System or Burst optimisation;
- GPU-based simulation;
- lower-resolution simulation cages driving high-resolution render meshes.

Based on the observed behaviour, Cluster Shape Matching appears to be a promising candidate for further refinement, especially for soft, elastic, water-balloon-like deformation. However, the final choice would depend on the target object, desired material behaviour, and performance constraints of the larger project.
