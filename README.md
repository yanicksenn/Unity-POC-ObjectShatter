# POC Object Shatter

Proof of concept of shattering objects procedurally.

## Basic Idea

1. Read the mesh of an object.
1. Randomly draw planes through that mesh (Random plane in a sphere) and slice it.
1. Create new game objects of the resulting pieces.
