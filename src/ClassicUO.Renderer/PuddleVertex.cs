// SPDX-License-Identifier: BSD-2-Clause

using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PuddleVertex : IVertexType
    {
        public Vector2 Position;
        public Vector2 ScreenUV;

        public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
            new VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.Position, 0),
            new VertexElement(8, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0)
        );

        VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
    }
}
