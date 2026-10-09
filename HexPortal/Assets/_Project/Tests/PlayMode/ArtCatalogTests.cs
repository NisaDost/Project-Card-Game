using System;
using System.Linq;
using HexPortal.Core;
using HexPortal.Core.Data;
using NUnit.Framework;

namespace HexPortal.Game.Tests
{
    /// <summary>The theme config (ArtCatalog) covers every Core key that can be shown: classes, the tower, cards,
    /// quests, passives, map events, card categories, biomes and markers.</summary>
    public class ArtCatalogTests
    {
        [Test]
        public void Theme_EveryKeyHasAnEntry()
        {
            foreach (UnitClass c in Enum.GetValues(typeof(UnitClass)))
            {
                Assert.That(ArtCatalog.Units.ContainsKey(c), Is.True, c.ToString());
                var a = ArtCatalog.Unit(c);
                Assert.That(a.Name, Is.Not.Empty, c + " name");
                Assert.That(a.OwnRef, Is.Not.Empty, c + " own ref");
                Assert.That(a.Icon, Is.Not.Empty, c + " icon");
                Assert.That(a.Ability, Is.Not.Empty, c + " ability");
                Assert.That(ArtCatalog.UnitMesh(c).vertexCount, Is.GreaterThan(0), c + " model");
            }
            Assert.That(ArtCatalog.Tower.Name, Is.Not.Empty);
            Assert.That(ArtCatalog.TowerMesh.vertexCount, Is.GreaterThan(0));

            var ids = Catalog.SupportCards.Select(d => d.Id).Concat(Catalog.Quests.Select(q => q.Id))
                .Concat(Catalog.Passives.Select(p => p.Id)).Concat(Catalog.MapEvents.Select(e => e.Id));
            foreach (var id in ids)
            {
                Assert.That(ArtCatalog.Items.ContainsKey(id), Is.True, id);
                Assert.That(ArtCatalog.Name(id), Is.Not.EqualTo(id).And.Not.Empty, id + " name");
            }
            foreach (var d in Catalog.SupportCards) Assert.That(ArtCatalog.Text(d.Id), Is.Not.Empty, d.Id + " text");
            foreach (CardCategory c in Enum.GetValues(typeof(CardCategory)))
                Assert.That(ArtCatalog.CategoryIcons.ContainsKey(c), Is.True, c.ToString());
            foreach (Biome b in Enum.GetValues(typeof(Biome)))
                if (b != Biome.None) Assert.That(ArtCatalog.Biomes.ContainsKey(b), Is.True, b.ToString());
            foreach (Marker m in Enum.GetValues(typeof(Marker)))
            {
                Assert.That(ArtCatalog.Markers.ContainsKey(m), Is.True, m.ToString());
                if (m != Marker.None && m != Marker.Void) // B-25: Void is never drawn
                    Assert.That(ArtCatalog.MarkerOf(m).Mesh().vertexCount, Is.GreaterThan(0), m + " prop");
            }
        }

        /// <summary>B-15 river tile must not read as Player A (blue) or B (red): clearly different hue and visible contrast.</summary>
        [Test]
        public void Theme_RiverDistinctFromOwnerColours()
        {
            var river = ArtCatalog.MarkerOf(Marker.River).TileColor;
            UnityEngine.Color.RGBToHSV(river, out float hr, out _, out _);
            foreach (var owner in new[] { ArtCatalog.OwnerA, ArtCatalog.OwnerB })
            {
                UnityEngine.Color.RGBToHSV(owner, out float ho, out _, out _);
                float d = UnityEngine.Mathf.Abs(hr - ho); d = UnityEngine.Mathf.Min(d, 1f - d);
                Assert.That(d * 360f, Is.GreaterThan(35f), "hue distance to " + owner);
                float rgb = UnityEngine.Mathf.Abs(river.r - owner.r) + UnityEngine.Mathf.Abs(river.g - owner.g) + UnityEngine.Mathf.Abs(river.b - owner.b);
                Assert.That(rgb, Is.GreaterThan(0.5f), "RGB distance to " + owner);
            }
        }
    }
}
