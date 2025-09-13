# POC Object Shatter

![recording](https://raw.githubusercontent.com/yanicksenn/Unity-POC-ObjectShatter/9df114019419b42c5506d68e2983f407126fc1a6/Img/recording.gif)

Proof of concept of shattering objects procedurally.

## Basic Idea

1. Read the mesh of an object.
1. Randomly draw planes through that mesh (Random plane in a sphere) and slice it.
1. Create new game objects of the resulting pieces.
