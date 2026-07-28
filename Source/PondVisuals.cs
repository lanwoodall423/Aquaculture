using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    [StaticConstructorOnStartup]
    public static class PondVisualRenderer
    {
        private static readonly Material FreshWater = SolidMaterial(new Color(0.10f, 0.48f, 0.53f, 0.20f));
        private static readonly Material SaltWater = SolidMaterial(new Color(0.08f, 0.32f, 0.68f, 0.24f));
        private static readonly Material BrackishWater = SolidMaterial(new Color(0.35f, 0.42f, 0.18f, 0.22f));
        private static readonly Material DepthOne = SolidMaterial(new Color(0.02f, 0.12f, 0.18f, 0.14f));
        private static readonly Material DepthTwo = SolidMaterial(new Color(0.01f, 0.06f, 0.12f, 0.18f));
        private static readonly Material Shore = SolidMaterial(new Color(0.34f, 0.25f, 0.12f, 0.48f));
        private static readonly Material Lily = TextureMaterial(CreateLilyTexture());
        private static readonly Material Reeds = TextureMaterial(CreateReedTexture());
        private static readonly Material Stones = TextureMaterial(CreateStoneTexture());
        private static readonly Material[] Shadows =
        {
            TextureMaterial(CreateEllipseTexture(new Color(0f, 0f, 0f, 0.25f), false)),
            TextureMaterial(CreateEllipseTexture(new Color(0f, 0f, 0f, 0.19f), false)),
            TextureMaterial(CreateEllipseTexture(new Color(0f, 0f, 0f, 0.13f), false)),
            TextureMaterial(CreateEllipseTexture(new Color(0f, 0f, 0f, 0.08f), false))
        };
        private static readonly Material[] Surface =
        {
            TextureMaterial(CreateSurfaceTexture(), new Color(0.50f, 0.84f, 0.88f, 0.075f)),
            TextureMaterial(CreateSurfaceTexture(), new Color(0.42f, 0.69f, 0.94f, 0.085f)),
            TextureMaterial(CreateSurfaceTexture(), new Color(0.69f, 0.73f, 0.43f, 0.08f))
        };
        private static readonly Material SurfaceEdge = SolidMaterial(new Color(0.70f, 0.91f, 0.94f, 0.22f));
        private static readonly Material[] Caustics = CreateCausticMaterials();
        private static readonly Dictionary<int, Material> FishWaterMaterials = new Dictionary<int, Material>();
        private static readonly Material[] Algae =
        {
            SolidMaterial(new Color(0.12f, 0.36f, 0.10f, 0.00f)),
            SolidMaterial(new Color(0.12f, 0.38f, 0.10f, 0.035f)),
            SolidMaterial(new Color(0.13f, 0.42f, 0.09f, 0.07f)),
            SolidMaterial(new Color(0.14f, 0.46f, 0.08f, 0.11f)),
            SolidMaterial(new Color(0.16f, 0.50f, 0.07f, 0.15f))
        };
        private static readonly Material[] Ripples = CreateRippleMaterials();

        public static Material WaterMaterial(PondWaterKind kind)
        {
            if (kind == PondWaterKind.Saltwater) return SaltWater;
            if (kind == PondWaterKind.Brackishwater) return BrackishWater;
            return FreshWater;
        }

        public static Material AlgaeMaterial(float fraction) => Algae[Mathf.Clamp(Mathf.FloorToInt(fraction * Algae.Length), 0, Algae.Length - 1)];
        public static Material DepthOneMaterial => DepthOne;
        public static Material DepthTwoMaterial => DepthTwo;
        public static Material ShoreMaterial => Shore;
        public static Material LilyMaterial => Lily;
        public static Material ReedMaterial => Reeds;
        public static Material StoneMaterial => Stones;
        public static Material RippleMaterial(int index) => Ripples[Mathf.Clamp(index, 0, Ripples.Length - 1)];
        public static Material SurfaceMaterial(PondWaterKind kind) => Surface[kind == PondWaterKind.Saltwater ? 1 : kind == PondWaterKind.Brackishwater ? 2 : 0];
        public static Material SurfaceEdgeMaterial => SurfaceEdge;
        public static Material CausticMaterial(int phase) => Caustics[PositiveMod(phase, Caustics.Length)];

        public static void DrawFishShadow(CompFishTraits fish, Vector3 location, Vector2 sourceSize, float rotation)
        {
            AquacultureSettings settings = AquacultureMod.Settings;
            if (settings?.enhancedPondVisuals == false || settings?.fishShadows == false) return;
            float depth = settings?.pseudoDepth == false ? 0f : fish.schoolDepth;
            int tier = Mathf.Clamp(Mathf.FloorToInt(depth * Shadows.Length), 0, Shadows.Length - 1);
            float spread = Mathf.Lerp(1f, 1.24f, depth);
            Vector3 scale = new Vector3(sourceSize.x * 0.62f * spread, 1f, sourceSize.y * 0.38f * spread);
            Vector3 position = new Vector3(location.x + 0.06f, Altitudes.AltitudeFor(AltitudeLayer.Shadows), location.z - 0.08f);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(position, Quaternion.AngleAxis(rotation, Vector3.up), scale), Shadows[tier], 0);
        }

        public static Material FishUnderwaterMaterial(CompFishTraits fish, Material source)
        {
            AquacultureSettings settings = AquacultureMod.Settings;
            if (settings?.enhancedPondVisuals == false || settings?.underwaterFishTint == false || source?.mainTexture == null) return source;
            FishPondMapComponent ponds = fish.parent.Map?.GetComponent<FishPondMapComponent>();
            if (ponds == null || !ponds.TryGetPondVisualState(fish, out PondWaterKind kind, out float algae)) return source;
            // Every pond fish receives the same submerged color grade. Pseudo-depth
            // remains a movement/shadow effect and cannot make individual fish appear
            // above the water surface.
            const int depthTier = 2;
            int algaeTier = Mathf.Clamp(Mathf.RoundToInt(algae * 4f), 0, 4);
            int sourceColor = source.color.GetHashCode();
            int featureBits = (settings?.surfaceOverlay != false ? 1 : 0) | (settings?.caustics != false ? 2 : 0);
            int key = source.mainTexture.GetInstanceID() * 397 ^ source.shader.GetInstanceID() * 53 ^ sourceColor ^ (int)kind * 31 ^ depthTier * 7 ^ algaeTier ^ featureBits * 7919;
            if (!FishWaterMaterials.TryGetValue(key, out Material material))
            {
                Color water = kind == PondWaterKind.Saltwater ? new Color(0.60f, 0.78f, 1f) :
                    kind == PondWaterKind.Brackishwater ? new Color(0.76f, 0.80f, 0.50f) : new Color(0.62f, 0.88f, 0.90f);
                water = Color.Lerp(water, new Color(0.48f, 0.76f, 0.34f), algaeTier * 0.045f);
                Texture2D texture = CreateSubmergedTexture(source.mainTexture, source.color, water, depthTier,
                    settings?.surfaceOverlay != false, settings?.caustics != false);
                Shader shader = source.shader == ShaderDatabase.Transparent || source.color.a < 0.999f ? ShaderDatabase.Transparent : ShaderDatabase.Cutout;
                material = MaterialPool.MatFrom(new MaterialRequest(texture, shader, Color.white));
                FishWaterMaterials.Add(key, material);
            }
            return material;
        }

        private static Texture2D CreateSubmergedTexture(Texture source, Color sourceColor, Color water, int depthTier, bool surface, bool caustics)
        {
            RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
            {
                name = source.name + "_AF_Submerged",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = source.filterMode
            };
            texture.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);

            Color32[] pixels = texture.GetPixels32();
            // Even the shallowest school depth is still below the surface. Depth changes
            // should vary the effect, never transition to an un-submerged appearance.
            float grade = 0.62f + depthTier * 0.025f;
            float veil = surface ? 0.22f + depthTier * 0.010f : 0f;
            for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
            {
                int index = y * texture.width + x;
                Color original = pixels[index];
                if (original.a <= 0f) continue;
                Color value = original * sourceColor;
                float luminance = value.grayscale;
                Color waterGraded = new Color(luminance * water.r, luminance * water.g, luminance * water.b, value.a);
                value = Color.Lerp(value, waterGraded, grade);
                if (surface) value = Color.Lerp(value, water, veil);
                if (surface)
                {
                    float surfaceWave = 0.72f + (Mathf.Sin(x * 0.16f + y * 0.09f) * 0.5f + 0.5f) * 0.14f;
                    value.r *= surfaceWave; value.g *= surfaceWave; value.b *= surfaceWave;
                }
                if (caustics)
                {
                    float bandA = Mathf.Sin(x * 0.23f + y * 0.14f);
                    float bandB = Mathf.Sin(x * -0.11f + y * 0.27f + 1.7f);
                    float light = Mathf.Pow(Mathf.Clamp01((bandA + bandB) * 0.5f - 0.48f), 2f) * 0.32f;
                    value = Color.Lerp(value, new Color(0.82f, 0.96f, 1f, value.a), light);
                }
                value.a = original.a * sourceColor.a;
                pixels[index] = value;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Material SolidMaterial(Color color)
        {
            return MaterialPool.MatFrom(new MaterialRequest(BaseContent.WhiteTex, ShaderDatabase.Transparent, color));
        }

        private static Material TextureMaterial(Texture2D texture)
        {
            return MaterialPool.MatFrom(new MaterialRequest(texture, ShaderDatabase.Transparent));
        }

        private static Material TextureMaterial(Texture2D texture, Color color)
        {
            return MaterialPool.MatFrom(new MaterialRequest(texture, ShaderDatabase.Transparent, color));
        }

        private static Material[] CreateCausticMaterials()
        {
            var result = new Material[4];
            for (int i = 0; i < result.Length; i++) result[i] = TextureMaterial(CreateCausticTexture(i), new Color(0.82f, 0.95f, 1f, 0.11f));
            return result;
        }

        private static Material[] CreateRippleMaterials()
        {
            Texture2D texture = CreateRingTexture();
            var result = new Material[4];
            for (int i = 0; i < result.Length; i++)
                result[i] = MaterialPool.MatFrom(new MaterialRequest(texture, ShaderDatabase.Transparent, new Color(0.72f, 0.92f, 1f, 0.24f - i * 0.045f)));
            return result;
        }

        private static Texture2D CreateLilyTexture()
        {
            const int size = 32;
            var texture = NewTexture(size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - size / 2f) / (size * 0.42f);
                float dy = (y + 0.5f - size / 2f) / (size * 0.34f);
                float radius = dx * dx + dy * dy;
                bool wedge = dx > 0f && Mathf.Abs(dy) < dx * 0.42f;
                if (radius <= 1f && !wedge) pixels[y * size + x] = radius > 0.78f ? new Color32(39, 94, 40, 230) : new Color32(57, 128, 54, 235);
            }
            Apply(texture, pixels);
            return texture;
        }

        private static Texture2D CreateReedTexture()
        {
            const int size = 32;
            var texture = NewTexture(size);
            var pixels = new Color32[size * size];
            int[] stems = { 10, 16, 22 };
            for (int i = 0; i < stems.Length; i++)
            {
                int baseX = stems[i];
                for (int y = 4 + i; y < 29; y++)
                {
                    int x = baseX + Mathf.RoundToInt(Mathf.Sin(y * 0.16f + i) * 2f);
                    pixels[y * size + Mathf.Clamp(x, 0, size - 1)] = y > 23 ? new Color32(111, 85, 38, 240) : new Color32(72, 124, 47, 235);
                    if (y > 8 && y < 18) pixels[y * size + Mathf.Clamp(x + (i - 1) * 3, 0, size - 1)] = new Color32(91, 139, 54, 210);
                }
            }
            Apply(texture, pixels);
            return texture;
        }

        private static Texture2D CreateStoneTexture()
        {
            return CreateEllipseTexture(new Color(0.43f, 0.46f, 0.43f, 0.88f), true);
        }

        private static Texture2D CreateEllipseTexture(Color color, bool highlight)
        {
            const int size = 32;
            var texture = NewTexture(size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - 16f) / 13f;
                float dy = (y + 0.5f - 16f) / 8f;
                float radius = dx * dx + dy * dy;
                if (radius > 1f) continue;
                Color value = color;
                if (highlight && dx < -0.1f && dy > 0.05f) value *= 1.22f;
                value.a = color.a * Mathf.Clamp01((1f - radius) * 4f);
                pixels[y * size + x] = value;
            }
            Apply(texture, pixels);
            return texture;
        }

        private static Texture2D CreateRingTexture()
        {
            const int size = 64;
            var texture = NewTexture(size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - 32f) / 27f;
                float dy = (y + 0.5f - 32f) / 15f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.82f) / 0.08f) * 0.8f;
                pixels[y * size + x] = new Color(0.78f, 0.94f, 1f, alpha);
            }
            Apply(texture, pixels);
            return texture;
        }

        private static Texture2D CreateSurfaceTexture()
        {
            const int size = 64;
            var texture = NewTexture(size);
            texture.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float wave = Mathf.Sin(x * 0.31f + Mathf.Sin(y * 0.21f)) * 0.5f + 0.5f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, 0.16f + wave * 0.18f);
            }
            Apply(texture, pixels);
            return texture;
        }

        private static Texture2D CreateCausticTexture(int phase)
        {
            const int size = 64;
            var texture = NewTexture(size);
            texture.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float a = Mathf.Sin(x * 0.24f + y * 0.15f + phase * 1.3f);
                float b = Mathf.Sin(x * -0.13f + y * 0.27f + phase * 0.8f);
                float line = Mathf.Pow(Mathf.Clamp01((a + b) * 0.5f - 0.42f), 2f) * 0.9f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, line);
            }
            Apply(texture, pixels);
            return texture;
        }

        private static int PositiveMod(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private static Texture2D NewTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            return texture;
        }

        private static void Apply(Texture2D texture, Color32[] pixels)
        {
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
        }
    }

    public sealed partial class FishPondMapComponent
    {
        private sealed class PondVisualData
        {
            public Mesh water;
            public Mesh depthOne;
            public Mesh depthTwo;
            public Mesh shore;
            public Mesh lilies;
            public Mesh reeds;
            public Mesh stones;
            public Mesh surface;
            public Mesh surfaceEdge;

            public void Destroy()
            {
                DestroyMesh(water); DestroyMesh(depthOne); DestroyMesh(depthTwo); DestroyMesh(shore);
                DestroyMesh(lilies); DestroyMesh(reeds); DestroyMesh(stones);
                DestroyMesh(surface); DestroyMesh(surfaceEdge);
            }

            private static void DestroyMesh(Mesh mesh)
            {
                if (mesh != null) UnityEngine.Object.Destroy(mesh);
            }
        }

        internal void DrawVisiblePondVisuals()
        {
            if (map != Find.CurrentMap) return;
            if (AquacultureMod.Settings?.enhancedPondVisuals == false) return;
            EnsurePondState();
            CellRect visible = Find.CameraDriver.CurrentViewRect.ExpandedBy(2);
            for (int i = 0; i < pondStates.Count; i++)
            {
                PondState pond = pondStates[i];
                if (!pond.info.Intersects(visible) || pond.visuals == null) continue;
                DrawPondVisuals(pond);
            }
        }

        private void DrawPondVisuals(PondState pond)
        {
            PondVisualData visual = pond.visuals;
            AquacultureSettings settings = AquacultureMod.Settings;
            if (settings?.waterTypeTint != false) DrawMesh(visual.water, PondVisualRenderer.WaterMaterial(pond.ecology.waterKind));
            float capacity = Mathf.Max(0.1f, pond.info.cells.Count * 0.25f);
            if (settings?.algaeTint != false) DrawMesh(visual.water, PondVisualRenderer.AlgaeMaterial(pond.ecology.algae / capacity));
            if (settings?.depthShading != false)
            {
                DrawMesh(visual.depthOne, PondVisualRenderer.DepthOneMaterial);
                DrawMesh(visual.depthTwo, PondVisualRenderer.DepthTwoMaterial);
            }
            if (settings?.shorelineVisuals != false) DrawMesh(visual.shore, PondVisualRenderer.ShoreMaterial);
            if (settings?.surfaceOverlay != false) DrawMesh(visual.surface, PondVisualRenderer.SurfaceMaterial(pond.ecology.waterKind));
            if (settings?.caustics != false)
            {
                int now = Find.TickManager?.TicksGame ?? 0;
                DrawMesh(visual.surface, PondVisualRenderer.CausticMaterial(now / 24 + pond.info.anchor.GetHashCode()));
            }
            if (settings?.shorelineVisuals != false) DrawMesh(visual.surfaceEdge, PondVisualRenderer.SurfaceEdgeMaterial);
            if (settings?.pondDecorations != false)
            {
                DrawMesh(visual.lilies, PondVisualRenderer.LilyMaterial);
                DrawMesh(visual.reeds, PondVisualRenderer.ReedMaterial);
                DrawMesh(visual.stones, PondVisualRenderer.StoneMaterial);
            }
            if (settings?.pondRipples != false) DrawRipple(pond);
        }

        private void DrawRipple(PondState pond)
        {
            if (pond.fish.Count == 0) return;
            int now = Find.TickManager?.TicksGame ?? 0;
            int cycle = PositiveMod(now + pond.info.anchor.GetHashCode(), 720);
            if (cycle >= 180) return;
            int fishIndex = PositiveMod(pond.info.anchor.GetHashCode() ^ now / 720, pond.fish.Count);
            CompFishTraits fish = null;
            for (int i = 0; i < pond.fish.Count; i++)
            {
                CompFishTraits candidate = pond.fish[(fishIndex + i) % pond.fish.Count];
                if (AquacultureMod.Settings?.pseudoDepth == false || candidate.schoolDepth <= 0.30f) { fish = candidate; break; }
            }
            if (fish == null) return;
            Vector2 position = fish.schoolInitialized ? fish.schoolPosition : new Vector2(fish.parent.Position.x + 0.5f, fish.parent.Position.z + 0.5f);
            float progress = cycle / 180f;
            float size = Mathf.Lerp(0.26f, 1.05f, progress);
            int fade = Mathf.Clamp(Mathf.FloorToInt(progress * 4f), 0, 3);
            Vector3 location = new Vector3(position.x, Altitudes.AltitudeFor(AltitudeLayer.PawnRope, 0.006f), position.y);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(location, Quaternion.identity, new Vector3(size, 1f, size * 0.58f)), PondVisualRenderer.RippleMaterial(fade), 0);
        }

        private static void DrawMesh(Mesh mesh, Material material)
        {
            if (mesh != null && material != null) Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0);
        }

        private PondVisualData BuildPondVisuals(PondMovementUtility.PondInfo info)
        {
            float baseAltitude = Altitudes.AltitudeFor(AltitudeLayer.TerrainScatter, -0.025f);
            var water = new QuadMeshBuilder();
            var depthOne = new QuadMeshBuilder();
            var depthTwo = new QuadMeshBuilder();
            var shore = new QuadMeshBuilder();
            var lilies = new QuadMeshBuilder();
            var reeds = new QuadMeshBuilder();
            var stones = new QuadMeshBuilder();
            var surface = new QuadMeshBuilder();
            var surfaceEdge = new QuadMeshBuilder();
            // Item graphics from dependencies can add their own vertical offsets. PawnRope is
            // above all item layers but remains below standing pawns and projectiles.
            float surfaceAltitude = Altitudes.AltitudeFor(AltitudeLayer.PawnRope);
            float decorationAltitude = Altitudes.AltitudeFor(AltitudeLayer.PawnRope, 0.010f);
            for (int i = 0; i < info.cells.Count; i++)
            {
                IntVec3 cell = info.cells[i];
                water.Add(cell.x, cell.z, 1f, 1f, baseAltitude);
                surface.Add(cell.x, cell.z, 1f, 1f, surfaceAltitude);
                bool north = info.cellSet.Contains(cell + IntVec3.North);
                bool south = info.cellSet.Contains(cell + IntVec3.South);
                bool east = info.cellSet.Contains(cell + IntVec3.East);
                bool west = info.cellSet.Contains(cell + IntVec3.West);
                bool interior = north && south && east && west;
                if (interior) depthOne.Add(cell.x, cell.z, 1f, 1f, baseAltitude + 0.002f);
                bool deep = interior && GenAdj.AdjacentCellsAround.All(offset => info.cellSet.Contains(cell + offset));
                if (deep) depthTwo.Add(cell.x, cell.z, 1f, 1f, baseAltitude + 0.003f);
                const float edge = 0.10f;
                if (!north) shore.Add(cell.x, cell.z + 1f - edge, 1f, edge, baseAltitude + 0.005f);
                if (!south) shore.Add(cell.x, cell.z, 1f, edge, baseAltitude + 0.005f);
                if (!east) shore.Add(cell.x + 1f - edge, cell.z, edge, 1f, baseAltitude + 0.005f);
                if (!west) shore.Add(cell.x, cell.z, edge, 1f, baseAltitude + 0.005f);
                const float highlight = 0.035f;
                if (!north) surfaceEdge.Add(cell.x, cell.z + 1f - highlight, 1f, highlight, surfaceAltitude + 0.002f);
                if (!south) surfaceEdge.Add(cell.x, cell.z, 1f, highlight, surfaceAltitude + 0.002f);
                if (!east) surfaceEdge.Add(cell.x + 1f - highlight, cell.z, highlight, 1f, surfaceAltitude + 0.002f);
                if (!west) surfaceEdge.Add(cell.x, cell.z, highlight, 1f, surfaceAltitude + 0.002f);

                uint hash = Hash((uint)(cell.x * 73856093 ^ cell.z * 19349663 ^ info.anchor.GetHashCode()));
                float roll = (hash & 0xffffu) / 65535f;
                float offsetX = ((hash >> 16) & 255u) / 255f * 0.46f + 0.27f;
                float offsetZ = ((hash >> 24) & 255u) / 255f * 0.46f + 0.27f;
                if (interior && roll < 0.075f) lilies.AddCentered(cell.x + offsetX, cell.z + offsetZ, 0.46f, 0.38f, decorationAltitude, hash % 4 * 90f);
                else if (!interior && roll < 0.12f) reeds.AddCentered(cell.x + offsetX, cell.z + offsetZ, 0.42f, 0.56f, decorationAltitude, hash % 3 * 18f - 18f);
                else if (!interior && roll < 0.20f) stones.AddCentered(cell.x + offsetX, cell.z + offsetZ, 0.38f, 0.26f, baseAltitude + 0.011f, hash % 4 * 45f);
            }
            return new PondVisualData
            {
                water = water.Build("AF pond water"), depthOne = depthOne.Build("AF pond depth 1"), depthTwo = depthTwo.Build("AF pond depth 2"),
                shore = shore.Build("AF pond shore"), lilies = lilies.Build("AF pond lilies"), reeds = reeds.Build("AF pond reeds"), stones = stones.Build("AF pond stones"),
                surface = surface.Build("AF pond surface"), surfaceEdge = surfaceEdge.Build("AF pond surface edge")
            };
        }

        internal bool TryGetPondVisualState(CompFishTraits fish, out PondWaterKind kind, out float algaeFraction)
        {
            if (fish != null && pondByFish.TryGetValue(fish, out PondState pond))
            {
                kind = pond.ecology.waterKind;
                float capacity = Mathf.Max(0.1f, pond.info.cells.Count * 0.25f);
                algaeFraction = Mathf.Clamp01(pond.ecology.algae / capacity);
                return true;
            }
            kind = PondWaterKind.Freshwater;
            algaeFraction = 0f;
            return false;
        }

        private void ReleasePondVisuals()
        {
            for (int i = 0; i < pondStates.Count; i++) pondStates[i].visuals?.Destroy();
        }

        private static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7feb352du;
            value ^= value >> 15;
            value *= 0x846ca68bu;
            return value ^ (value >> 16);
        }

        private sealed class QuadMeshBuilder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<int> triangles = new List<int>();

            public void Add(float x, float z, float width, float height, float altitude)
            {
                AddCentered(x + width * 0.5f, z + height * 0.5f, width, height, altitude, 0f);
            }

            public void AddCentered(float x, float z, float width, float height, float altitude, float rotation)
            {
                int start = vertices.Count;
                float radians = rotation * Mathf.Deg2Rad;
                float cos = Mathf.Cos(radians);
                float sin = Mathf.Sin(radians);
                AddVertex(-width * 0.5f, -height * 0.5f); AddVertex(-width * 0.5f, height * 0.5f);
                AddVertex(width * 0.5f, height * 0.5f); AddVertex(width * 0.5f, -height * 0.5f);
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(0f, 1f)); uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(1f, 0f));
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);

                void AddVertex(float localX, float localZ)
                {
                    vertices.Add(new Vector3(x + localX * cos - localZ * sin, altitude, z + localX * sin + localZ * cos));
                }
            }

            public Mesh Build(string name)
            {
                if (vertices.Count == 0) return null;
                var mesh = new Mesh { name = name };
                if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); mesh.UploadMeshData(true);
                return mesh;
            }
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(DynamicDrawManager), "DrawDynamicThings")]
    public static class PondSurfaceDynamicDrawPatch
    {
        public static void Postfix()
        {
            Find.CurrentMap?.GetComponent<FishPondMapComponent>()?.DrawVisiblePondVisuals();
        }
    }
}
