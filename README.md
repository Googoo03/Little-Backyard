
![LB_Banner_Aug21](https://github.com/user-attachments/assets/df342711-48be-4466-9554-10fd683af113)

## Description

This project, formally called Little Backyard is a passion project of mine to demonstrate my skills with complex algorithms, procedural noise, GPU Programming, and game design. My ultimate goal for this project is to be published on Itch.io, link [here](https://karalis03.itch.io/little-backyard), and be maintained with features that both the playerbase and myself look forward to implementing. This game will be free to play, with pay-as-you-wish donations to further fund development.

In this project, the player is placed in the middle of a procedurally generated galaxy, filled with diverse planets, resources, and alien life. The player does not have a goal by design, this game falls under the "zen" category of games, letting the player simply experience the world around them as a moment to relax from the stresses of the world they wish to escape. 

## Current Goals

As of September 6, 2024, my current goal is to implement an object pool system for all generated foliage on planets. At the moment, all foliage created is simply a GPU-instanced mesh, letting the player see but not interact with the foliage.

![Tree_Devlog2](https://github.com/user-attachments/assets/a8ddd89a-3b04-4ef7-b35d-393fced674f5)

By implementing this system, this will only have overhead at the beginning of the game, and virtually none elsewhere. All foliage objects will be picked from a pool, and will change their mesh, position, and various properties accordingly as the terrain generation scripts call on them. Once this is finished, the only GPU-instanced mesh will be the grass.

## Challenges and Compromises

One of the detailing features that I wanted in Little Backyard was the ability to travel between planets and solar systems with no loading screen. There are many incredible examples of this, including No Man's Sky, however the player is still unable to travel between solar systems, rather only between planets within the same solar system. To tackle this, the player does not move around the galaxy, rather, the galaxy moves around the player. The entire playspace is encompassed in a 3x3 grid of "galactic boxes" - subset managers that govern their own part of the galaxy. When a player crosses the boundary of these boxes, they are shifted accordingly and generate any needed features.

![GalacticBox](https://github.com/user-attachments/assets/9b2ed92a-30bd-4a86-ba32-8d1e8e1ca356)


This means the entire play space that the player can interact with must fit within floating point error boundaries. This puts a barrier on how large the planets can be without encountering either floating point errors or solar systems interfering with each other. There are ideas to get around this, but nothing of the sort has been implemented as of yet.

### Demo Video

[![Watch the Demo](https://github.com/user-attachments/assets/98fae1f5-7689-48ff-83c5-d8932e42e49e)](https://youtu.be/OuMUYvi9r6k)

### Looking Forward

There is a significant bottleneck in game design, this being the 2D nature of the terrain. Because there is only a manipulated plane, there becomes no possibility of cave systems or overhangs. This can be remedied with a voxel engine, of which I have made a prototype in this following demo. After playing around with the shaders, I have realized there is potential for a secondary city-building game that surrounds this mechanic. Future updates regarding this algorithm will follow soon.

[![Watch the Demo](https://github.com/user-attachments/assets/65b60358-30e5-41e5-b924-347026c8f37b)](https://youtu.be/EpPYONP5dHw)

