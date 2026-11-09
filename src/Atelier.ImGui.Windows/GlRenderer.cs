using System.Numerics;
using ImGuiNET;
using Silk.NET.Maths;
using Silk.NET.OpenGL;

namespace Atelier.Windows;

// Owns this standalone host's GL state; existing applications retain their own renderer.
internal sealed unsafe class GlRenderer : IDisposable
{
    private readonly GL gl;
    private readonly uint program;
    private readonly uint vertices;
    private readonly uint indices;
    private readonly uint vertexArray;
    private readonly uint fontTexture;
    private readonly HashSet<uint> imageTextures = [];

    public GlRenderer(GL gl)
    {
        this.gl = gl;
        var vertex = Shader(ShaderType.VertexShader, """
            #version 330 core
            layout(location=0) in vec2 position;
            layout(location=1) in vec2 uv;
            layout(location=2) in vec4 color;
            uniform mat4 projection;
            out vec2 fragUv;
            out vec4 fragColor;
            void main() { fragUv=uv; fragColor=color; gl_Position=projection*vec4(position,0,1); }
            """);
        var fragment = Shader(ShaderType.FragmentShader, """
            #version 330 core
            in vec2 fragUv;
            in vec4 fragColor;
            uniform sampler2D image;
            out vec4 outputColor;
            void main() { outputColor=fragColor*texture(image,fragUv); }
            """);
        program = gl.CreateProgram();
        gl.AttachShader(program, vertex); gl.AttachShader(program, fragment); gl.LinkProgram(program);
        gl.DeleteShader(vertex); gl.DeleteShader(fragment);
        gl.GetProgram(program, GLEnum.LinkStatus, out var linked);
        if (linked == 0) throw new InvalidOperationException(gl.GetProgramInfoLog(program));
        vertices = gl.GenBuffer(); indices = gl.GenBuffer(); vertexArray = gl.GenVertexArray();
        gl.BindVertexArray(vertexArray);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertices);
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, indices);
        gl.EnableVertexAttribArray(0); gl.EnableVertexAttribArray(1); gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 20, (void*)0);
        gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 20, (void*)8);
        gl.VertexAttribPointer(2, 4, VertexAttribPointerType.UnsignedByte, true, 20, (void*)16);
        var io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out IntPtr data, out var width, out var height, out _);
        fontTexture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, fontTexture);
        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)width, (uint)height, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, (void*)data);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        io.Fonts.SetTexID((IntPtr)fontTexture);
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
    }

    private uint Shader(ShaderType type, string source)
    {
        var shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source); gl.CompileShader(shader);
        gl.GetShader(shader, GLEnum.CompileStatus, out var compiled);
        if (compiled == 0) throw new InvalidOperationException(gl.GetShaderInfoLog(shader));
        return shader;
    }

    public void Render(ImDrawDataPtr data, Vector2D<int> framebuffer)
    {
        if (framebuffer.X <= 0 || framebuffer.Y <= 0) return;
        gl.Disable(EnableCap.ScissorTest);
        gl.ClearColor(0, 0, 0, 0);
        gl.Clear(ClearBufferMask.ColorBufferBit);
        gl.Viewport(0, 0, (uint)framebuffer.X, (uint)framebuffer.Y);
        SetState(data);
        for (var l = 0; l < data.CmdListsCount; l++)
        {
            var list = data.CmdLists[l];
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(list.VtxBuffer.Size * 20), (void*)list.VtxBuffer.Data, BufferUsageARB.StreamDraw);
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(list.IdxBuffer.Size * 2), (void*)list.IdxBuffer.Data, BufferUsageARB.StreamDraw);
            for (var c = 0; c < list.CmdBuffer.Size; c++)
            {
                var command = list.CmdBuffer[c];
                if (command.UserCallback == new IntPtr(-8)) { SetState(data); continue; }
                if (command.UserCallback != IntPtr.Zero)
                    throw new NotSupportedException("The standalone sample host does not support custom native draw callbacks.");
                var clip = command.ClipRect;
                var scale = data.FramebufferScale;
                var x = Math.Max(0, (clip.X - data.DisplayPos.X) * scale.X);
                var y = Math.Max(0, (clip.Y - data.DisplayPos.Y) * scale.Y);
                var right = Math.Min(framebuffer.X, (clip.Z - data.DisplayPos.X) * scale.X);
                var bottom = Math.Min(framebuffer.Y, (clip.W - data.DisplayPos.Y) * scale.Y);
                if (right <= x || bottom <= y) continue;
                gl.Scissor((int)x, framebuffer.Y - (int)bottom, (uint)(right - x), (uint)(bottom - y));
                gl.BindTexture(TextureTarget.Texture2D, (uint)command.TextureId);
                gl.DrawElementsBaseVertex(PrimitiveType.Triangles, command.ElemCount, DrawElementsType.UnsignedShort,
                    (void*)(command.IdxOffset * 2), (int)command.VtxOffset);
            }
        }
        gl.Disable(EnableCap.ScissorTest);
    }

    private void SetState(ImDrawDataPtr data)
    {
        gl.Enable(EnableCap.Blend); gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
        gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
        gl.Disable(EnableCap.CullFace); gl.Disable(EnableCap.DepthTest); gl.Disable(EnableCap.StencilTest);
        gl.Enable(EnableCap.ScissorTest);
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.UseProgram(program);
        gl.Uniform1(gl.GetUniformLocation(program, "image"), 0);
        var projection = Matrix4x4.CreateOrthographicOffCenter(data.DisplayPos.X, data.DisplayPos.X + data.DisplaySize.X,
            data.DisplayPos.Y + data.DisplaySize.Y, data.DisplayPos.Y, -1, 1);
        gl.UniformMatrix4(gl.GetUniformLocation(program, "projection"), 1, false, (float*)&projection);
        gl.BindVertexArray(vertexArray);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertices);
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, indices);
    }

    internal byte[] ReadPixels(Vector2D<int> size)
    {
        var bytes = new byte[size.X * size.Y * 4];
        fixed (byte* pointer = bytes)
            gl.ReadPixels(0, 0, (uint)size.X, (uint)size.Y, PixelFormat.Rgba, PixelType.UnsignedByte, pointer);
        return bytes;
    }

    internal UiImage CreateImage(int width, int height, ReadOnlySpan<byte> rgba)
    {
        if (width <= 0 || height <= 0 || (long)width * height * 4 != rgba.Length)
            throw new ArgumentException("RGBA data must contain exactly width * height * 4 bytes.");
        var texture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, texture);
        fixed (byte* pixels = rgba)
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        imageTextures.Add(texture);
        return new UiImage((IntPtr)texture, width, height);
    }

    public void Dispose()
    {
        foreach (var texture in imageTextures) gl.DeleteTexture(texture);
        imageTextures.Clear();
        gl.DeleteTexture(fontTexture); gl.DeleteVertexArray(vertexArray);
        gl.DeleteBuffer(vertices); gl.DeleteBuffer(indices); gl.DeleteProgram(program);
        gl.Dispose();
    }
}
