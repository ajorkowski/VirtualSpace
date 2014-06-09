using OpenTK.Graphics.OpenGL4;
using System;

namespace VirtualSpace.Rendering.OpenGL
{
    public sealed partial class Shader : IDisposable
    {
        public string VertexSource { get; private set; }
        public string FragmentSource { get; private set; }

        public int VertexID { get; private set; }
        public int FragmentID { get; private set; }

        public int Program { get; private set; }

        public int PositionLocation { get; set; }
        public int NormalLocation { get; set; }
        public int TexCoordLocation { get; set; }
        public int ColorLocation { get; set; }

        public VertexFormat Format { get; private set; }

        public Shader(ref string vs, ref string fs, VertexFormat format)
        {
            VertexSource = vs;
            FragmentSource = fs;
            Format = format;

            Build();
        }

        public VertexFloatBuffer CreateBuffer(int limit = 1024)
        {
            return new VertexFloatBuffer(Format, limit);
        }

        private void Build()
        {
            int status_code;
            string info;

            VertexID = GL.CreateShader(ShaderType.VertexShader);
            FragmentID = GL.CreateShader(ShaderType.FragmentShader);

            // Compile vertex shader
            GL.ShaderSource(VertexID, VertexSource);
            GL.CompileShader(VertexID);
            GL.GetShaderInfoLog(VertexID, out info);
            GL.GetShader(VertexID, ShaderParameter.CompileStatus, out status_code);

            if (status_code != 1)
                throw new ApplicationException(info);

            // Compile fragment shader
            GL.ShaderSource(FragmentID, FragmentSource);
            GL.CompileShader(FragmentID);
            GL.GetShaderInfoLog(FragmentID, out info);
            GL.GetShader(FragmentID, ShaderParameter.CompileStatus, out status_code);

            if (status_code != 1)
                throw new ApplicationException(info);

            Program = GL.CreateProgram();
            GL.AttachShader(Program, FragmentID);
            GL.AttachShader(Program, VertexID);

            GL.LinkProgram(Program);

            GL.UseProgram(Program);
            //layout dependent locations
            PositionLocation = GL.GetAttribLocation(Program, "vertex_position");
            NormalLocation = GL.GetAttribLocation(Program, "vertex_normal");
            TexCoordLocation = GL.GetAttribLocation(Program, "vertex_texcoord");
            ColorLocation = GL.GetAttribLocation(Program, "vertex_color");

            if (PositionLocation >= 0)
                GL.BindAttribLocation(Program, PositionLocation, "vertex_position");
            if (NormalLocation >= 0)
                GL.BindAttribLocation(Program, NormalLocation, "vertex_normal");
            if (TexCoordLocation >= 0)
                GL.BindAttribLocation(Program, TexCoordLocation, "vertex_texcoord");
            if (ColorLocation >= 0)
                GL.BindAttribLocation(Program, ColorLocation, "vertex_color");

            GL.UseProgram(0);
        }

        public void Dispose()
        {
            if (Program != 0)
                GL.DeleteProgram(Program);
            if (FragmentID != 0)
                GL.DeleteShader(FragmentID);
            if (VertexID != 0)
                GL.DeleteShader(VertexID);
        }
    }
}
