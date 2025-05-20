using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace AssetStudio
{
    public static class SpriteHelper
    {
        public static Image<Bgra32> GetImage(this Sprite m_Sprite)
        {
            if (m_Sprite.m_SpriteAtlas != null && m_Sprite.m_SpriteAtlas.TryGet(out var m_SpriteAtlas))
            {
                if (m_SpriteAtlas.m_RenderDataMap.TryGetValue(m_Sprite.m_RenderDataKey, out var spriteAtlasData) && spriteAtlasData.texture.TryGet(out var m_Texture2D))
                {
                    float maxHeight = 0;
                    float maxWidth = 0;
                    float minHeight = spriteAtlasData.textureRect.height;
                    float minWidth = spriteAtlasData.textureRect.width;

                    // 從 m_Sprite.m_Name 提取前綴並去掉數字
                    string characterPrefix = Regex.Replace(m_Sprite.m_Name, @"\d+$", ""); // 移除末尾數字
                    float thresholdMultiplier = 1f;

                    // 遍歷 m_PackedSprites 以獲取所有 Sprite 名稱及其尺寸
                    foreach (var spritePtr in m_SpriteAtlas.m_PackedSprites)
                    {
                        if (!spritePtr.IsNull && spritePtr.TryGet(out var sprite))
                        {
                            // 檢查 Sprite 名稱是否以 characterPrefix 開頭
                            if (sprite.m_Name.StartsWith(characterPrefix, StringComparison.OrdinalIgnoreCase))
                            {
                                // 從 m_RenderDataMap 獲取對應的 SpriteAtlasData
                                if (m_SpriteAtlas.m_RenderDataMap.TryGetValue(sprite.m_RenderDataKey, out var data))
                                {
                                    if (data.textureRect.height > maxHeight)
                                    {
                                        maxHeight = data.textureRect.height;
                                    }
                                    if (data.textureRect.width > maxWidth)
                                    {
                                        maxWidth = data.textureRect.width;
                                    }
                                }
                            }
                        }
                    }

                    Vector2 maxSize = new Vector2(maxWidth, maxHeight);

                    Console.WriteLine($"Character Prefix: {characterPrefix}");
                    Console.WriteLine($"Largest Height (Character Sprites): {maxHeight}");
                    Console.WriteLine($"Largest Width (Character Sprites): {maxWidth}");
                    return CutImage(m_Sprite, m_Texture2D, spriteAtlasData.textureRect, spriteAtlasData.textureRectOffset, spriteAtlasData.downscaleMultiplier, spriteAtlasData.settingsRaw, maxSize);
                }
            }
            else
            {
                if (m_Sprite.m_RD.texture.TryGet(out var m_Texture2D))
                {
                    return CutImage(m_Sprite, m_Texture2D, m_Sprite.m_RD.textureRect, m_Sprite.m_RD.textureRectOffset, m_Sprite.m_RD.downscaleMultiplier, m_Sprite.m_RD.settingsRaw);
                }
            }
            return null;
        }

        private static Image<Bgra32> CutImage(Sprite m_Sprite, Texture2D m_Texture2D, Rectf textureRect, Vector2 textureRectOffset, float downscaleMultiplier, SpriteSettings settingsRaw, Vector2 largestSpriteSize = default)
        {
            var originalImage = m_Texture2D.ConvertToImage(false);
            if (originalImage != null)
            {
                using (originalImage)
                {
                    if (downscaleMultiplier > 0f && downscaleMultiplier != 1f)
                    {
                        var width = (int)(m_Texture2D.m_Width / downscaleMultiplier);
                        var height = (int)(m_Texture2D.m_Height / downscaleMultiplier);
                        originalImage.Mutate(x => x.Resize(width, height));
                    }

                    // 使用 m_Rect 的尺寸進行裁剪
                    var rectX = (int)Math.Floor(textureRect.x);
                    var rectY = (int)Math.Floor(textureRect.y);
                    var targetWidth = (int)Math.Ceiling(textureRect.width);
                    var targetHeight = (int)Math.Ceiling(textureRect.height);
                    var rectRight = Math.Min(rectX + targetWidth, originalImage.Width);
                    var rectBottom = Math.Min(rectY + targetHeight, originalImage.Height);
                    var rect = new Rectangle(rectX, rectY, rectRight - rectX, rectBottom - rectY);

                    var spriteImage = originalImage.Clone(x => x.Crop(rect));

                    // 創建包含偏移的畫布
                    var canvasWidth = (int)m_Sprite.m_Rect.width;
                    var canvasHeight = (int)m_Sprite.m_Rect.height;
                    var unifiedImage = new Image<Bgra32>(canvasWidth, canvasHeight, SixLabors.ImageSharp.Color.Transparent);

                    // 計算樞軸點並定位
                    Vector2 pivotPosition = new Vector2(
                        m_Sprite.m_Rect.width * m_Sprite.m_Pivot.X + m_Sprite.m_Offset.X,
                        m_Sprite.m_Rect.height * m_Sprite.m_Pivot.Y + m_Sprite.m_Offset.Y
                    );
                    Vector2 adjustedPivot = pivotPosition - textureRectOffset;
                    var placeX = (int)Math.Round((canvasWidth * 0.5f - adjustedPivot.X));
                    var placeY = (int)Math.Round(-adjustedPivot.Y + (adjustedPivot.Y < 0 ? -largestSpriteSize.Y / 2 : largestSpriteSize.Y / 2));

                    unifiedImage.Mutate(x => x.DrawImage(spriteImage, new Point(placeX, (int)-adjustedPivot.Y+ (int)pivotPosition.Y), 1f));

                    // 處理旋轉與翻轉
                    if (settingsRaw.packed == 1)
                    {
                        switch (settingsRaw.packingRotation)
                        {
                            case SpritePackingRotation.FlipHorizontal:
                                unifiedImage.Mutate(x => x.Flip(FlipMode.Horizontal));
                                break;
                            case SpritePackingRotation.FlipVertical:
                                unifiedImage.Mutate(x => x.Flip(FlipMode.Vertical));
                                break;
                            case SpritePackingRotation.Rotate180:
                                unifiedImage.Mutate(x => x.Rotate(180));
                                break;
                            case SpritePackingRotation.Rotate90:
                                unifiedImage.Mutate(x => x.Rotate(270));
                                break;
                        }
                    }

                    // 處理緊密包裝
                    if (settingsRaw.packingMode == SpritePackingMode.Tight)
                    {
                        try
                        {
                            var triangles = GetTriangles(m_Sprite.m_RD);
                            var polygons = triangles.Select(x => new Polygon(new LinearLineSegment(x.Select(y => new PointF(y.X, y.Y)).ToArray()))).ToArray();
                            IPathCollection path = new PathCollection(polygons);
                            var matrix = Matrix3x2.CreateTranslation(
                                m_Sprite.m_Rect.width * m_Sprite.m_Pivot.X - textureRectOffset.X,
                                m_Sprite.m_Rect.height * m_Sprite.m_Pivot.Y - textureRectOffset.Y
                            );
                            path = path.Transform(matrix);
                            var options = new DrawingOptions
                            {
                                GraphicsOptions = new GraphicsOptions
                                {
                                    Antialias = false,
                                    AlphaCompositionMode = PixelAlphaCompositionMode.DestOut
                                }
                            };
                            using (var mask = new Image<Bgra32>(rect.Width, rect.Height, SixLabors.ImageSharp.Color.Black))
                            {
                                mask.Mutate(x => x.Fill(options, SixLabors.ImageSharp.Color.Red, path));
                                var brush = new ImageBrush(mask);
                                spriteImage.Mutate(x => x.Fill(options, brush));
                                spriteImage.Mutate(x => x.Flip(FlipMode.Vertical));
                                return spriteImage;
                            }
                        }
                        catch
                        {
                            // 記錄錯誤以便調試
                            Console.WriteLine($"緊密包裝處理失敗: {m_Sprite.m_Name}");
                        }
                    }

                    unifiedImage.Mutate(x => x.Flip(FlipMode.Vertical));
                    return unifiedImage;
                }
            }
            return null;
        }

        private static Vector2[][] GetTriangles(SpriteRenderData m_RD)
        {
            if (m_RD.vertices != null) //5.6 down
            {
                var vertices = m_RD.vertices.Select(x => (Vector2)x.pos).ToArray();
                var triangleCount = m_RD.indices.Length / 3;
                var triangles = new Vector2[triangleCount][];
                for (int i = 0; i < triangleCount; i++)
                {
                    var first = m_RD.indices[i * 3];
                    var second = m_RD.indices[i * 3 + 1];
                    var third = m_RD.indices[i * 3 + 2];
                    var triangle = new[] { vertices[first], vertices[second], vertices[third] };
                    triangles[i] = triangle;
                }
                return triangles;
            }
            else //5.6 and up
            {
                var triangles = new List<Vector2[]>();
                var m_VertexData = m_RD.m_VertexData;
                var m_Channel = m_VertexData.m_Channels[0]; //kShaderChannelVertex
                var m_Stream = m_VertexData.m_Streams[m_Channel.stream];
                using (var vertexReader = new EndianBinaryReader(new MemoryStream(m_VertexData.m_DataSize), EndianType.LittleEndian))
                {
                    using (var indexReader = new EndianBinaryReader(new MemoryStream(m_RD.m_IndexBuffer), EndianType.LittleEndian))
                    {
                        foreach (var subMesh in m_RD.m_SubMeshes)
                        {
                            vertexReader.BaseStream.Position = m_Stream.offset + subMesh.firstVertex * m_Stream.stride + m_Channel.offset;

                            var vertices = new Vector2[subMesh.vertexCount];
                            for (int v = 0; v < subMesh.vertexCount; v++)
                            {
                                vertices[v] = new Vector3(vertexReader.ReadSingle(), vertexReader.ReadSingle(), vertexReader.ReadSingle());
                                vertexReader.BaseStream.Position += m_Stream.stride - 12;
                            }

                            indexReader.BaseStream.Position = subMesh.firstByte;

                            var triangleCount = subMesh.indexCount / 3u;
                            for (int i = 0; i < triangleCount; i++)
                            {
                                var first = indexReader.ReadUInt16() - subMesh.firstVertex;
                                var second = indexReader.ReadUInt16() - subMesh.firstVertex;
                                var third = indexReader.ReadUInt16() - subMesh.firstVertex;
                                var triangle = new[] { vertices[first], vertices[second], vertices[third] };
                                triangles.Add(triangle);
                            }
                        }
                    }
                }
                return triangles.ToArray();
            }
        }
    }
}
