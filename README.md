# Space-Game

This project, formally called the "Space-Game" is a personal project to demonstrate my skills with complex algorithms, procedural noise, GPU Programming, and game design.

In this project, a procedural galaxy will be generated for the player. Using clever mechanics, the player can travel between each solar system and planet in real time. Each solar system and subsequent planet are procedurally generated as well. The player can explore each one of these planets, collect resources, build bases, and converse with alien species.

The planets use a dynamic Level-Of-Detail system to create the needed geometry on the fly. This makes it possible for each planet to be planet-sized while still containing detail on the minute levels. The terrain of each planet uses a mix of algorithms. As of now, each planet uses a mix of Worley Noise and Simplex Noise to generate its various terrains. In the future, more noise algorithms will be introduced to create rivers, towns, and economies on each of these planets.

![Procedural Planet](https://raw.githubusercontent.com/Googoo03/Space-Game/782587b9eb24069287db498643e91f83170eefb7/.github/images/Planet_Render.png)

Each planet is made of a cube-sphere. There are 6 sides to each cube, whose lengths are normalized to give the impression of a sphere. Each side of the sphere is the root of an individual quadtree, which extends in height when the player crosses a boundary threshold. This continues recursively until a maximum height is reached. Example here.

![LOD_Example](https://raw.githubusercontent.com/Googoo03/Space-Game/fa63fe534175d9518bdfca7bc8a105b07b1a7ae8/.github/images/DynamicLOD.png)

All terrain values are decided by the GPU under a compute shader. This applies for both the Simplex Noise algorithm and Worley Noise algorithm. Each vertex position is then displaced in parallel in the GPU and passed back to the CPU for mesh generation.

After the mesh positions have been decided, the normals are recalculated and the surface shader takes over. The surface shader assigns a color value per pixel of the mesh. Similar to the compute shader, this is computed in parallel on the GPU, allowing for consistent 400fps. The shader determines each color either by a corresponding height value of the vertex, or through calculating the steepness of each pixel, allowing for organic mountain colors to appear.

![Atmosphere Render](https://raw.githubusercontent.com/Googoo03/Space-Game/782587b9eb24069287db498643e91f83170eefb7/.github/images/Atmosphere_Render.png)

The atmosphere uses a post-processing shader that uses a clever algorithm to compute a fog around the planet quickly and efficiently. Most algorithms use a raycast, needing an iterative process to compute. However, my solution uses the orthogonal projection of the player's view and the planet position to determine where the atmosphere is. This is done in O(1) time as opposed to the standard O(n) time. Similar to all previous shaders, this is ran on the GPU in parallel.

The difficulties that come with running algorithms on the GPU is in most cases pixels cannot communicate with each other. Therefore, each pixel has to determine is color or displacement purely based on itself. Additionally, algorithms must be computationally simple for compute shaders. If not, then they run the risk of interfering with each other on the same GPU. This was a large issue when implementing the Worley Noise algorithm, and ultimately led to refinements in its calculation.
