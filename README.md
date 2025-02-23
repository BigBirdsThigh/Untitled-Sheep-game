# Boids System - Developer Guide

## Overview

This README is here to give you a detailed enough overview of the boid code so that you **hopefully** don’t have to dig through my code just to figure out how to make the player interact with them. The boid system has **4 states**:

1. **Roaming** - Default state, the boids move naturally.
2. **Panic** - Triggered when boids take damage or detect the player.
3. **Regroup** - Entered after a set period without spotting the player or taking damage.
4. **Die** - Boid is removed from the system.

### **State Transitions**
- A **boid starts in Roaming**.
- If it **detects the player** (via raycast) or **takes damage**, it **enters Panic** they also alert nearby boids to enter panic mode.
- After a **set duration**, if the player is no longer detected, it **enters Regroup**.
- Once enough **boids have regrouped**, they **return to Roaming**.

**Note:**  
Player detection and damage-based state changes **are already implemented**. Simply ensure the player has the `Player` tag in the Unity editor.

---

## **Boid Interaction**

### **Getting a Boid Reference**
To interact with a boid, you need a reference to it:
```csharp
public Boid boid; // Example reference to a boid
```
However, I imagine you'll be dynamically obtaining a boid from colliding or some other means

### **Changing a Boid's State**
You can change the state of a boid by using
```csharp
boid.ChangeState(BoidState.Panicking) // Example of forcing the boid into panick mode
```
### **Damaging a Boid**
To apply damage to a boid use:
```csharp
boid.TakeDamage(damageAmount) // Where damageAmount is a float
```
if a boid's health reaches 0 it will obviously die, there is a small death animation in place

## **Current System Limitations**
- There is no path planning in place right now, if boids feel like they are pathing weirdly this will most likely be why(working on this)
- So currently they follow a sphere object called **Follow**
- This will be most notable in the **Panic** state as they may still try to go towards this object even if they see the player there

## **Cleanup tasks**
### **Things to remove**
- There is a **Player** object and **PlayerTest** script in Assets/Sheep/Prefabs. this was used for testing boid interactions, this should be removed
### **To-Do List**
- **BoidManager**: Allow spawning a new Boid group when all boids are destroyed and a new round is started
- **UI System**: 
	- Display an **upgrade selection** if the win condition(all boids destroyed) is met
	- Show a **Game Over** screen with a **Replay** button if the lose condition(timer ran out) is met
	- **Replace**: placeholder boid cube with sheep model
	- **Implement**: basic **BFS**(Best First Search) for path planning

## **BoidManager Overview**
The **BoidManager** controls the boids globally:
- Spawns a group of boids when starting
- Handles the **Regrouping** logic (setting all boids to the **Roaming** state when enough have regrouped)
- Removes boids when they die
- Provides possibly useful functions:
	- ```csharp
		boidManager.RemoveBoid(boid); // Removes a boid from the system
		boidManager.AnyBoidsPanicking(); // Returns true if any boids are in Panic state```

 these functions are mostly for the future game state stuff e.g. when the timer should tick down
