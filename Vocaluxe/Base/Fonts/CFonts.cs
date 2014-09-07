#region license
// This file is part of Vocaluxe.
// 
// Vocaluxe is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Vocaluxe is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Vocaluxe. If not, see <http://www.gnu.org/licenses/>.
#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml;
using VocaluxeLib;
using VocaluxeLib.Menu;

namespace Vocaluxe.Base.Fonts
{
    /// <summary>
    ///     Struct used for describing a font family (a type of text with 4 different styles)
    /// </summary>
    struct SFontFamily
    {
        public string Name;

        public int PartyModeID;
        public string ThemeName;
        public string Folder;

        public string FileNormal;
        public string FileItalic;
        public string FileBold;
        public string FileBoldItalic;

        public float Outline; //0..1, 0=not outline 1=100% outline
        public SColorF OutlineColor;

        public CFontStyle Normal;
        public CFontStyle Italic;
        public CFontStyle Bold;
        public CFontStyle BoldItalic;
    }

    static class CFonts
    {
        private static bool _IsInitialized;
        private static readonly List<SFontFamily> _FontFamilies = new List<SFontFamily>();
        private static readonly List<String> _LoggedMissingFonts = new List<string>();

        public static int PartyModeID { get; set; }

        public static bool Init()
        {
            if (_IsInitialized)
                return false;
            PartyModeID = -1;
            return _LoadDefaultFonts();
        }

        public static void Close()
        {
            foreach (SFontFamily font in _FontFamilies)
            {
                font.Normal.Dispose();
                font.Bold.Dispose();
                font.Italic.Dispose();
                font.BoldItalic.Dispose();
            }
            _FontFamilies.Clear();
            _IsInitialized = false;
        }

        #region DrawText
        /// <summary>
        ///     Draws a black text
        /// </summary>
        /// <param name="text">The text to be drawn</param>
        /// <param name="font">The texts font</param>
        /// <param name="x">The texts x-position</param>
        /// <param name="y">The texts y-position</param>
        /// <param name="z">The texts z-position</param>
        public static void DrawText(string text, CFont font, float x, float y, float z)
        {
            DrawText(text, font, x, y, z, new SColorF(0f, 0f, 0f, 1f));
        }

        /// <summary>
        ///     Draws a text
        /// </summary>
        /// <param name="text">The text to be drawn</param>
        /// <param name="font">The texts font</param>
        /// <param name="x">The texts x-position</param>
        /// <param name="y">The texts y-position</param>
        /// <param name="z">The texts z-position</param>
        /// <param name="color">The text color</param>
        public static void DrawText(string text, CFont font, float x, float y, float z, SColorF color)
        {
            if (font.Height <= 0f || text == "")
                return;

            CFontStyle fontStyle = _GetFontStyle(font);

            float dx = x;
            foreach (char chr in text)
            {
                fontStyle.DrawGlyph(chr, font.Height, dx, y, z, color);
                dx += fontStyle.GetWidth(chr, font.Height);
            }
        }

        public static void DrawTextReflection(string text, CFont font, float x, float y, float z, SColorF color, float rspace, float rheight)
        {
            if (font.Height <= 0f || text == "")
                return;

            CFontStyle fontStyle = _GetFontStyle(font);

            float dx = x;
            foreach (char chr in text)
            {
                fontStyle.DrawGlyphReflection(chr, font.Height, dx, y, z, color, rspace, rheight);
                dx += fontStyle.GetWidth(chr, font.Height);
            }
        }

        public static void DrawText(string text, CFont font, float x, float y, float z, SColorF color, float begin, float end)
        {
            if (font.Height <= 0f || text == "")
                return;

            float w = GetTextWidth(text, font);
            if (w <= 0f)
                return;

            float xStart = x + w * begin;
            float xEnd = x + w * end;
            float xCur = x;

            CFontStyle fontStyle = _GetFontStyle(font);

            foreach (char chr in text)
            {
                float w2 = fontStyle.GetWidth(chr, font.Height);
                float b = (xStart - xCur) / w2;

                if (b < 1f)
                {
                    if (b < 0f)
                        b = 0f;
                    float e = (xEnd - xCur) / w2;
                    if (e > 0f)
                    {
                        if (e > 1f)
                            e = 1f;
                        fontStyle.DrawGlyph(chr, font.Height, xCur, y, z, color, b, e);
                    }
                }
                xCur += w2;
            }
        }
        #endregion DrawText

        /// <summary>
        ///     Calculates the bounds for a CText object
        /// </summary>
        /// <param name="text">The CText object of which the bounds should be calculated for</param>
        /// <returns>RectangleF object containing the bounds</returns>
        public static RectangleF GetTextBounds(CText text)
        {
            return new RectangleF(text.X, text.Y, GetTextWidth(text.TranslatedText, text.Font), GetTextHeight(text.TranslatedText, text.Font));
        }

        private static CFontStyle _GetFontStyle(CFont font)
        {
            int index = _GetPartyFontIndex(font.Name, PartyModeID);
            if (index < 0)
                index = _GetThemeFontIndex(font.Name, CConfig.Theme);
            if (index < 0)
                index = _GetFontIndex(font.Name);
            if (index < 0)
            {
                if (!_LoggedMissingFonts.Contains(font.Name))
                {
                    _LoggedMissingFonts.Add(font.Name);
                    CLog.LogError("Font \"" + font.Name + "\" not found!");
                }
                index = 0;
            }

            switch (font.Style)
            {
                case EStyle.Normal:
                    return _FontFamilies[index].Normal;
                case EStyle.Italic:
                    return _FontFamilies[index].Italic;
                case EStyle.Bold:
                    return _FontFamilies[index].Bold;
                case EStyle.BoldItalic:
                    return _FontFamilies[index].BoldItalic;
            }
            throw new ArgumentException("Invalid Style: " + font.Style);
        }

        public static float GetTextWidth(string text, CFont font)
        {
            CFontStyle fontStyle = _GetFontStyle(font);
            return text.Sum(chr => fontStyle.GetWidth(chr, font.Height));
        }

        public static float GetTextHeight(string text, CFont font)
        {
            CFontStyle fontStyle = _GetFontStyle(font);
            return text == "" ? 0 : text.Select(chr => fontStyle.GetHeight(chr, font.Height)).Max();
        }

        private static int _GetFontIndex(string fontName)
        {
            for (int i = 0; i < _FontFamilies.Count; i++)
            {
                if (_FontFamilies[i].Name == fontName)
                    return i;
            }

            return -1;
        }

        private static int _GetThemeFontIndex(string fontName, string themeName)
        {
            if (themeName == "" || fontName == "")
                return -1;

            for (int i = 0; i < _FontFamilies.Count; i++)
            {
                if (_FontFamilies[i].Name == fontName && _FontFamilies[i].ThemeName == themeName)
                    return i;
            }

            return -1;
        }

        private static int _GetPartyFontIndex(string fontName, int partyModeID)
        {
            if (partyModeID == -1 || fontName == "")
                return -1;

            for (int i = 0; i < _FontFamilies.Count; i++)
            {
                if (_FontFamilies[i].PartyModeID == partyModeID && _FontFamilies[i].Name == fontName)
                    return i;
            }

            return -1;
        }

        /// <summary>
        ///     Load default fonts
        /// </summary>
        /// <returns></returns>
        private static bool _LoadDefaultFonts()
        {
            CXMLReader xmlReader = CXMLReader.OpenFile(Path.Combine(CSettings.ProgramFolder, CSettings.FolderNameFonts, CSettings.FileNameFonts));
            if (xmlReader == null)
                return false;

            return _LoadFontFile(xmlReader, Path.Combine(CSettings.ProgramFolder, CSettings.FolderNameFonts));
        }

        /// <summary>
        ///     Loads theme fonts from skin file
        /// </summary>
        public static bool LoadThemeFonts(string themeName, string fontFolder, CXMLReader xmlReader)
        {
            bool ok = _LoadFontFile(xmlReader, fontFolder, themeName);
            CLog.StartBenchmark("BuildGlyphs");
            _BuildGlyphs();
            CLog.StopBenchmark("BuildGlyphs");
            return ok;
        }

        /// <summary>
        ///     Loads party mode fonts from skin file
        /// </summary>
        public static bool LoadPartyModeFonts(int partyModeID, string fontFolder, CXMLReader xmlReader)
        {
            bool ok = _LoadFontFile(xmlReader, fontFolder, "", partyModeID);
            CLog.StartBenchmark("BuildGlyphs");
            _BuildGlyphs();
            CLog.StopBenchmark("BuildGlyphs");
            return ok;
        }

        private static bool _LoadFontFile(CXMLReader xmlReader, string fontFolder, string themeName = "", int partyModeId = -1)
        {
            string value;
            int i = 1;
            while (xmlReader.GetValue("//root/Fonts/Font" + i + "/Folder", out value))
            {
                var sf = new SFontFamily {Folder = value, ThemeName = themeName, PartyModeID = partyModeId};

                bool ok = true;

                ok &= xmlReader.GetValue("//root/Fonts/Font" + i + "/Name", out sf.Name);
                ok &= xmlReader.GetValue("//root/Fonts/Font" + i + "/FileNormal", out sf.FileNormal);
                ok &= xmlReader.GetValue("//root/Fonts/Font" + i + "/FileItalic", out sf.FileItalic);
                ok &= xmlReader.GetValue("//root/Fonts/Font" + i + "/FileBold", out sf.FileBold);
                ok &= xmlReader.GetValue("//root/Fonts/Font" + i + "/FileBoldItalic", out sf.FileBoldItalic);
                ok &= xmlReader.TryGetNormalizedFloatValue("//root/Fonts/Font" + i + "/Outline", ref sf.Outline);
                ok &= xmlReader.TryGetColorFromRGBA("//root/Fonts/Font" + i + "/OutlineColor", ref sf.OutlineColor);

                if (ok)
                {
                    sf.Normal = new CFontStyle(Path.Combine(fontFolder, sf.Folder, sf.FileNormal), EStyle.Normal, sf.Outline, sf.OutlineColor);
                    sf.Italic = new CFontStyle(Path.Combine(fontFolder, sf.Folder, sf.FileItalic), EStyle.Italic, sf.Outline, sf.OutlineColor);
                    sf.Bold = new CFontStyle(Path.Combine(fontFolder, sf.Folder, sf.FileBold), EStyle.Bold, sf.Outline, sf.OutlineColor);
                    sf.BoldItalic = new CFontStyle(Path.Combine(fontFolder, sf.Folder, sf.FileBoldItalic), EStyle.BoldItalic, sf.Outline, sf.OutlineColor);
                    _FontFamilies.Add(sf);
                }
                else
                {
                    string fontTypes;
                    if (partyModeId >= 0)
                        fontTypes = "theme fonts for party mode";
                    else if (themeName != "")
                        fontTypes = "theme fonts for theme \"" + themeName + "\"";
                    else
                        fontTypes = "basic fonts";
                    CLog.LogError("Error loading " + fontTypes + ": Error in Font" + i);
                    return false;
                }
                i++;
            }
            return true;
        }

        public static void SaveThemeFonts(string themeName, XmlWriter writer)
        {
            if (_FontFamilies.Count == 0)
                return;

            writer.WriteStartElement("Fonts");
            int fontNr = 1;
            foreach (SFontFamily font in _FontFamilies)
            {
                if (font.ThemeName == themeName)
                {
                    writer.WriteStartElement("Font" + fontNr);

                    writer.WriteElementString("Name", font.Name);
                    writer.WriteElementString("Folder", font.Folder);

                    writer.WriteElementString("Outline", font.Outline.ToString("#0.00"));
                    writer.WriteStartElement("OutlineColor");
                    writer.WriteElementString("R", font.OutlineColor.R.ToString("#0.00"));
                    writer.WriteElementString("G", font.OutlineColor.G.ToString("#0.00"));
                    writer.WriteElementString("B", font.OutlineColor.B.ToString("#0.00"));
                    writer.WriteElementString("A", font.OutlineColor.A.ToString("#0.00"));
                    writer.WriteEndElement();

                    writer.WriteElementString("FileNormal", font.FileNormal);
                    writer.WriteElementString("FileBold", font.FileBold);
                    writer.WriteElementString("FileItalic", font.FileItalic);
                    writer.WriteElementString("FileBoldItalic", font.FileBoldItalic);

                    writer.WriteEndElement();

                    fontNr++;
                }
            }

            writer.WriteEndElement();
        }

        public static void UnloadThemeFonts(string themeName)
        {
            int index = 0;
            while (index < _FontFamilies.Count)
            {
                if (_FontFamilies[index].ThemeName == themeName)
                {
                    _FontFamilies[index].Normal.Dispose();
                    _FontFamilies[index].Italic.Dispose();
                    _FontFamilies[index].Bold.Dispose();
                    _FontFamilies[index].BoldItalic.Dispose();
                    _FontFamilies.RemoveAt(index);
                }
                else
                    index++;
            }
        }

        private static void _BuildGlyphs()
        {
            const string text = " abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";

            foreach (SFontFamily fontFamily in _FontFamilies)
            {
                foreach (char chr in text)
                {
                    fontFamily.Normal.GetOrAddGlyph(chr, -1);
                    fontFamily.Bold.GetOrAddGlyph(chr, -1);
                    fontFamily.Italic.GetOrAddGlyph(chr, -1);
                    fontFamily.BoldItalic.GetOrAddGlyph(chr, -1);
                }
            }
        }
    }
}