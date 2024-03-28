# Space-Game

This project, formally called the "Space-Game" is a personal project to demonstrate my skills with complex algorithms, procedural noise, GPU Programming, and game design.

In this project, a procedural galaxy will be generated for the player. Using clever mechanics, the player can travel between each solar system and planet in real time. Each solar system and subsequent planet are procedurally generated as well. The player can explore each one of these planets, collect resources, build bases, and converse with alien species.

The planets use a dynamic Level Of Detail system to create the needed geometry on the fly. This makes it possible for each planet to be planet-sized while still containing detail on the minute levels. The terrain of each planet uses a mix of algorithms. As of now, each planet uses a mix of Worley Noise and Simplex Noise to generate its various terrains. In the future, more noise algorithms will be introduced to create rivers, towns, and economies on each of these planets.

![Procedural Planet](https://raw.githubusercontent.com/Googoo03/Space-Game/ccc1338ddbefa84f137c76cef708c850e424197d/.github/images/DotProductPlanet.png)

Each planet is made of a cube-sphere. There are 6 sides to each cube, whose lengths are normalized to give the impression of a sphere. Each side of the sphere is the root of an individual quadtree, which extends in height when the player crosses a boundary threshold. This continues recursively until a maximum height is reached. Example here.

![LOD_Example](https://raw.githubusercontent.com/Googoo03/Space-Game/fa63fe534175d9518bdfca7bc8a105b07b1a7ae8/.github/images/DynamicLOD.png)
