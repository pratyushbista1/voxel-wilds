using System;
using System.Collections.Generic;
using UnityEngine;
using VoxelWilds.Core;

namespace VoxelWilds
{
    public sealed class ItemIconAtlas : IDisposable
    {
        public const int Resolution = 32;
        private readonly Dictionary<int, Texture2D> icons = new Dictionary<int, Texture2D>();
        private Color32[] blockPixels;
        private bool disposed;

        public Texture2D Get(int id)
        {
            if (disposed || !Items.Exists(id)) return null;
            if (icons.TryGetValue(id, out var existing)) return existing;
            var p = new Sprite();
            if (id <= (int)Block.EndFrame) PaintBlock(p, (Block)id);
            else PaintItem(p, id);
            p.Outline();
            var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false, false)
            {
                name = "Inventory " + Items.Name(id),
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(p.Pixels);
            texture.Apply(false, false);
            icons.Add(id, texture);
            return texture;
        }

        public void Dispose()
        {
            if (disposed) return;
            foreach (var icon in icons.Values) Release(icon);
            icons.Clear();
            blockPixels = null;
            disposed = true;
        }

        private static void Release(Texture2D texture)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
            else UnityEngine.Object.DestroyImmediate(texture);
        }

        private void PaintBlock(Sprite p, Block block)
        {
            if (Blocks.IsBed(block)) { Bed(p); return; }
            switch (block)
            {
                case Block.Torch: Torch(p); return;
                case Block.Lantern: Lantern(p); return;
                case Block.Campfire: Campfire(p); return;
                case Block.Door: Door(p); return;
                case Block.Crop: Wheat(p, true); return;
                case Block.NetherWart: Wart(p); return;
                case Block.Chest: Cube(p, block, 10, 12); return;
                default: Cube(p, block, 8, 14); return;
            }
        }

        private void Cube(Sprite p, Block block, int top, int depth)
        {
            if (blockPixels == null)
            {
                var atlas = BlockTextureAtlas.Create();
                blockPixels = atlas.GetPixels32();
                Release(atlas);
            }
            Face(p, block, 0, 3, top, 13, 7, 0, depth, .72f);
            Face(p, block, 5, 16, top + 7, 13, -7, 0, depth, .87f);
            Face(p, block, 2, 16, top - 7, 13, 7, -13, 7, 1f);
        }

        private void Face(Sprite p, Block block, int face, float ox, float oy, float ax, float ay, float bx, float by, float light)
        {
            float determinant = ax * by - ay * bx;
            int tile = BlockTextureAtlas.TileFor(block, face);
            int atlasSize = BlockTextureAtlas.Columns * BlockTextureAtlas.CellSize;
            int tx = tile % BlockTextureAtlas.Columns * BlockTextureAtlas.CellSize + BlockTextureAtlas.Padding;
            int ty = tile / BlockTextureAtlas.Columns * BlockTextureAtlas.CellSize + BlockTextureAtlas.Padding;
            for (int y = 0; y < Resolution; y++) for (int x = 0; x < Resolution; x++)
            {
                float dx = x + .5f - ox, dy = y + .5f - oy;
                float u = (dx * by - dy * bx) / determinant, v = (ax * dy - ay * dx) / determinant;
                if (u < 0 || v < 0 || u >= 1 || v >= 1) continue;
                int sampleX = tx + Mathf.Clamp((int)(u * 16), 0, 15);
                int sampleY = ty + 15 - Mathf.Clamp((int)(v * 16), 0, 15);
                Color32 color = blockPixels[sampleX + sampleY * atlasSize];
                if (color.a == 0) continue;
                color.r = (byte)Mathf.Min(255, color.r * light);
                color.g = (byte)Mathf.Min(255, color.g * light);
                color.b = (byte)Mathf.Min(255, color.b * light);
                if (block == Block.Glass) color.a = (byte)Mathf.Max(100, (int)color.a);
                p.Pixel(x, y, color);
            }
        }

        private static void PaintItem(Sprite p, int id)
        {
            if (Items.SpawnMob(id) != null) { Egg(p, id - Items.ZombieEgg); return; }
            switch (id)
            {
                case Items.WoodenPickaxe: Pickaxe(p, 0xb58a52, 0xe1b677, 0x795635); break;
                case Items.StonePickaxe: Pickaxe(p, 0x89958f, 0xc5cfc3, 0x505e5c); break;
                case Items.IronPickaxe: Pickaxe(p, 0xcbd6d2, 0xf4f7e9, 0x839893); break;
                case Items.CrystalPickaxe: Pickaxe(p, 0x59c7c2, 0xc0fff0, 0x287b8d); break;
                case Items.IronSword: Sword(p, 0xcbd6d2, 0xf4f7e9, 0x829893); break;
                case Items.CrystalSword: Sword(p, 0x59c7c2, 0xc0fff0, 0x287b8d); break;
                case Items.IronAxe: Axe(p); break;
                case Items.IronShovel: Shovel(p); break;
                case Items.IronHoe: Hoe(p); break;
                case Items.IronHelmet: Helmet(p, 0xbdccc7, 0xf3f3e2, 0x768d8a); break;
                case Items.GoldHelmet: Helmet(p, 0xe6b53e, 0xffec94, 0xa66a27); break;
                case Items.IronChestplate: Chestplate(p); break;
                case Items.IronLeggings: Leggings(p); break;
                case Items.IronBoots: Boots(p); break;
                case Items.Shield: Shield(p); break;
                case Items.Stick: Handle(p, 6, 25, 24, 7); break;
                case Items.Coal: Coal(p); break;
                case Items.IronIngot: Ingot(p, 0xc9d1c9, 0xf2f5e5, 0x83988d); break;
                case Items.GoldIngot: Ingot(p, 0xe8b84b, 0xffed96, 0xb17a26); break;
                case Items.Brick: Ingot(p, 0xba7151, 0xe7a477, 0x854c3a); break;
                case Items.NetherBrick: Ingot(p, 0x774349, 0xa56665, 0x4b303c); break;
                case Items.IronNugget: Nugget(p, 0xc6d2c7, 0xf3f7e2, 0x7d948e); break;
                case Items.GoldNugget: Nugget(p, 0xe8bd42, 0xffed8c, 0xa47427); break;
                case Items.RawIron: RawIron(p); break;
                case Items.ClayBall: Clay(p); break;
                case Items.Crystal: Crystal(p, false); break;
                case Items.Quartz: Crystal(p, true); break;
                case Items.Berries: Berries(p); break;
                case Items.String: String(p); break;
                case Items.RawMutton: Meat(p, 0xc66062, 0xf1b098, 0x943d4a, 0); break;
                case Items.CookedMutton: Meat(p, 0xa4633d, 0xd79955, 0x683c30, 0); break;
                case Items.RawBeef: Meat(p, 0xb94750, 0xf1a29a, 0x7f2c42, 1); break;
                case Items.Steak: Meat(p, 0x905132, 0xca8650, 0x603728, 1); break;
                case Items.RawPorkchop: Meat(p, 0xe99b9a, 0xffd7bd, 0xb66874, 2); break;
                case Items.CookedPorkchop: Meat(p, 0xc28b51, 0xf1c481, 0x855332, 2); break;
                case Items.RawChicken: Chicken(p, false); break;
                case Items.CookedChicken: Chicken(p, true); break;
                case Items.RottenFlesh: RottenFlesh(p); break;
                case Items.Flint: Flint(p); break;
                case Items.FlintSteel: FlintSteel(p); break;
                case Items.Leather: Leather(p); break;
                case Items.Feather: Feather(p); break;
                case Items.Bone: Bone(p); break;
                case Items.BlazeRod: BlazeRod(p); break;
                case Items.EmptyBucket: Bucket(p, 0); break;
                case Items.WaterBucket: Bucket(p, 0x478fd8); break;
                case Items.LavaBucket: Bucket(p, 0xf89029); break;
                case Items.Bow: Bow(p); break;
                case Items.Arrow: Arrow(p); break;
                case Items.EnderPearl: Pearl(p, false); break;
                case Items.EyeEnder: Pearl(p, true); break;
                case Items.BlazePowder: BlazePowder(p); break;
                case Items.Wheat: Wheat(p, false); break;
                case Items.Bread: Bread(p); break;
                case Items.Seeds: Seeds(p); break;
                case Items.Apple: Apple(p); break;
            }
        }

        private static void Handle(Sprite p, int x0, int y0, int x1, int y1)
        {
            p.Line(x0, y0, x1, y1, 4, 0x68492e);
            p.Line(x0 - 1, y0 - 1, x1 - 1, y1 - 1, 2, 0xbc8b50);
            p.Line(x0, y0 - 2, x1 - 1, y1 - 2, 1, 0xe0b779);
        }

        private static void Pickaxe(Sprite p, uint face, uint shine, uint dark)
        {
            Handle(p, 6, 26, 21, 11);
            p.Poly(dark, 7,4, 17,4, 27,14, 27,24, 23,20, 23,15, 16,8, 11,8);
            p.Poly(face, 8,4, 16,4, 26,14, 26,21, 24,19, 24,14, 16,6, 10,6);
            p.Line(9,4,16,4,1,shine); p.Line(17,5,25,13,1,shine);
            p.Rect(18,10,3,3,0x896333);
        }

        private static void Sword(Sprite p, uint face, uint shine, uint dark)
        {
            Handle(p, 6,26,12,20);
            p.Poly(dark, 11,18, 23,6, 28,4, 26,10, 16,21);
            p.Poly(face, 12,17, 23,6, 27,5, 25,10, 15,20);
            p.Line(13,17,25,5,2,shine); p.Line(16,19,26,9,1,dark);
            p.Line(9,16,18,25,3,0x677574); p.Line(9,15,18,24,1,shine);
            p.Rect(3,26,4,3,dark); p.Rect(3,26,3,1,shine);
        }

        private static void Axe(Sprite p)
        {
            Handle(p,7,26,22,10);
            p.Poly(0x80958f, 11,5, 19,4, 26,10, 27,15, 23,19, 18,19, 18,14, 13,12, 9,9);
            p.Poly(0xd0d9d0, 12,5, 19,5, 25,10, 25,15, 22,17, 19,17, 19,12, 14,11, 11,8);
            p.Line(12,5,18,5,1,0xf7f5e6); p.Line(25,11,25,15,1,0xf7f5e6); p.Rect(20,10,3,3,0x8c6741);
        }

        private static void Shovel(Sprite p)
        {
            Handle(p,6,26,20,12);
            p.Poly(0x84998f, 17,7, 24,4, 28,8, 27,15, 23,20, 17,18, 14,14);
            p.Poly(0xd1dcd0, 18,7, 24,5, 27,8, 26,14, 22,18, 18,16, 16,13);
            p.Line(18,8,23,6,2,0xf4f6e3); p.Line(19,16,26,9,1,0xa5b9ac);
        }

        private static void Hoe(Sprite p)
        {
            Handle(p,7,26,21,11);
            p.Poly(0x7c918b, 9,4, 15,4, 23,10, 26,15, 23,18, 21,13, 14,8, 9,8);
            p.Poly(0xc9d6ca, 10,4, 15,5, 23,11, 24,15, 22,13, 14,7, 10,7);
            p.Line(10,4,14,4,1,0xf4f7e6); p.Line(16,6,22,10,1,0xf4f7e6);
        }

        private static void Helmet(Sprite p, uint face, uint shine, uint dark)
        {
            p.Poly(dark, 7,8, 11,5, 22,5, 26,9, 27,23, 22,26, 20,23, 20,18, 12,18, 12,25, 6,23);
            p.Poly(face, 8,8, 12,6, 22,6, 25,9, 25,22, 22,23, 22,15, 10,15, 10,23, 7,22);
            p.Rect(10,8,12,2,shine); p.Rect(8,10,3,6,shine); p.Rect(22,10,3,3,dark);
            p.Rect(14,16,5,2,0x384d4c); p.Pixel(12,7,shine);
        }

        private static void Chestplate(Sprite p)
        {
            p.Poly(0x79958c, 7,5, 11,4, 12,8, 19,8, 21,4, 25,5, 29,14, 24,18, 22,15, 22,27, 10,27, 10,15, 7,18, 3,14);
            p.Poly(0xc6d5c7, 7,6, 10,5, 11,10, 20,10, 22,5, 24,6, 27,14, 24,16, 21,12, 21,25, 11,25, 11,12, 7,16, 5,14);
            p.Line(7,7,5,12,2,0xf2f4dd); p.Rect(12,11,7,2,0xf2f4dd); p.Rect(12,14,2,8,0xe2e9d7);
            p.Rect(19,16,2,9,0xa2b9ab); p.Rect(11,24,10,2,0x9cb7a7);
        }

        private static void Leggings(Sprite p)
        {
            p.Poly(0x779086, 7,5, 25,5, 24,27, 18,27, 17,16, 15,16, 14,27, 7,27);
            p.Rect(8,6,16,6,0xc8d6c9); p.Rect(8,6,16,2,0xf3f2df);
            p.Poly(0xc6d4c6, 8,13, 15,13, 13,25, 8,25); p.Poly(0xb0c5b5, 18,13, 23,13, 23,25, 19,25);
            p.Rect(9,14,2,9,0xe8eedd); p.Rect(19,14,1,10,0xdfe8d5); p.Rect(15,9,3,2,0x7d968b);
        }

        private static void Boots(Sprite p)
        {
            for (int i=0;i<2;i++)
            {
                int x=5+i*13; p.Poly(0x738c83, x+2,7, x+9,7, x+9,24, x-1,24, x-1,19, x+2,17);
                p.Rect(x+3,8,5,11,0xc9d7c9); p.Rect(x,19,8,4,0xb3c9b8);
                p.Rect(x+3,8,5,2,0xf1f4e1); p.Rect(x+3,11,2,8,0xe4eddb); p.Rect(x,20,4,1,0xf1f4e1);
            }
        }

        private static void Shield(Sprite p)
        {
            p.Poly(0x8e9a91, 5,5, 25,5, 25,19, 22,24, 15,28, 8,24, 5,19);
            p.Poly(0x835c38, 7,7, 23,7, 23,18, 20,23, 15,26, 10,23, 7,18);
            p.Poly(0xbd8e54, 8,8, 22,8, 22,18, 19,22, 15,24, 11,22, 8,18);
            p.Line(11,8,11,21,1,0x866038); p.Line(18,8,18,22,1,0x866038);
            p.Rect(8,8,13,2,0xd5ad72); p.Rect(13,13,5,6,0x818f83); p.Rect(14,13,3,4,0xdbe1c9);
            p.Rect(6,6,18,1,0xe5e8d6); p.Pixel(8,22,0xd9decb); p.Pixel(22,22,0xd9decb);
        }

        private static void Coal(Sprite p)
        {
            p.Poly(0x252c32, 6,12, 12,6, 21,4, 27,12, 26,21, 19,26, 9,24, 4,19);
            p.Poly(0x4d5555, 6,12, 12,7, 21,5, 24,12, 16,16, 8,17);
            p.Poly(0x343d41, 16,17, 25,13, 24,21, 18,24, 10,22);
            p.Line(9,12,13,8,2,0x717872); p.Line(14,8,20,7,1,0x8a9084); p.Rect(7,18,3,2,0x59635d);
        }

        private static void Ingot(Sprite p, uint face, uint shine, uint dark)
        {
            p.Poly(dark, 4,15, 10,8, 24,8, 28,14, 27,20, 10,25, 4,21);
            p.Poly(face, 5,15, 11,9, 24,9, 27,14, 10,20); p.Poly(shine, 7,14, 11,10, 23,10, 24,12, 10,17);
            p.Poly(face, 11,21, 26,16, 26,19, 11,23); p.Line(5,17,9,20,1,shine);
            p.Line(12,11,20,11,1,0xfff5db);
        }

        private static void Nugget(Sprite p, uint face, uint shine, uint dark)
        {
            p.Poly(dark, 9,10, 18,7, 24,11, 23,20, 17,25, 9,21, 7,16);
            p.Poly(face, 10,11, 18,8, 22,11, 22,18, 17,22, 10,19, 9,16);
            p.Poly(shine, 10,11, 18,9, 19,12, 15,17, 10,16); p.Rect(11,11,4,2,0xfff4d2); p.Pixel(20,17,shine);
        }

        private static void RawIron(Sprite p)
        {
            p.Poly(0x706f63, 5,11, 12,6, 19,8, 24,6, 27,14, 25,22, 18,25, 8,24, 4,19);
            p.Poly(0xb29d7a, 7,11, 12,8, 20,10, 24,8, 25,16, 20,22, 9,22, 6,18);
            p.Poly(0xdfb992, 8,11, 14,10, 17,14, 14,18, 8,16); p.Rect(9,11,4,2,0xf3d5ae);
            p.Poly(0xd6a881, 18,17, 23,15, 24,19, 20,22, 17,20); p.Rect(18,17,3,2,0xf0cfa6);
        }

        private static void Clay(Sprite p)
        {
            p.Poly(0x788b92, 5,13, 9,8, 18,6, 24,10, 27,18, 23,24, 11,25, 6,21);
            p.Poly(0xa7b9bb, 6,13, 10,9, 18,7, 23,11, 25,17, 20,21, 10,22, 7,18);
            p.Poly(0xd0d9cf, 8,13, 12,10, 18,9, 21,12, 15,14, 10,17); p.Line(13,23,21,22,1,0x637b88);
        }

        private static void Crystal(Sprite p, bool quartz)
        {
            uint dark=quartz?0x999ba4U:0x256d82U, face=quartz?0xdedcceU:0x52c6c2U, shine=quartz?0xfff8e2U:0xc5fff0U;
            p.Poly(dark, 10,9, 18,3, 25,12, 22,24, 14,28, 7,21);
            p.Poly(face, 11,10, 18,4, 23,12, 21,23, 14,26, 9,21);
            p.Poly(shine, 11,10, 18,5, 17,14, 14,24, 11,20);
            p.Poly(dark, 18,14, 23,12, 21,23, 15,26); p.Line(19,6,22,11,1,shine);
            if (quartz) { p.Poly(0xb6b6bc, 5,18, 8,12, 11,18, 11,25, 7,23); p.Line(7,17,8,14,1,shine); }
            else { p.Rect(20,15,2,3,0x79e4d4); p.Pixel(24,5,0xd4fff3); }
        }

        private static void Berries(Sprite p)
        {
            p.Line(14,15,18,5,2,0x64763b); p.Poly(0x87a64a, 17,8, 18,4, 25,4, 22,8); p.Poly(0x4f7b35, 14,10, 7,6, 7,11, 13,14);
            Berry(p,9,17,0xad304e); Berry(p,19,15,0xa53362); Berry(p,16,23,0xc54260);
        }
        private static void Berry(Sprite p, int x, int y, uint color)
        {
            p.Disc(x,y,5,color); p.Rect(x-2,y-3,3,2,0xf19a9a); p.Rect(x+2,y+2,2,2,0x792e48); p.Pixel(x,y-5,0x496b3b);
        }

        private static void String(Sprite p)
        {
            p.Line(9,10,19,5,2,0x899f97); p.Line(19,5,25,10,2,0xecf0d9); p.Line(25,10,21,17,2,0xecf0d9);
            p.Line(21,17,9,19,2,0xbacabb); p.Line(9,19,6,14,2,0xecf0d9); p.Line(6,14,12,9,2,0xecf0d9);
            p.Line(11,11,17,9,1,0xffffff); p.Line(10,19,12,24,2,0xecf0d9); p.Line(12,24,21,23,2,0xbacabb); p.Line(21,23,24,27,2,0xecf0d9);
        }

        private static void Meat(Sprite p, uint face, uint shine, uint dark, int shape)
        {
            if (shape==0)
            {
                p.Line(12,23,6,27,4,0xd9d4b6); p.Rect(3,25,4,3,0xffefca);
                p.Poly(dark, 8,15, 11,7, 19,4, 25,9, 26,18, 20,24, 13,24, 8,20);
                p.Poly(face, 10,15, 13,8, 19,6, 24,10, 24,18, 19,22, 13,21);
                p.Line(14,9,19,7,2,shine); p.Line(11,17,14,21,2,shine); p.Rect(16,14,4,5,dark); p.Rect(17,14,2,3,shine);
            }
            else if(shape==1)
            {
                p.Poly(shine, 4,15, 9,7, 18,5, 26,10, 27,17, 23,24, 12,27, 5,22);
                p.Poly(dark, 6,15, 10,9, 18,7, 24,11, 25,17, 21,23, 12,25, 7,21);
                p.Poly(face, 7,15, 11,10, 18,8, 23,12, 24,17, 20,22, 12,23, 8,20);
                p.Line(11,12,13,20,2,shine); p.Line(17,10,19,18,2,dark); p.Line(20,19,16,22,1,shine);
            }
            else
            {
                p.Poly(shine, 4,12, 10,6, 19,5, 26,9, 28,15, 24,20, 18,19, 14,25, 7,24, 4,19);
                p.Poly(dark, 6,12, 11,8, 19,7, 25,11, 26,15, 23,18, 17,17, 13,23, 8,22, 6,18);
                p.Poly(face, 7,12, 12,9, 19,8, 24,11, 25,15, 21,16, 16,16, 12,21, 8,20);
                p.Rect(10,11,5,2,shine); p.Line(18,10,22,12,2,shine); p.Rect(9,17,3,3,dark);
            }
        }

        private static void Chicken(Sprite p, bool cooked)
        {
            uint face=cooked?0xb67e42U:0xe4b49fU, shine=cooked?0xefba6cU:0xffe0c6U, dark=cooked?0x744934U:0xbb8583U;
            p.Line(9,22,5,27,4,0xe9dbb8); p.Rect(3,25,3,4,0xffedc6); p.Rect(5,27,3,2,0xffedc6);
            p.Poly(dark, 7,18, 11,10, 16,8, 23,10, 27,17, 25,24, 17,27, 10,24);
            p.Poly(face, 9,18, 12,11, 17,9, 22,11, 25,17, 23,23, 17,25, 11,23);
            p.Poly(shine, 11,17, 14,12, 18,11, 21,13, 17,15, 15,20); p.Line(20,18,22,21,2,dark);
            p.Line(23,11,26,7,3,face); p.Rect(24,5,4,3,0xf2dbba);
        }

        private static void RottenFlesh(Sprite p)
        {
            p.Poly(0x70543c, 8,4, 15,7, 22,5, 26,11, 23,16, 26,22, 19,27, 13,23, 6,25, 4,18, 8,13);
            p.Poly(0xa1764a, 9,6, 15,9, 21,7, 24,11, 21,16, 23,22, 19,24, 13,21, 8,23, 6,18, 10,13);
            p.Rect(10,9,4,4,0xc49062); p.Rect(17,18,4,4,0x667544); p.Rect(8,18,3,3,0x667544); p.Line(15,12,19,15,2,0x813e34);
        }

        private static void Flint(Sprite p)
        {
            p.Poly(0x303e48, 5,20, 13,8, 24,4, 27,13, 23,20, 13,26, 7,25);
            p.Poly(0x61717a, 7,19, 14,9, 23,6, 24,13, 18,19, 11,23, 7,23);
            p.Poly(0x8c9a9d, 9,17, 15,10, 22,7, 19,12, 13,17); p.Line(20,18,24,13,1,0x455b68);
        }

        private static void FlintSteel(Sprite p)
        {
            p.Line(11,10,17,5,4,0x899e9c); p.Line(17,5,25,8,4,0xe0e7d9); p.Line(25,8,26,19,4,0xc2d2c5);
            p.Line(26,19,20,25,4,0x91a59b); p.Line(20,25,16,22,3,0xd8e2d0); p.Line(18,6,23,8,1,0xffffff);
            p.Poly(0x344753, 4,22, 8,12, 14,10, 17,18, 13,26, 7,27);
            p.Poly(0x768b94, 6,21, 9,14, 13,12, 14,17, 10,23); p.Rect(17,12,2,2,0xffd47a); p.Pixel(19,10,0xffedab);
        }

        private static void Leather(Sprite p)
        {
            p.Poly(0x754c33, 8,4, 13,8, 18,8, 23,4, 28,9, 23,14, 24,20, 28,24, 23,28, 18,24, 12,24, 7,28, 3,23, 7,19, 8,14, 4,10);
            p.Poly(0xbb8550, 9,6, 13,10, 19,10, 23,6, 25,9, 21,14, 22,21, 25,24, 23,25, 18,22, 12,22, 8,25, 6,23, 9,19, 10,14, 7,10);
            p.Line(12,12,11,18,2,0xd5a66b); p.Rect(13,11,6,2,0xd5a66b); p.Line(18,14,19,19,1,0x9b683f);
        }

        private static void Feather(Sprite p)
        {
            p.Poly(0xa6bbad, 6,24, 7,17, 11,10, 19,5, 27,4, 26,11, 22,15, 20,14, 20,18, 14,22);
            p.Poly(0xebeed8, 8,22, 9,16, 13,10, 20,6, 25,5, 24,10, 19,14, 17,18, 12,21);
            p.Line(5,27,23,7,2,0xc6cba6); p.Line(10,20,10,15,1,0xffffff); p.Line(14,16,15,10,1,0xffffff); p.Line(18,12,19,8,1,0xffffff);
        }

        private static void Bone(Sprite p)
        {
            p.Line(8,24,23,9,5,0xc1c6a4); p.Line(7,23,22,8,3,0xf3f0ce);
            p.Rect(4,22,5,5,0xf3f0ce); p.Rect(7,25,5,4,0xe1e1ba); p.Rect(20,4,5,5,0xf9f4d5); p.Rect(23,7,5,5,0xe1e1ba);
            p.Line(9,21,20,10,1,0xffffff); p.Rect(5,23,2,2,0xffffff); p.Rect(21,5,2,2,0xffffff);
        }

        private static void BlazeRod(Sprite p)
        {
            p.Line(6,26,25,6,5,0xc17326); p.Line(5,25,24,5,3,0xf4bb43); p.Line(5,24,23,5,1,0xffed97);
            p.Line(10,23,12,20,1,0x9e5425); p.Line(17,16,19,13,1,0x9e5425); p.Rect(24,4,3,3,0xffe881);
        }

        private static void Bucket(Sprite p, uint fluid)
        {
            p.Poly(0x7c9392, 5,8, 27,8, 24,25, 20,28, 11,28, 7,24);
            p.Poly(0xc0d0c8, 7,10, 25,10, 22,24, 19,26, 12,26, 9,23);
            p.Poly(0x839e9b, 18,13, 25,10, 22,24, 19,26, 17,26);
            p.Rect(7,6,18,2,0xc9dbd1); p.Rect(5,8,2,4,0xf0f3de); p.Rect(25,8,2,4,0x647f80);
            p.Rect(7,8,18,5,fluid==0?0x455e65U:fluid); p.Rect(9,8,13,2,fluid==0?0x6f898aU:fluid==0x478fd8?0x81cbefU:0xffdb71U);
            if(fluid!=0) { p.Rect(8,11,4,1,fluid==0x478fd8?0xb5e6f1U:0xffef9fU); p.Rect(18,10,4,1,fluid==0x478fd8?0x3773b9U:0xe45822U); }
            p.Line(10,15,11,22,2,0xf0f3df); p.Rect(13,24,5,1,0xdce7cd);
        }

        private static void Bow(Sprite p)
        {
            p.Line(9,4,9,27,1,0xe1d8ac);
            p.Line(10,4,20,9,4,0x7f542f); p.Line(20,9,25,15,4,0x7f542f); p.Line(25,15,20,22,4,0x7f542f); p.Line(20,22,10,27,4,0x7f542f);
            p.Line(10,4,20,9,1,0xddb877); p.Line(20,9,24,15,1,0xddb877); p.Line(24,15,20,21,1,0xba8a4e); p.Line(20,21,10,26,1,0xba8a4e);
            p.Line(23,13,23,18,3,0x514434); p.Rect(23,14,2,1,0xa8915b); p.Rect(23,17,2,1,0xa8915b);
        }

        private static void Arrow(Sprite p)
        {
            p.Line(6,26,24,8,2,0xab8552); p.Line(6,25,23,8,1,0xe1c086);
            p.Poly(0x879d9b, 18,8, 27,4, 25,13, 22,10); p.Poly(0xe3ebd8, 19,8, 26,5, 23,9);
            p.Poly(0xd9dfc6, 4,21, 8,17, 10,21, 7,25, 4,27); p.Line(5,22,8,19,1,0xffffff);
            p.Poly(0xa9bea8, 9,23, 14,22, 10,27, 6,28); p.Pixel(4,28,0x725337);
        }

        private static void Pearl(Sprite p, bool eye)
        {
            p.Disc(16,16,11,0x234f58); p.Disc(15,15,10,eye?0x739345U:0x3a8b8cU); p.Disc(14,13,7,eye?0xa7bd60U:0x58b6abU);
            p.Line(10,9,14,7,2,eye?0xd6dfa0U:0xa2e8d2U); p.Rect(8,11,2,3,eye?0xd6dfa0U:0xa2e8d2U);
            if(eye)
            {
                p.Poly(0x315652, 7,16, 11,12, 20,12, 25,16, 20,21, 12,21);
                p.Disc(16,16,5,0x3c8c83); p.Rect(15,12,3,9,0x172e3c); p.Rect(15,12,2,2,0xb7eac6);
            }
            else { p.Line(19,23,23,18,2,0x216778); p.Rect(11,8,3,2,0xd9f6da); p.Rect(17,21,2,2,0x6fc3b0); }
        }

        private static void BlazePowder(Sprite p)
        {
            p.Poly(0xa95c27, 4,24, 9,19, 7,15, 14,16, 17,7, 19,13, 23,10, 23,19, 28,23, 23,27, 10,27);
            p.Poly(0xf3a336, 6,24, 12,19, 15,20, 18,11, 20,19, 23,17, 22,22, 26,24, 20,26, 11,25);
            p.Poly(0xffd567, 11,23, 16,19, 18,15, 20,23, 16,25); p.Rect(7,10,3,3,0xffcd60); p.Rect(24,6,2,3,0xffcd60); p.Pixel(12,7,0xffeb9b);
        }

        private static void Wheat(Sprite p, bool crop)
        {
            for(int i=0;i<3;i++)
            {
                int x=9+i*7, y=5+(i==1?0:3);
                p.Line(x,y+4,15+i,28,2,crop?0x648542U:0xb99941U);
                for(int n=0;n<4;n++)
                {
                    int sy=y+n*3; p.Poly(0xc4a149, x,sy+5, x-4,sy+2, x-4,sy, x,sy+2);
                    p.Poly(0xe7c766, x,sy+5, x+4,sy+2, x+4,sy, x,sy+2); p.Pixel(x+2,sy,0xffdfa0);
                }
                p.Line(x,y-1,x,y+3,1,0xf0d382);
            }
            p.Line(9,22,21,23,2,0x79683a); p.Line(10,22,21,22,1,0xe3bb63);
            if(crop) { p.Line(14,27,5,20,2,0x6d9546); p.Line(19,27,26,20,2,0x91ae52); }
        }

        private static void Bread(Sprite p)
        {
            p.Poly(0x8b572c, 4,16, 7,11, 17,7, 25,9, 28,14, 26,21, 17,25, 7,24, 4,21);
            p.Poly(0xc79449, 5,16, 8,12, 17,8, 24,10, 27,14, 24,20, 16,23, 8,22, 5,20);
            p.Poly(0xe6bb6a, 6,15, 10,12, 17,10, 23,11, 25,14, 22,17, 13,20, 7,19);
            p.Line(10,13,12,18,2,0xffd994); p.Line(16,11,18,16,2,0xffd994); p.Line(22,12,23,15,2,0xffd994);
            p.Rect(8,22,5,1,0xb07839);
        }

        private static void Seeds(Sprite p)
        {
            p.Poly(0x4c7137, 5,12, 8,7, 14,8, 14,14, 10,18, 6,16); p.Poly(0xa8c167, 7,12, 9,8, 12,9, 12,13, 9,16);
            p.Poly(0x47713b, 18,15, 18,8, 25,5, 27,11, 23,16); p.Poly(0x91b65c, 20,13, 20,9, 24,7, 25,11, 23,14);
            p.Poly(0x5a773b, 12,24, 13,19, 20,18, 22,23, 19,27, 14,28); p.Poly(0xb5c975, 14,24, 15,20, 19,20, 20,23, 17,26);
            p.Rect(9,9,2,2,0xdae8a3); p.Pixel(23,8,0xdae8a3); p.Rect(15,21,2,2,0xe2e9a6);
        }

        private static void Apple(Sprite p)
        {
            p.Line(16,10,18,4,3,0x6c5433); p.Poly(0x649c44, 18,7, 22,3, 27,4, 24,7); p.Line(20,5,24,4,1,0xa8c967);
            p.Poly(0x942f3f, 5,14, 9,9, 14,9, 17,11, 21,9, 26,12, 28,17, 25,24, 20,28, 16,26, 12,28, 7,24, 4,18);
            p.Poly(0xd65153, 6,14, 10,10, 14,10, 17,12, 21,10, 25,13, 26,17, 23,23, 19,26, 15,24, 11,26, 8,22, 6,18);
            p.Poly(0xf28372, 8,14, 11,12, 14,13, 12,16, 10,21, 8,19); p.Rect(9,13,3,2,0xffc6a1); p.Line(23,17,21,22,2,0xb23542);
        }

        private static void Egg(Sprite p, int mob)
        {
            uint[] shells={0x6f9474,0xc3b19a,0xefb1ad,0xe8e6cd,0xe8d6a4,0xe5ac44,0xd6a691,0xb4c3bf,0x70a64d,0x625461,0x9b8267,0x56636a,0x474553,0x625477,0xb49369};
            uint[] spots={0x394e56,0x68514b,0xc77b86,0xa3b295,0xbd5e48,0xa85329,0x8a695e,0x667b81,0x36553b,0xb45550,0x56473a,0x293946,0xbc8fce,0xb396d7,0x5c7560};
            uint shell=shells[mob], spot=spots[mob];
            p.Poly(Shade(shell,-39), 13,4, 19,4, 23,8, 26,15, 27,22, 23,27, 9,27, 5,23, 5,17, 9,8);
            p.Poly(shell, 13,5, 18,5, 22,9, 25,16, 25,21, 22,25, 10,25, 7,22, 7,17, 10,9);
            p.Poly(Shade(shell,27), 13,6, 16,6, 14,10, 11,16, 9,20, 8,17, 11,10);
            int offset=mob%3;
            p.Rect(14+offset,8,4,3,spot); p.Rect(19,14+offset,5,4,spot); p.Rect(10,17-offset,4,4,spot); p.Rect(15-offset,22,4,3,spot);
            p.Pixel(20,9,spot); p.Rect(8,13,2,2,spot); p.Rect(13,6,3,2,Shade(shell,58));
            if(mob==12||mob==13) { p.Rect(10,17,3,1,0xe1b5f0); p.Rect(19,17,3,1,0xe1b5f0); }
        }

        private static void Door(Sprite p)
        {
            p.Rect(8,3,16,26,0x755132); p.Rect(9,4,14,24,0xc89957); p.Rect(9,4,13,2,0xf0c381); p.Rect(9,6,2,20,0xe3b26a);
            p.Rect(12,7,8,8,0x745835); p.Clear(13,8,6,6); p.Rect(16,8,1,6,0xc89957); p.Rect(13,11,6,1,0xc89957);
            p.Rect(12,18,8,8,0x986a3b); p.Rect(13,19,6,6,0xb78747); p.Rect(13,19,6,1,0xe0af64); p.Rect(20,16,2,3,0x5e523b); p.Pixel(20,16,0xffda81);
            p.Rect(8,9,2,2,0x666252); p.Rect(8,23,2,2,0x666252);
        }

        private static void Bed(Sprite p)
        {
            p.Poly(0x785333, 3,17, 16,23, 29,15, 29,22, 16,30, 3,23);
            p.Poly(0xcca06a, 4,18, 16,24, 16,27, 4,22); p.Poly(0x996b40, 17,24, 28,17, 28,21, 17,27);
            p.Poly(0x9c3446, 3,14, 15,20, 29,12, 29,17, 16,25, 3,19);
            p.Poly(0xd4545e, 3,14, 16,6, 29,12, 15,21); p.Line(4,14,15,19,1,0xfb8582);
            p.Poly(0xf2e9ce, 15,7, 18,5, 29,10, 26,13); p.Poly(0xd7d9c3, 15,8, 26,13, 26,15, 15,10);
            p.Line(18,5,27,9,1,0xfff6df); p.Rect(3,23,3,4,0x634a32); p.Rect(16,27,3,3,0x634a32); p.Rect(27,21,2,4,0x634a32);
        }

        private static void Torch(Sprite p)
        {
            Handle(p,10,27,18,12);
            p.Poly(0xd17329, 13,12, 14,7, 17,9, 18,3, 22,7, 23,12, 20,17, 15,16);
            p.Poly(0xffc655, 15,12, 17,8, 18,10, 20,6, 21,11, 19,15, 16,14);
            p.Rect(17,10,2,4,0xfff1b3); p.Rect(20,4,2,2,0xffe795);
        }

        private static void Lantern(Sprite p)
        {
            p.Line(12,7,12,4,2,0x828d84); p.Line(12,4,19,4,2,0xa7b1a0); p.Line(19,4,19,7,2,0x687970);
            p.Rect(9,8,14,3,0x53646b); p.Rect(7,11,18,14,0x8d7146); p.Rect(9,12,14,11,0xf6b94e); p.Rect(11,13,10,9,0xffdf85);
            p.Rect(13,14,6,7,0xfff4bc); p.Rect(8,10,2,14,0x697a73); p.Rect(21,10,2,14,0x697a73); p.Rect(15,10,2,14,0x97896b);
            p.Rect(7,24,18,3,0x45565f); p.Rect(9,27,14,1,0x354952); p.Rect(8,24,15,1,0x94a094); p.Rect(10,8,11,1,0xa7b1a0);
        }

        private static void Campfire(Sprite p)
        {
            p.Line(5,22,26,27,5,0x6d4f33); p.Line(5,22,26,26,2,0xb2864f); p.Line(7,27,25,21,5,0x795433); p.Line(7,26,25,20,2,0xc19758);
            p.Poly(0xcd6128, 8,21, 9,13, 12,16, 15,4, 19,9, 21,6, 25,18, 22,24, 12,25);
            p.Poly(0xf5aa38, 10,21, 12,16, 14,19, 16,8, 19,13, 22,12, 23,19, 20,23, 13,24);
            p.Poly(0xffdf76, 13,22, 15,17, 17,20, 19,14, 21,20, 18,24); p.Rect(16,21,3,3,0xfff1b4);
        }

        private static void Wart(Sprite p)
        {
            for(int i=0;i<3;i++)
            {
                int x=8+i*8,y=9+(i==1?-3:3); p.Line(x,y+5,x-1,27,3,0x763240);
                p.Poly(0x9f384f, x-5,y+2, x-2,y-2, x+2,y-2, x+5,y+2, x+4,y+8, x-3,y+9, x-5,y+6);
                p.Poly(0xca5363, x-4,y+2, x-1,y-1, x+2,y, x+3,y+5, x,y+7, x-3,y+5);
                p.Rect(x-2,y,3,2,0xf39384); p.Rect(x+2,y+5,2,2,0x742f49);
            }
        }

        private static uint Shade(uint rgb, int amount)
        {
            return (uint)(Mathf.Clamp((int)(rgb>>16&255)+amount,0,255)<<16 | Mathf.Clamp((int)(rgb>>8&255)+amount,0,255)<<8 | Mathf.Clamp((int)(rgb&255)+amount,0,255));
        }

        private sealed class Sprite
        {
            public readonly Color32[] Pixels = new Color32[Resolution * Resolution];
            public void Pixel(int x, int y, uint color) => Pixel(x,y,new Color32((byte)(color>>16),(byte)(color>>8),(byte)color,255));
            public void Pixel(int x, int y, Color32 color)
            {
                if(x>=0&&y>=0&&x<Resolution&&y<Resolution) Pixels[x+(Resolution-1-y)*Resolution]=color;
            }
            public void Rect(int x,int y,int width,int height,uint color)
            {
                for(int dy=0;dy<height;dy++) for(int dx=0;dx<width;dx++) Pixel(x+dx,y+dy,color);
            }
            public void Clear(int x,int y,int width,int height)
            {
                for(int dy=0;dy<height;dy++) for(int dx=0;dx<width;dx++) Pixel(x+dx,y+dy,new Color32());
            }
            public void Disc(int cx,int cy,int radius,uint color)
            {
                for(int y=cy-radius;y<=cy+radius;y++) for(int x=cx-radius;x<=cx+radius;x++)
                    if((x-cx)*(x-cx)+(y-cy)*(y-cy)<=radius*radius) Pixel(x,y,color);
            }
            public void Line(int x0,int y0,int x1,int y1,int thickness,uint color)
            {
                int steps=Mathf.Max(Mathf.Abs(x1-x0),Mathf.Abs(y1-y0));
                for(int step=0;step<=steps;step++)
                {
                    float t=steps==0?0:step/(float)steps;
                    int x=Mathf.RoundToInt(Mathf.Lerp(x0,x1,t)),y=Mathf.RoundToInt(Mathf.Lerp(y0,y1,t));
                    Rect(x-thickness/2,y-thickness/2,thickness,thickness,color);
                }
            }
            public void Poly(uint color, params int[] points)
            {
                for(int y=0;y<Resolution;y++) for(int x=0;x<Resolution;x++)
                {
                    bool inside=false; float px=x+.5f,py=y+.5f;
                    for(int i=0,j=points.Length-2;i<points.Length;j=i,i+=2)
                    {
                        float xi=points[i],yi=points[i+1],xj=points[j],yj=points[j+1];
                        if((yi>py)!=(yj>py)&&px<(xj-xi)*(py-yi)/(yj-yi)+xi) inside=!inside;
                    }
                    if(inside) Pixel(x,y,color);
                }
            }
            public void Outline()
            {
                var source=(Color32[])Pixels.Clone();
                for(int y=1;y<Resolution-1;y++) for(int x=1;x<Resolution-1;x++)
                {
                    int index=x+y*Resolution;
                    if(source[index].a!=0) continue;
                    Color32 neighbor=source[index+Resolution];
                    if(neighbor.a<160) neighbor=source[index-1];
                    if(neighbor.a<160) neighbor=source[index+1];
                    if(neighbor.a<160) neighbor=source[index-Resolution];
                    if(neighbor.a<160) continue;
                    Pixels[index]=new Color32((byte)(neighbor.r*.29f+9),(byte)(neighbor.g*.29f+11),(byte)(neighbor.b*.29f+15),235);
                }
            }
        }
    }
}
