using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KaiBanks_Localisation_2026;
using KaiBanks_UILibrary_2025;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

public static class TurkishPatchRuntime
{
    private static bool _initialized;
    private static bool _inInit;
    private static bool _sceneHooked;
    private static TMP_FontAsset _googleSans;
    private static TMP_FontAsset _fuzzyRegularDyn;
    private static readonly Dictionary<int, TMP_FontAsset> _dynamicTwins = new Dictionary<int, TMP_FontAsset>();
    private static readonly HashSet<int> _processedFonts = new HashSet<int>();
    private static readonly Dictionary<string, string> _extraTr = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "English", "Türkçe" },
        { "Gallery", "Galeri" },
        { "Extras", "Ekstralar" },
        { "Delete this save?", "Bu kayıt silinsin mi?" },
        { "Demo Idle Seconds: ", "Demo Boşta Kalma Süresi (sn): " },
        { "R Phone", "R Telefon" },
        { "T Close", "T Kapat" },
        { "Y Sketchbook", "Y Eskiz Defteri" },
        { "R Dismiss<br>T Close<br>Y Sketchbook", "R Bırak<br>T Kapat<br>Y Eskiz Defteri" },
        { "R Phone<br>T Close<br>Y Dismiss", "R Telefon<br>T Kapat<br>Y Bırak" },
        { "Hold ESC to Skip", "Atlamak için ESC'ye Basılı Tutun" },
        { "Hold A Button to Skip", "Atlamak için A Tuşuna Basılı Tutun" },
        { "Interlude", "Ara Sahne" },
        { "Handheld Mode", "El Konsolu Modu" },
        { "Activated", "Etkin" }
    };

    public static void EnsureInitialized()
    {
        if (_inInit)
        {
            return;
        }
        _inInit = true;
        try
        {
            if (!_sceneHooked)
            {
                _sceneHooked = true;
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
            if (!_initialized)
            {
                _initialized = true;
                EnsureAllLoadedFonts();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TurkishPatch] EnsureInitialized error: " + ex.Message);
        }
        finally
        {
            _inInit = false;
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureAllLoadedFonts();
    }

    public static void EnsureAllLoadedFonts()
    {
        try
        {
            TMP_FontAsset baseFallback = GetGoogleSansFallback();
            if (baseFallback != null)
            {
                try
                {
                    if (TMP_Settings.fallbackFontAssets == null)
                    {
                        TMP_Settings.fallbackFontAssets = new List<TMP_FontAsset>();
                    }
                    if (!TMP_Settings.fallbackFontAssets.Contains(baseFallback))
                    {
                        TMP_Settings.fallbackFontAssets.Add(baseFallback);
                    }
                    if (TMP_Settings.defaultFontAsset != null)
                    {
                        EnsureFontFallbacks(TMP_Settings.defaultFontAsset);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[TurkishPatch] TMP_Settings fallback error: " + ex.Message);
                }
            }

            TMP_FontAsset[] allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (allFonts == null)
            {
                return;
            }

            int i;
            for (i = 0; i < allFonts.Length; i++)
            {
                TMP_FontAsset f = allFonts[i];
                if (f != null && !string.IsNullOrEmpty(f.name) && f.name.StartsWith("FuzzyBubbles-Regular", StringComparison.Ordinal))
                {
                    EnsureFontFallbacks(f);
                }
            }

            for (i = 0; i < allFonts.Length; i++)
            {
                EnsureFontFallbacks(allFonts[i]);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TurkishPatch] EnsureAllLoadedFonts error: " + ex.Message);
        }
    }

    private static TMP_FontAsset GetGoogleSansFallback()
    {
        if (_googleSans == null)
        {
            _googleSans = Resources.Load<TMP_FontAsset>("GoogleSans-RegularSDF");
            if (_googleSans == null)
            {
                _googleSans = Resources.Load<TMP_FontAsset>("Roboto-RegularSDF");
            }
            if (_googleSans != null)
            {
                MarkPersistent(_googleSans);
                _processedFonts.Add(_googleSans.GetInstanceID());
            }
        }
        return _googleSans;
    }

    private static TMP_FontAsset FindFuzzyRegularDynamic()
    {
        if (_fuzzyRegularDyn != null)
        {
            return _fuzzyRegularDyn;
        }
        TMP_FontAsset[] allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        if (allFonts == null)
        {
            return null;
        }
        for (int i = 0; i < allFonts.Length; i++)
        {
            TMP_FontAsset f = allFonts[i];
            if (f != null && !string.IsNullOrEmpty(f.name) &&
                f.name.StartsWith("FuzzyBubbles-Regular", StringComparison.Ordinal) &&
                f.sourceFontFile != null)
            {
                _fuzzyRegularDyn = GetOrCreateDynamicTwin(f);
                return _fuzzyRegularDyn;
            }
        }
        return null;
    }

    public static void EnsureFontFallbacks(TMP_FontAsset font)
    {
        if (font == null)
        {
            return;
        }
        int id = font.GetInstanceID();
        if (_processedFonts.Contains(id))
        {
            return;
        }

        string fontName = font.name ?? string.Empty;
        if (fontName.StartsWith("NotoSans", StringComparison.Ordinal) ||
            fontName.EndsWith("(TR Dynamic)", StringComparison.Ordinal) ||
            fontName.EndsWith("(Localised)", StringComparison.Ordinal))
        {
            _processedFonts.Add(id);
            return;
        }

        try
        {
            if (font.fallbackFontAssetTable == null)
            {
                font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            }

            if (font.sourceFontFile != null && font.atlasPopulationMode == AtlasPopulationMode.Static)
            {
                TMP_FontAsset twin = GetOrCreateDynamicTwin(font);
                if (twin != null)
                {
                    if (fontName.StartsWith("FuzzyBubbles-Regular", StringComparison.Ordinal))
                    {
                        _fuzzyRegularDyn = twin;
                    }
                    if (!font.fallbackFontAssetTable.Contains(twin))
                    {
                        font.fallbackFontAssetTable.Insert(0, twin);
                    }
                }
                TMP_FontAsset gFallback = GetGoogleSansFallback();
                if (gFallback != null && gFallback != font && !font.fallbackFontAssetTable.Contains(gFallback))
                {
                    font.fallbackFontAssetTable.Add(gFallback);
                }
                _processedFonts.Add(id);
                return;
            }

            if (fontName.Contains("AnnieUseYourTelescope") || fontName.Contains("ReenieBeanie"))
            {
                TMP_FontAsset fuzzy = FindFuzzyRegularDynamic();
                if (fuzzy != null && !font.fallbackFontAssetTable.Contains(fuzzy))
                {
                    font.fallbackFontAssetTable.Insert(0, fuzzy);
                }
                TMP_FontAsset gFallback = GetGoogleSansFallback();
                if (gFallback != null && gFallback != font && !font.fallbackFontAssetTable.Contains(gFallback))
                {
                    font.fallbackFontAssetTable.Add(gFallback);
                }
                if (fuzzy != null)
                {
                    _processedFonts.Add(id);
                }
                return;
            }

            if (font.atlasPopulationMode == AtlasPopulationMode.Static)
            {
                TMP_FontAsset gFallback = GetGoogleSansFallback();
                if (gFallback != null && gFallback != font && !font.fallbackFontAssetTable.Contains(gFallback))
                {
                    font.fallbackFontAssetTable.Add(gFallback);
                }
            }
            _processedFonts.Add(id);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TurkishPatch] EnsureFontFallbacks(" + fontName + ") error: " + ex.Message);
            _processedFonts.Add(id);
        }
    }

    private static TMP_FontAsset GetOrCreateDynamicTwin(TMP_FontAsset baseFont)
    {
        Font src = baseFont.sourceFontFile;
        if (src == null)
        {
            return null;
        }
        int srcId = src.GetInstanceID();
        TMP_FontAsset existing;
        if (_dynamicTwins.TryGetValue(srcId, out existing) && existing != null)
        {
            return existing;
        }

        int pointSize = Mathf.RoundToInt(baseFont.faceInfo.pointSize);
        if (pointSize < 16 || pointSize > 256)
        {
            pointSize = 90;
        }
        int padding = baseFont.atlasPadding;
        if (padding < 1 || padding > 64)
        {
            padding = 9;
        }
        GlyphRenderMode renderMode = baseFont.atlasRenderMode;
        if ((int)renderMode == 0)
        {
            renderMode = GlyphRenderMode.SDFAA;
        }

        TMP_FontAsset dyn = null;
        try
        {
            dyn = TMP_FontAsset.CreateFontAsset(src, pointSize, padding, renderMode, 512, 512, AtlasPopulationMode.Dynamic, true);
        }
        catch
        {
            dyn = null;
        }

        if (dyn == null)
        {
            try
            {
                dyn = TMP_FontAsset.CreateFontAsset(src, 90, 9, GlyphRenderMode.SDFAA, 512, 512, AtlasPopulationMode.Dynamic, true);
            }
            catch
            {
                dyn = null;
            }
        }

        if (dyn == null)
        {
            return null;
        }

        dyn.name = baseFont.name + " (TR Dynamic)";
        dyn.normalStyle = baseFont.normalStyle;
        dyn.normalSpacingOffset = baseFont.normalSpacingOffset;
        dyn.boldStyle = baseFont.boldStyle;
        dyn.boldSpacing = baseFont.boldSpacing;
        dyn.italicStyle = baseFont.italicStyle;
        dyn.tabSize = baseFont.tabSize;

        FaceInfo baseInfo = baseFont.faceInfo;
        FaceInfo dynInfo = dyn.faceInfo;
        if (baseInfo.scale > 0f)
        {
            dynInfo.scale = baseInfo.scale;
            dyn.faceInfo = dynInfo;
        }

        MarkPersistent(dyn);
        _processedFonts.Add(dyn.GetInstanceID());
        _dynamicTwins[srcId] = dyn;
        return dyn;
    }

    private static void MarkPersistent(TMP_FontAsset fontAsset)
    {
        if (fontAsset == null)
        {
            return;
        }
        fontAsset.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        if (fontAsset.material != null)
        {
            fontAsset.material.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        }
        Texture2D[] textures = fontAsset.atlasTextures;
        if (textures != null)
        {
            for (int i = 0; i < textures.Length; i++)
            {
                if (textures[i] != null)
                {
                    textures[i].hideFlags |= HideFlags.DontUnloadUnusedAsset;
                }
            }
        }
    }

    public static string Translate(string key)
    {
        EnsureInitialized();
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        bool isTurkishSlot = LocalisationService.currentLanguage == null ||
                             LocalisationService.currentLanguage.language == LocalisationService.Language.English;

        LocalisationService svc;
        if (ServiceManager.TryGetService<LocalisationService>(out svc) && svc != null)
        {
            string val = svc.Get(key);
            string trimmedKey = LocalisationService.NormalizeKey(key);
            if (!string.IsNullOrEmpty(val) && !string.Equals(val, key, StringComparison.Ordinal) && !string.Equals(val, trimmedKey, StringComparison.Ordinal))
            {
                if (key.EndsWith(" ", StringComparison.Ordinal) && !val.EndsWith(" ", StringComparison.Ordinal))
                {
                    return val + " ";
                }
                return val;
            }
        }

        if (isTurkishSlot)
        {
            string fallback;
            if (_extraTr.TryGetValue(key, out fallback))
            {
                return fallback;
            }
        }
        return key;
    }

    public static string GetInitialLanguageDisplayName(string raw)
    {
        EnsureInitialized();
        if (string.Equals(raw, "English", StringComparison.Ordinal))
        {
            return Translate("English");
        }
        return raw ?? string.Empty;
    }

    public static string GetSrtText(TextAsset asset)
    {
        EnsureInitialized();
        if (asset == null)
        {
            return string.Empty;
        }
        try
        {
            if (LocalisationService.currentLanguage == null ||
                LocalisationService.currentLanguage.language == LocalisationService.Language.English)
            {
                string name = asset.name;
                if (!string.IsNullOrEmpty(name))
                {
                    string subPath = Path.Combine(Application.streamingAssetsPath, "subtitles", name + ".srt");
                    if (File.Exists(subPath))
                    {
                        return File.ReadAllText(subPath, Encoding.UTF8);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TurkishPatch] Failed loading external SRT: " + ex.Message);
        }
        return asset.text;
    }
}
