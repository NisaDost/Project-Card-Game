using System;
using System.Collections.Generic;
using HexPortal.Core;
using HexPortal.Core.Data;
using UnityEngine;

namespace HexPortal.Game
{
    /// <summary>
    /// The ONE theme config for everything visible: display names, descriptions, icons, models and palettes, keyed by
    /// the Core code names (UnitClass, card/quest/passive/event ids, Biome, Marker). Rules and Core names never change
    /// with a theme; a later art phase swaps the data here (names, icon glyphs or sprites, model recipes or prefabs,
    /// palettes, sound ids). Views and UI look things up by key and never switch on a class themselves.
    /// Numbers in descriptions come from Catalog, never typed in here.
    /// </summary>
    public static class ArtCatalog
    {
        /// <summary>A unit class (or the tower): what the player sees.</summary>
        public sealed class UnitArt
        {
            public string Name;            // "Okçu"
            public string OwnRef;          // possessive for notices, "Okçun"
            public string Letter;          // one-letter tag
            public string Icon;            // glyph (a sprite reference can be added later)
            public string Ability;         // UX-11 ability line
            public Action<LowPoly.Builder> Model; // model recipe (a prefab reference can replace it later)
            public float ModelScale = 1.35f;
            public string SoundId;         // future, unused
        }

        /// <summary>A card, quest, passive or map event.</summary>
        public sealed class ItemArt
        {
            public string Name;
            public string Text;            // one-line description (UX-07)
            public string EffectIcon;      // buff/debuff chip glyph (cards only)
        }

        public sealed class BiomeArt { public string Name; public Color Color; }

        /// <summary>Icon: the small glyph above a unit standing on the marker (UX-16; empty = none).</summary>
        public sealed class MarkerArt { public Color Color; public Func<Mesh> Mesh; public string Icon = ""; }

        public static readonly Dictionary<UnitClass, UnitArt> Units = new Dictionary<UnitClass, UnitArt>
        {
            [UnitClass.Guardian] = new UnitArt { Name = "Muhafız", OwnRef = "Muhafızın", Letter = "M", Icon = "■",
                Ability = "Siper: Komşu dost birimler ve kule saldırıyla hedef alınamaz", Model = GrayboxModels.Guardian },
            [UnitClass.Rider] = new UnitArt { Name = "Süvari", OwnRef = "Süvarin", Letter = "S", Icon = "➜",
                Ability = "Atik: Birimlerin üstünden geçer. Hücum: İlerle-saldırda +" + Catalog.RiderChargeBonus + " Saldırı", Model = GrayboxModels.Rider },
            [UnitClass.Archer] = new UnitArt { Name = "Okçu", OwnRef = "Okçun", Letter = "O", Icon = "➶",
                Ability = "Uzak atış: Hat üzerindeki hiçbir şey atışı engellemez", Model = GrayboxModels.Archer },
            [UnitClass.Mage] = new UnitArt { Name = "Büyücü", OwnRef = "Büyücün", Letter = "B", Icon = "✦",
                Ability = "Patlama: Hedefe komşu düşmanlar " + Catalog.MageSplashDamage + " hasar alır", Model = GrayboxModels.Mage },
            [UnitClass.Healer] = new UnitArt { Name = "Şifacı", OwnRef = "Şifacın", Letter = "Ş", Icon = "✚",
                Ability = "Şifa: Tur başında komşu dostlar +" + Catalog.HealerHealAmount + " Can", Model = GrayboxModels.Healer },
        };

        public static readonly UnitArt Tower = new UnitArt { Name = "Kule", Icon = "♜", Model = GrayboxModels.Tower, ModelScale = 1f,
            Ability = "Hareket etmez. Rakibin turunda menzilinde duran, sahaya çıkan ya da saldıran tek birime 1 kez ateş eder" };

        /// <summary>UX-11 note on a unit whose move-attack has a charge bonus (U-02 Hücum).</summary>
        public static readonly string ChargeNote = " (+" + Catalog.RiderChargeBonus + " Hücum ile ilerle-saldırda)";

        public static readonly Dictionary<string, ItemArt> Items = new Dictionary<string, ItemArt>
        {
            ["C-10"] = new ItemArt { Name = "Öfke", Text = "+2 Saldırı, 2 tur", EffectIcon = "⚔+" },
            ["C-11"] = new ItemArt { Name = "Dev Gücü", Text = "+1 Saldırı, kalıcı", EffectIcon = "⚔+" },
            ["C-12"] = new ItemArt { Name = "Kalkan", Text = "Sonraki hasarı engeller", EffectIcon = "◈" },
            ["C-13"] = new ItemArt { Name = "Şifa İksiri", Text = "+5 Can", EffectIcon = "★" },
            ["C-14"] = new ItemArt { Name = "Rüzgâr Adımı", Text = "Bu tur +2 Hareket", EffectIcon = "★" },
            ["C-15"] = new ItemArt { Name = "Işınlanma", Text = "Dost birimi boş karoya taşır", EffectIcon = "★" },
            ["C-16"] = new ItemArt { Name = "Zayıflık", Text = "−2 Saldırı, 2 tur", EffectIcon = "⚔−" },
            ["C-17"] = new ItemArt { Name = "Zehir", Text = "Tur başında −2 Can, 2 tur", EffectIcon = "☠" },
            ["C-18"] = new ItemArt { Name = "İtme", Text = "Düz yönde 2 karo iter", EffectIcon = "★" },
            ["C-19"] = new ItemArt { Name = "Kök Salma", Text = "Hareket edemez, 2 tur", EffectIcon = "⚓" },
            ["C-20"] = new ItemArt { Name = "Diken Tuzağı", Text = "Tuzak: 3 hasar", EffectIcon = "★" },
            ["C-21"] = new ItemArt { Name = "Ayna Tuzağı", Text = "Tuzak: başlangıca ışınlar", EffectIcon = "★" },
            ["Q-10"] = new ItemArt { Name = "Rün Bekçisi", Text = "Aynı rün taşını tut" },
            ["Q-11"] = new ItemArt { Name = "Çift Rün", Text = "İki farklı rün taşında birimin olsun" },
            ["Q-12"] = new ItemArt { Name = "Avcı", Text = "2 düşman birimi yok et" },
            ["Q-13"] = new ItemArt { Name = "Kuşatma", Text = "Rakip kuleye toplam 10 hasar ver" },
            ["Q-14"] = new ItemArt { Name = "Sağlam Kale", Text = "10. raunt sonunda kule Canı 14 veya üstü" },
            ["Q-15"] = new ItemArt { Name = "Biyom Ustası", Text = "3 birimin aynı anda kendi biyomunda" },
            ["Q-16"] = new ItemArt { Name = "Kayıpsız", Text = "8. raunt sonuna kadar birim kaybetme" },
            ["Q-17"] = new ItemArt { Name = "Tuzakçı", Text = "Tuzağın bir düşmanda tetiklensin" },
            ["Q-18"] = new ItemArt { Name = "Kaynak Lordu", Text = "Kaynak karosunu tut" },
            ["Q-19"] = new ItemArt { Name = "Derin Akın", Text = "Rakip başlangıç bölgesinde 2 birimin olsun" },
            ["P-01"] = new ItemArt { Name = "Son Nefes", Text = "İlk ölen birimin 4 Can ile geri gelir" },
            ["P-02"] = new ItemArt { Name = "Tuzak Ustası", Text = "Tuzaklar +1 hasar verir" },
            ["P-03"] = new ItemArt { Name = "Hızlı Başlangıç", Text = "3. rauntta +2 Mana" },
            ["P-04"] = new ItemArt { Name = "Kalın Duvar", Text = "Kulene gelen ilk 5 hasar engellenir" },
            ["P-05"] = new ItemArt { Name = "Pazarcı", Text = "İlk Pazar alımında +1 kör kart" },
            ["P-06"] = new ItemArt { Name = "Portal Bekçisi", Text = "Portal'a giren rakip 3 hasar alır" },
            ["E-10"] = new ItemArt { Name = "Deprem", Text = "" },
            ["E-11"] = new ItemArt { Name = "Biyom Kayması", Text = "" },
            ["E-12"] = new ItemArt { Name = "Rün Yağmuru", Text = "" },
        };

        /// <summary>Card category glyphs (UX-07 card icon).</summary>
        public static readonly Dictionary<CardCategory, string> CategoryIcons = new Dictionary<CardCategory, string>
        {
            [CardCategory.Power] = "⚔", [CardCategory.Protection] = "◈", [CardCategory.Movement] = "➜",
            [CardCategory.Curse] = "☠", [CardCategory.Control] = "⚓", [CardCategory.Trap] = "✱",
        };

        /// <summary>A-02 biome palette (Forest green, Desert orange, Snow light blue).</summary>
        public static readonly Dictionary<Biome, BiomeArt> Biomes = new Dictionary<Biome, BiomeArt>
        {
            [Biome.Forest] = new BiomeArt { Name = "Orman", Color = new Color(0.30f, 0.70f, 0.30f) },
            [Biome.Desert] = new BiomeArt { Name = "Çöl", Color = new Color(0.95f, 0.60f, 0.20f) },
            [Biome.Snow] = new BiomeArt { Name = "Kar", Color = new Color(0.70f, 0.88f, 1.00f) },
        };
        public static readonly Color NoBiomeColor = new Color(0.75f, 0.45f, 0.90f);

        /// <summary>A-03 marker props. Marker.None has no prop.</summary>
        public static readonly Dictionary<Marker, MarkerArt> Markers = new Dictionary<Marker, MarkerArt>
        {
            [Marker.None] = new MarkerArt { Color = Color.clear, Mesh = null },
            [Marker.RuneStone] = new MarkerArt { Color = new Color(0.55f, 0.25f, 0.85f), Mesh = () => LowPoly.Rune, Icon = Strings.EnergyIcon },
            [Marker.Wellspring] = new MarkerArt { Color = new Color(0.1f, 0.85f, 0.95f), Mesh = () => LowPoly.Wellspring, Icon = Strings.ManaIcon },
            [Marker.Portal] = new MarkerArt { Color = new Color(0.95f, 0.3f, 0.9f), Mesh = () => LowPoly.Portal },
            [Marker.Rock] = new MarkerArt { Color = new Color(0.35f, 0.33f, 0.32f), Mesh = () => LowPoly.Rock },
            [Marker.Void] = new MarkerArt { Color = Color.clear, Mesh = null }, // B-25: never drawn
        };
        public static readonly Color FogColor = new Color(0.16f, 0.17f, 0.24f); // UX-09 Hidden cloud
        public static readonly Color OwnTrapColor = new Color(0.8f, 0.3f, 1f);
        public static readonly Color HeldGlow = new Color(1f, 0.85f, 0.3f);
        public static readonly Color OwnerA = new Color(0.20f, 0.45f, 1.00f);
        public static readonly Color OwnerB = new Color(0.95f, 0.20f, 0.25f);

        /// <summary>Future: event type name → sound id (A-04, Faz 2). Empty until audio exists.</summary>
        public static readonly Dictionary<string, string> EventSounds = new Dictionary<string, string>();

        // ---------- Lookups (the only way views and UI reach theme data) ----------

        public static UnitArt Unit(UnitClass c) => Units[c];
        public static string UnitName(UnitClass c) => Units[c].Name;
        public static string UnitIcon(UnitClass c) => Units[c].Icon;
        public static string UnitLetter(UnitClass c) => Units[c].Letter;
        public static string Ability(UnitClass c) => Units[c].Ability;
        /// <summary>"Okçun" (own) / "Rakip Okçu" (enemy) for notices and the event log.</summary>
        public static string UnitRef(UnitClass c, bool own) => own ? Units[c].OwnRef : Strings.EnemyPrefix + Units[c].Name;
        public static Mesh UnitMesh(UnitClass c) => LowPoly.Model("unit." + c, Units[c].Model);
        public static Mesh TowerMesh => LowPoly.Model("tower", Tower.Model);

        public static string Name(string id) => id != null && Items.TryGetValue(id, out var a) ? a.Name : id ?? "";
        public static string Text(string id) => id != null && Items.TryGetValue(id, out var a) ? a.Text : "";
        public static string EffectIcon(string id) => id != null && Items.TryGetValue(id, out var a) && a.EffectIcon != null ? a.EffectIcon : "★";
        public static string CardIcon(CardCategory c) => CategoryIcons.TryGetValue(c, out var s) ? s : "✱";

        public static string BiomeName(Biome b) => Biomes.TryGetValue(b, out var a) ? a.Name : "";
        public static Color BiomeColor(Biome b) => Biomes.TryGetValue(b, out var a) ? a.Color : NoBiomeColor;
        public static Color OwnerColor(PlayerId p) => p == PlayerId.A ? OwnerA : OwnerB;
        public static MarkerArt MarkerOf(Marker m) => Markers[m];
    }
}
