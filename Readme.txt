Game Description:
In this First-person shooter the Player has a Portal Gun that can shoot 2 Portals on surfaces.
Objects can teleport between the two Portals while maintaining their relative position, orientation and speed.
Currently the game has 2 Levels that were built with ProBuilder.
But they are only there to test the mechanics and there are no objectives.

Dependencies:
ProBuilder (Version 5.0.6) by Unity Technologies
ProGrids (Version 3.0.3-preview.6) by Unity Technologies
Starter Assets - First Person Character Controller (Version 1.1.1) by Unity Technologies:
https://assetstore.unity.com/packages/essentials/starter-assets-first-person-character-controller-196525

Used Models and Textures:
HD Portalgun Remastered by Igrium: https://sketchfab.com/3d-models/hd-portalgun-remastered-073ba676dd364b6e96c2d0a9b9991362
Companion Cube by Michael Klement: https://sketchfab.com/3d-models/companion-cube-47502a51b211475b9750b04b4b1dffc4
Chell by Mm123: https://sketchfab.com/3d-models/chell-3b239db8827e49d5aabdb9aba7e18952
Wall Textures: https://archive.org/details/portaltexturerocksliqmetal

Other Portal implementations:
Portal in OpenGL:
    https://en.wikibooks.org/wiki/OpenGL_Programming/Mini-Portal
Portal with render textures and recursion:
    https://danielilett.com/2019-12-14-tut4-2-portal-rendering/
    https://github.com/daniel-ilett/shaders-portal
    https://www.youtube.com/watch?v=PkGjYig8avo
Portal with render textures and recursion:
    https://github.com/SebLague/Portals/tree/master
    https://www.youtube.com/watch?v=cWpFZbjtSQg

ProBuilder: https://docs.unity3d.com/Packages/com.unity.probuilder@5.0/manual/index.html
    ProBuilder lets users build, edit, and texture custom geometry in the Unity Editor.
    UV-mapping with the UV-editor:
        Auto UVs (Simple Texturing): https://www.youtube.com/watch?v=bigj13SU1rs&list=WL&index=14&t=103s
        Manual UVs (Advanced Texturing): https://www.youtube.com/watch?v=d3_2h4cN4cY&list=WL&index=15

ProGrids: https://docs.unity3d.com/Packages/com.unity.progrids@3.0/manual/index.html
    ProGrids keeps objects aligned and evenly spaced by grid snapping. It's designed to work with ProBuilder.

Next will follow explanations of how to render Portals. To separate both portals in the descriptions, the terms
thisPortal and otherPortal are used. Every explanation from one portal's perspective is also true vice versa.

Hidden surface removal: https://gabrielgambetta.com/computer-graphics-from-scratch/12-hidden-surface-removal.html
Hidden surface removal tries to determine which parts of the scene are visible to the camera,
so only visible parts are drawn. There are 2 main solutions to this problem.
1. Painter's algorithm:
The painter's algorithm sorts the polygons within the image by their depth (distance from camera) and placing each polygon
in order from farthest to closest. This algorithms is inefficient because of the sorting required. This approach also
does not work properly when polygons overlap.
2. Depth Buffering (z-Buffering):
The Depth Buffer is inside the GPU and contains depth (z) values for every pixel on the screen.
If the current pixel is behind the pixel in the Z-buffer the pixel is rejected, otherwise it is shaded and its depth
value replaces the one in the Z-buffer.

Stencil Buffer:
The Depth Buffer is inside the GPU and contains stencil values for every pixel on the screen.
During rendering, the stencil values can be set. Later they can be compared to the stencil values of the following pixels,
to decide if they should be discarded or drawn.

RenderTexture: https://docs.unity3d.com/Manual/class-RenderTexture.html
API: https://docs.unity3d.com/ScriptReference/RenderTexture.html
Render textures are textures that can be rendered to.
One typical usage of render textures is setting them as the "target texture" property of a Camera (Camera.targetTexture).
This will make a camera render into a texture instead of rendering to the screen.
For this game, a RenderTexture is used to render the Portal.

Custom Shaders in Unity:
Unity Shader documentation: https://docs.unity3d.com/Manual/SL-VertexFragmentShaderExamples.html
Instead of writing shaders in code, shaders can also be built visually with Unity Shader Graph:
https://docs.unity3d.com/Packages/com.unity.shadergraph@12.1/manual/index.html

Portal Camera positioning:
Each portal has a camera attached to it. The camera of thisPortal is rendered to the render texture of otherPortal.
The portal camera must have the same relative position and rotational difference towards thisPortal as the player camera has
towards otherPortal, just rotated 180 degrees around thisPortal's local y-axis (up-axis).
That means if the player stands in front of otherPortal and looks into it, the portal camera will look into the back face
of thisPortal. Note that surfaces with normals that point away from the camera are invisible (back face culling).

Portal Camera rendering:
The view from the player camera of otherPortal's front face and the view from the portal camera of thisPortal's back face
have the same pixels in screen coordinates. Only the part of the portal camera's view that is seen inside
thisPortal's back face must be rendered into otherPortal's render texture.
To achieve this a custom shader is required that samples otherPortal's render texture in screen coordinates
(instead of uv coordinates). These pixels are then taken from the view of the portal camera and mapped onto
otherPortal's render texture.

The portal shader also uses a Texture Mask with an oval shape to make the render texture (rectangle) pixels
transparent outside of the oval. The shader also disables shadows on the render texture.

Clipping objects between the portal camera and thisPortal's back face:
One solution would be to set the portal camera's nearClipPlane to the distance between portal and camera.
However the camera's near clipping plane is perpendicular to the camera's forward direction, but it needs to be coplanar
with the portal surface.
The solution is to use an oblique projection matrix for the camera.
The solution was taken from: https://danielilett.com/2019-12-18-tut4-3-matrix-matching/
Camera.projectionMatrix must be set for this: https://docs.unity3d.com/ScriptReference/Camera-projectionMatrix.html
A projection matrix is a 4x4 Matrix that projects 3D coordinates from camera space into normalized device coordinates.
It also takes into consideration field of view, aspect ratio, near- and far clipping plane.
Perspective Projection Matrix: https://www.youtube.com/watch?v=U0_ONQQ5ZNM

Recursive Portal view rendering:
When otherPortal is visible in a view through otherPortal a recursive view occurs.
In the first recursion the portal camera would see the content from the previous frame in the render texture.
This would lead to a layering effect which is not desireable.
The camera is first set to the player camera position/ rotation which looks into otherPortal.
To get the otherPortal render texture content in recursion 0, the camera must go through one positioning cycle
to get the view from behind thisPortal (recursion 1). But this view also contains otherPortal and the render texture content
has to be determined again. The same positioning cycle has to be done with the starting position/ rotation
where the camera is now. This can repeat recursively.
But when taking the snapshot from recursion 1, the render texture content of recursion 2 must already be in place.
This is why the iteration must go backwards through the recursions where iteration 1 starts from the deepest
recursion (where the camera is the furthest away from thisPortal). This is the Painter's Algorithm approach.
For the otherPortal render texture content in the deepest recursion a flat texture will be used.
A better solution would be to put the scaled down view from recursion 1 into the Render Texture
seen in the deepest recursion to give an illusion of infinite recursion, but this is probably impossible.

Next to render textures, portals can also be rendered by manipulating the stencil- and depth buffer while rendering.
This approach is used by Valve. It draws opaque objects starting from the main view (recursion 0) to the deepest recursion.
Do the following operations recursively:
    1. First all opaque objects (without portal) in the main view are rendered. 
    2. Increment the stencil value of all pixels inside the portal (oval shape) from 0 to 1. From now on rendering will only
    be performed in the area where the stencil value is 1.
    3. Set the depth value of all pixels on the screen to +inf (will only affect pixels with stencil value 1).
    4. Position the portal camera (see explanations above) and render it to the screen (at stenciled pixels).
At the deepest recursion level, the view from the first recursion is used, scaled down and placed into the area with
the currently active stencil value (highest stencil value). This creates an illusion of infinite recursion.
This is not a perfect illusion however, because copying the view introduces an perspective error that is visible when
going through the portal. The copied view will snap into place in this case.
Transparent objects are drawn from deepest recursion backwards. Each iteration will decrement the current stencil values
by 1 and reset the depth values back to the current portal plane.

For Portal Colliders, Teleportation and possible next features to implement, see source code.

Simple First Person Shooter Gun holding:
The GameObject with the Gun model should be a child object of the player camera.
To place the gun at the desired relative position to the camera, the game view and the scene view can be put side by side
to directly see how it looks like.
If the player is near a wall, the gun will get clipped by the wall. This can be fixed as follows:
1. Put the Gun GameObject in a separate "Gun" layer.
2. Create a second Camera as a child of the player camera with the same properties as the player camera.
Change the following properties:
    Culling Mask: "Gun"
    Clear Flags: Depth only
    Depth: <Set to a higher value as player camera>
3. Remove the "Gun" layer from the player camera's culling mask.
This will make the second camera render the Gun over anything else in the view.

Retargeting Character Animations:
Tutorial: https://www.youtube.com/watch?v=fNgPkuMgWFg
Unity Avatar: https://docs.unity3d.com/2021.3/Documentation/Manual/ConfiguringtheAvatar.html
Unity provides an Avatar system for Humanoid Characters that can be used to map the character's bone names to a set
of standard names.
Mixamo: https://www.mixamo.com/
Mixamo provides Animations and Models that can be downloaded.
1. Import the Character Model into Unity and modify the import settings:
    Animation Type: Humanoid
    Avatar Definition: Create from this Model
The Avatar will be created and can be configured. The Bones of the character can be mapped to the Avatar bones.
Muscle constraints can be set to change the range of movement that the character has.
2. Upload Character Model on Mixamo -> Select Animation -> Download: .fbx for Unity, Without Skin
3. Import the Animation into Unity and modify the import settings:
    Animation Type: Humanoid
    Avatar Definition: Copy From other Avatar
    Source: <Avatar created earlier>
    If the Animation should be looping:
        Loop Time: true
        Loop Pose: true
4. Change Animator Component settings:
    Avatar: <Avatar created earlier>
    Apply Root Motion: false
