using OpenTK.Graphics.OpenGL4;

namespace VirtualSpace.Rendering.OpenGL
{
    public sealed partial class VertexFloatBuffer
    {
        /// <summary>
        /// Sprites have the coordinate system defined from 0 - 1 (left - right) on x and 0 - 1 (top - bottom) on y
        /// </summary>
        public static class Sprite
        {
            public static VertexFloatBuffer SimpleSprite(VertexFloatBuffer buffer, float left, float right, float top, float bottom)
            {
                var vertices = new float[16];
                int currentVertex = 0;
                vertices[currentVertex++] = -1 + 2 * left; vertices[currentVertex++] = 1 - 2 * top; vertices[currentVertex++] = 0; vertices[currentVertex++] = 0; // top left
                vertices[currentVertex++] = -1 + 2 * right; vertices[currentVertex++] = 1 - 2 * top; vertices[currentVertex++] = 1; vertices[currentVertex++] = 0; // top right
                vertices[currentVertex++] = -1 + 2 * right; vertices[currentVertex++] = 1 - 2 * bottom; vertices[currentVertex++] = 1; vertices[currentVertex++] = 1; // bottom right
                vertices[currentVertex++] = -1 + 2 * left; vertices[currentVertex++] = 1 - 2 * bottom; vertices[currentVertex++] = 0; vertices[currentVertex++] = 1; // bottom left

                buffer.Set(vertices, new uint[4]);
                buffer.IndexFromLength();
                return buffer;
            }

            public static VertexFloatBuffer SimpleSprite(float left, float right, float top, float bottom)
            {
                var buffer = new VertexFloatBuffer(VertexFormat.XY_UV, 4);
                buffer.DrawMode = BeginMode.Quads;

                return SimpleSprite(buffer, left, right, top, bottom);
            }
        }
    }
}
