namespace VirtualSpace.Rendering.OpenGL
{
    public sealed partial class Shader
    {
        public static class Default
        {
            private static Shader _spriteShader_XY_UV;
            private static Shader _defaultShader_XYZ_UV;

            public static Shader SpriteShader_XY_UV
            {
                get
                {
                    if(_spriteShader_XY_UV == null)
                    {
                        string vertex_source =
                        @"#version 400
                        layout (location = 0) in vec2 vertex_position;
                        layout (location = 1) in vec2 vertex_texcoord;

                        void main(void)
                        {
                            gl_Position = vec4(vertex_position, 0.0, 1.0);
                            gl_TexCoord[0].xy = vertex_texcoord.xy;
                        }";

                        string fragment_source =
                        @"#version 400
                        layout (location = 0) out vec4 frag_color;

                        uniform sampler2D colorMap;

                        void main(void)
                        {
	                        frag_color = texture2D(colorMap, gl_TexCoord[0].xy);
                        }";
                        _spriteShader_XY_UV = new Shader(ref vertex_source, ref fragment_source, VertexFormat.XY_UV);
                    }

                    return _spriteShader_XY_UV;
                }
            }

            public static Shader DefaultShader_XYZ_UV
            {
                get
                {
                    if (_defaultShader_XYZ_UV == null)
                    {
                        string vertex_source =
                        @"#version 400
                        layout (location = 0) in vec3 vertex_position;
                        layout (location = 1) in vec2 vertex_texcoord;

                        uniform mat4 mvp_matrix;

                        void main(void)
                        {
                            gl_Position = mvp_matrix * vec4(vertex_position, 1.0);
                            gl_TexCoord[0].xy = vertex_texcoord.xy;
                        }";

                        string fragment_source =
                        @"#version 400
                        layout (location = 0) out vec4 frag_color;

                        uniform sampler2D colorMap;

                        void main(void)
                        {
	                        frag_color = texture2D(colorMap, gl_TexCoord[0].xy);
                        }";
                        _defaultShader_XYZ_UV = new Shader(ref vertex_source, ref fragment_source, VertexFormat.XYZ_UV);
                    }

                    return _defaultShader_XYZ_UV;
                }
            }
        }
    }
}
