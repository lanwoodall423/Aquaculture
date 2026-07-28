using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AquacultureFishing
{
    [StaticConstructorOnStartup]
    public static class FishVisualCache
    {
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Graphic> GraphicsByKey = new Dictionary<string, Graphic>();
        public static int CacheRevision { get; private set; }

        private sealed class Graphic_StyledFish : Graphic
        {
            private readonly Material[] materials;

            public Graphic_StyledFish(Material[] styledMaterials, Vector2 styledDrawSize)
            {
                materials = styledMaterials;
                drawSize = styledDrawSize;
            }

            public override Material MatSingle => materials[0];
            public override Material MatAt(Rot4 rot, Thing thing = null)
            {
                Material material;
                if (materials.Length == 1 || Find.TickManager == null) material = materials[0];
                else
                {
                    int phase = Mathf.Abs(Find.TickManager.TicksGame / 18 + (thing?.thingIDNumber ?? 0)) % materials.Length;
                    material = materials[phase];
                }
                CompFishTraits traits = thing?.TryGetComp<CompFishTraits>();
                return traits?.IsSwimmingInPond == true ? PondVisualRenderer.FishUnderwaterMaterial(traits, material) : material;
            }

            public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
            {
                CompFishTraits traits = thing?.TryGetComp<CompFishTraits>();
                if (traits?.IsSwimmingInPond == true)
                {
                    loc = PondMovementUtility.DrawPosition(traits, loc);
                    extraRotation += traits.schoolDrawRotation;
                    PondVisualRenderer.DrawFishShadow(traits, loc, drawSize, extraRotation);
                    float depth = AquacultureMod.Settings?.pseudoDepth == false ? 0f : traits.schoolDepth;
                    Vector2 originalSize = drawSize;
                    drawSize *= Mathf.Lerp(1f, 0.88f, depth);
                    try
                    {
                        base.DrawWorker(loc, rot, thingDef, thing, extraRotation);
                    }
                    finally { drawSize = originalSize; }
                    return;
                }
                base.DrawWorker(loc, rot, thingDef, thing, extraRotation);
            }
        }

        public static void Clear()
        {
            Materials.Clear();
            GraphicsByKey.Clear();
            CacheRevision++;
        }

        public static bool TryGetReplacementGraphic(Thing thing, CompFishTraits traits, Graphic original, out Graphic replacement)
        {
            replacement = original;
            if (thing == null || traits == null || original == null) return false;
            if (!traits.HasVisualTraits && !traits.IsInPond) return false;
            if (traits.cachedVisualReplacement != null && traits.cachedVisualSource == original &&
                traits.cachedVisualTraitRevision == traits.traitRevision && traits.cachedVisualCacheRevision == CacheRevision)
            {
                replacement = traits.cachedVisualReplacement;
                return true;
            }
            FishTextureVariation variation = FishGraphicUtility.VariationFor(thing, original);
            if (variation?.texture == null) return false;
            float scale = traits.SizeFactor;
            float verticalScale = traits.VerticalScale;
            string key = thing.def.defName + ":" + variation.key + ":" + traits.MaterialKey + ":" + scale.ToString("0.####") + ":" + verticalScale.ToString("0.####") + ":" + AquacultureMod.Settings.maskRevision;
            if (!GraphicsByKey.TryGetValue(key, out replacement))
            {
                bool animated = false;
                List<FishTraitDef> active = traits.ActiveTraits;
                for (int i = 0; i < active.Count; i++)
                    if (active[i].kind == FishTraitKind.Finish && !active[i].defName.Contains("Metallic")) { animated = true; break; }
                Material[] materials = traits.HasVisualTraits
                    ? Enumerable.Range(0, animated ? 4 : 1).Select(phase => MaterialFor(thing, traits, original, phase)).ToArray()
                    : new[] { variation.graphic?.MatAt(thing.Rotation, thing) ?? original.MatAt(thing.Rotation, thing) };
                replacement = new Graphic_StyledFish(materials, new Vector2(original.drawSize.x * scale, original.drawSize.y * scale * verticalScale));
                GraphicsByKey[key] = replacement;
            }
            traits.cachedVisualSource = original;
            traits.cachedVisualReplacement = replacement;
            traits.cachedVisualTraitRevision = traits.traitRevision;
            traits.cachedVisualCacheRevision = CacheRevision;
            return replacement != null;
        }

        public static Material MaterialFor(Thing thing, CompFishTraits traits, Graphic original, int phase = 0)
        {
            FishTextureVariation variation = FishGraphicUtility.VariationFor(thing, original);
            if (variation?.texture == null) return original.MatAt(thing.Rotation, thing);
            List<FishTraitDef> activeTraits = traits.ActiveTraits;
            string key = thing.def.defName + ":" + variation.key + ":" + traits.MaterialKey + ":p" + phase + ":" + AquacultureMod.Settings.maskRevision;
            if (Materials.TryGetValue(key, out Material cached)) return cached;

            Texture2D readable = ReadableCopy(variation.texture);
            Color32[] pixels = readable.GetPixels32();
            int width = readable.width;
            int height = readable.height;
            Color tint = Color.white;
            FishMaskRecord fishMask = AquacultureMod.Settings.GetMask(thing.def.defName, false);
            var patterns = new List<FishTraitDef>();
            var patternMasks = new List<FishPatternMaskRecord>();
            var finishes = new List<FishTraitDef>();
            float opacity = 1f;
            for (int i = 0; i < activeTraits.Count; i++)
            {
                FishTraitDef trait = activeTraits[i];
                if (trait.kind == FishTraitKind.Color) tint *= trait.tint;
                else if (trait.kind == FishTraitKind.Variegated)
                {
                    FishPatternMaskRecord mask = fishMask?.GetMask(variation.key, trait.pattern, width, height, false);
                    if (MaskHasPixels(mask)) { patterns.Add(trait); patternMasks.Add(mask); }
                }
                else if (trait.kind == FishTraitKind.Finish) { finishes.Add(trait); opacity *= trait.opacity; }
            }
            finishes.Sort((a, b) => string.CompareOrdinal(a.defName, b.defName));

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    Color source = pixels[index];
                    if (source.a <= 0f) continue;
                    Color result = source * tint;
                    for (int patternIndex = 0; patternIndex < patterns.Count; patternIndex++)
                    {
                        FishTraitDef patternTrait = patterns[patternIndex];
                        FishPatternMaskRecord mask = patternMasks[patternIndex];
                        if (mask.pixels[index] != 0 && PatternAt(patternTrait.pattern, x, y))
                        {
                            float factor = patternTrait.pattern == FishPattern.Spotted ? 0.42f : 1.38f;
                            result.r *= factor;
                            result.g *= factor;
                            result.b *= factor;
                        }
                    }
                    for (int finishIndex = 0; finishIndex < finishes.Count; finishIndex++)
                    {
                        FishTraitDef finish = finishes[finishIndex];
                        float wave = 0.5f + 0.5f * Mathf.Sin(x * 0.23f + y * 0.17f + phase * Mathf.PI * 0.5f);
                        if (finish.defName.Contains("Iridescent"))
                        {
                            Color sheen = Color.HSVToRGB(Mathf.Repeat((x + y) / (float)Mathf.Max(1, width + height) + 0.52f + phase * 0.08f, 1f), 0.65f, 1f);
                            result = Color.Lerp(result, sheen, 0.18f + wave * 0.14f);
                        }
                        else if (finish.defName.Contains("Pearlescent"))
                        {
                            Color sheen = Color.HSVToRGB(Mathf.Repeat(x / (float)Mathf.Max(1, width) + y * 0.015f + phase * 0.07f, 1f), 0.38f, 1f);
                            result = Color.Lerp(result, sheen, 0.25f + wave * 0.18f);
                        }
                        else if (finish.defName.Contains("Metallic"))
                        {
                            float luminance = result.grayscale;
                            float shine = Mathf.Clamp01((luminance - 0.35f) * 2.2f + wave * 0.3f);
                            result = Color.Lerp(result * 0.72f, Color.white, shine * 0.48f);
                        }
                        else if (finish.defName.Contains("Gilded"))
                        {
                            Color gold = new Color(1f, 0.67f, 0.08f, 1f);
                            result = Color.Lerp(result * new Color(1.08f, 0.82f, 0.34f, 1f), gold, 0.62f + wave * 0.16f);
                        }
                        else if (finish.defName.Contains("Glass"))
                        {
                            result = Color.Lerp(result, new Color(0.72f, 0.92f, 1f, 1f), 0.42f);
                        }
                    }
                    result.a = source.a * opacity;
                    pixels[index] = result;
                }
            }
            readable.SetPixels32(pixels);
            readable.Apply(false, true);
            Material materialResult = MaterialPool.MatFrom(new MaterialRequest(readable, opacity < 0.999f ? ShaderDatabase.Transparent : ShaderDatabase.Cutout));
            Materials[key] = materialResult;
            return materialResult;
        }

        private static bool MaskHasPixels(FishPatternMaskRecord mask)
        {
            if (mask?.pixels == null) return false;
            for (int i = 0; i < mask.pixels.Length; i++) if (mask.pixels[i] != 0) return true;
            return false;
        }

        private static bool PatternAt(FishPattern pattern, int x, int y)
        {
            if (pattern == FishPattern.Striped) return ((x + y / 2) / 5) % 2 == 0;
            int sx = x % 13 - 6;
            int sy = y % 11 - 5;
            return sx * sx + sy * sy < 10;
        }

        private static Texture2D ReadableCopy(Texture source)
        {
            RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
            return copy;
        }
    }
}
