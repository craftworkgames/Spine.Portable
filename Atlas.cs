/******************************************************************************
 * Spine Runtimes Software License
 * Version 2.1
 * 
 * Copyright (c) 2013, Esoteric Software
 * All rights reserved.
 * 
 * You are granted a perpetual, non-exclusive, non-sublicensable and
 * non-transferable license to install, execute and perform the Spine Runtimes
 * Software (the "Software") solely for internal use. Without the written
 * permission of Esoteric Software (typically granted by licensing Spine), you
 * may not (a) modify, translate, adapt or otherwise create derivative works,
 * improvements of the Software or develop new applications using the Software
 * or (b) remove, delete, alter or obscure any trademarks or any copyright,
 * trademark, patent or other intellectual property or proprietary rights
 * notices on or in the Software, including any copy thereof. Redistributions
 * in binary or source form must include this license and terms.
 * 
 * THIS SOFTWARE IS PROVIDED BY ESOTERIC SOFTWARE "AS IS" AND ANY EXPRESS OR
 * IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF
 * MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO
 * EVENT SHALL ESOTERIC SOFTARE BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS;
 * OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR
 * OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
 * ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *****************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;

namespace Spine
{
    public class Atlas
    {
        private readonly List<AtlasPage> pages = new List<AtlasPage>();
        private readonly List<AtlasRegion> regions = new List<AtlasRegion>();
        private TextureLoader textureLoader;

        private Atlas(TextReader reader, String dir, TextureLoader textureLoader)
        {
            Load(reader, dir, textureLoader);
        }

        private Atlas(List<AtlasPage> pages, List<AtlasRegion> regions)
        {
            this.pages = pages;
            this.regions = regions;
            textureLoader = null;
        }

        public static Atlas Load(Stream stream, string imagesPath, TextureLoader textureLoader)
        {
            using (var reader = new StreamReader(stream))
            {
                var atlas = new Atlas(new List<AtlasPage>(), new List<AtlasRegion>());
                atlas.Load(reader, imagesPath, textureLoader);
                return atlas;
            }
        }

        private void Load(TextReader reader, String imagesDir, TextureLoader textureLoader)
        {
            if (textureLoader == null) throw new ArgumentNullException("textureLoader cannot be null.");
            this.textureLoader = textureLoader;

            var tuple = new String[4];
            AtlasPage page = null;
            while (true)
            {
                String line = reader.ReadLine();
                if (line == null) break;
                if (line.Trim().Length == 0)
                    page = null;
                else if (page == null)
                {
                    page = new AtlasPage();
                    page.name = line;

                    if (readTuple(reader, tuple) == 2)
                    {
                        // size is only optional for an atlas packed with an old TexturePacker.
                        page.width = int.Parse(tuple[0]);
                        page.height = int.Parse(tuple[1]);
                        readTuple(reader, tuple);
                    }
                    page.format = (Format) Enum.Parse(typeof (Format), tuple[0], false);

                    readTuple(reader, tuple);
                    page.minFilter = (TextureFilter) Enum.Parse(typeof (TextureFilter), tuple[0], false);
                    page.magFilter = (TextureFilter) Enum.Parse(typeof (TextureFilter), tuple[1], false);

                    String direction = readValue(reader);
                    page.uWrap = TextureWrap.ClampToEdge;
                    page.vWrap = TextureWrap.ClampToEdge;
                    if (direction == "x")
                        page.uWrap = TextureWrap.Repeat;
                    else if (direction == "y")
                        page.vWrap = TextureWrap.Repeat;
                    else if (direction == "xy")
                        page.uWrap = page.vWrap = TextureWrap.Repeat;

                    textureLoader.Load(page, imagesDir + "/" + line); // Path.Combine(imagesDir, line));

                    pages.Add(page);
                }
                else
                {
                    var region = new AtlasRegion();
                    region.name = line;
                    region.page = page;

                    region.rotate = Boolean.Parse(readValue(reader));

                    readTuple(reader, tuple);
                    int x = int.Parse(tuple[0]);
                    int y = int.Parse(tuple[1]);

                    readTuple(reader, tuple);
                    int width = int.Parse(tuple[0]);
                    int height = int.Parse(tuple[1]);

                    region.u = x / (float) page.width;
                    region.v = y / (float) page.height;
                    if (region.rotate)
                    {
                        region.u2 = (x + height) / (float) page.width;
                        region.v2 = (y + width) / (float) page.height;
                    }
                    else
                    {
                        region.u2 = (x + width) / (float) page.width;
                        region.v2 = (y + height) / (float) page.height;
                    }
                    region.x = x;
                    region.y = y;
                    region.width = Math.Abs(width);
                    region.height = Math.Abs(height);

                    if (readTuple(reader, tuple) == 4)
                    {
                        // split is optional
                        region.splits = new[]
                        {
                            int.Parse(tuple[0]), int.Parse(tuple[1]),
                            int.Parse(tuple[2]), int.Parse(tuple[3])
                        };

                        if (readTuple(reader, tuple) == 4)
                        {
                            // pad is optional, but only present with splits
                            region.pads = new[]
                            {
                                int.Parse(tuple[0]), int.Parse(tuple[1]),
                                int.Parse(tuple[2]), int.Parse(tuple[3])
                            };

                            readTuple(reader, tuple);
                        }
                    }

                    region.originalWidth = int.Parse(tuple[0]);
                    region.originalHeight = int.Parse(tuple[1]);

                    readTuple(reader, tuple);
                    region.offsetX = int.Parse(tuple[0]);
                    region.offsetY = int.Parse(tuple[1]);

                    region.index = int.Parse(readValue(reader));

                    regions.Add(region);
                }
            }
        }

        private static String readValue(TextReader reader)
        {
            String line = reader.ReadLine();
            int colon = line.IndexOf(':');
            if (colon == -1) throw new Exception("Invalid line: " + line);
            return line.Substring(colon + 1).Trim();
        }

        /// <summary>Returns the number of tuple values read (1, 2 or 4).</summary>
        private static int readTuple(TextReader reader, String[] tuple)
        {
            String line = reader.ReadLine();
            int colon = line.IndexOf(':');
            if (colon == -1) throw new Exception("Invalid line: " + line);
            int i = 0, lastMatch = colon + 1;
            for (; i < 3; i++)
            {
                int comma = line.IndexOf(',', lastMatch);
                if (comma == -1) break;
                tuple[i] = line.Substring(lastMatch, comma - lastMatch).Trim();
                lastMatch = comma + 1;
            }
            tuple[i] = line.Substring(lastMatch).Trim();
            return i + 1;
        }

        public void FlipV()
        {
            for (int i = 0, n = regions.Count; i < n; i++)
            {
                AtlasRegion region = regions[i];
                region.v = 1 - region.v;
                region.v2 = 1 - region.v2;
            }
        }

        /// <summary>
        ///     Returns the first region found with the specified name. This method uses string comparison to find the region, so
        ///     the result
        ///     should be cached rather than calling this method multiple times.
        /// </summary>
        /// <returns>The region, or null.</returns>
        public AtlasRegion FindRegion(String name)
        {
            for (int i = 0, n = regions.Count; i < n; i++)
                if (regions[i].name == name) return regions[i];
            return null;
        }

        public void Dispose()
        {
            if (textureLoader == null) return;
            for (int i = 0, n = pages.Count; i < n; i++)
                textureLoader.Unload(pages[i].rendererObject);
        }
    }

    public enum Format
    {
        Alpha,
        Intensity,
        LuminanceAlpha,
        RGB565,
        RGBA4444,
        RGB888,
        RGBA8888
    }

    public enum TextureFilter
    {
        Nearest,
        Linear,
        MipMap,
        MipMapNearestNearest,
        MipMapLinearNearest,
        MipMapNearestLinear,
        MipMapLinearLinear
    }

    public enum TextureWrap
    {
        MirroredRepeat,
        ClampToEdge,
        Repeat
    }

    public class AtlasPage
    {
        public Format format;
        public int height;
        public TextureFilter magFilter;
        public TextureFilter minFilter;
        public String name;
        public Object rendererObject;
        public TextureWrap uWrap;
        public TextureWrap vWrap;
        public int width;
    }

    public class AtlasRegion
    {
        public int height;
        public int index;
        public String name;
        public float offsetX, offsetY;
        public int originalHeight;
        public int originalWidth;
        public int[] pads;
        public AtlasPage page;
        public bool rotate;
        public int[] splits;
        public float u;
        public float u2;
        public float v;
        public float v2;
        public int width;
        public int x, y;
    }

    public interface TextureLoader
    {
        void Load(AtlasPage page, String path);
        void Unload(Object texture);
    }
}