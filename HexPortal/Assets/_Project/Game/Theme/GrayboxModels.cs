using UnityEngine;

namespace HexPortal.Game
{
    /// <summary>Graybox low-poly model recipes (A-01…A-03), referenced from ArtCatalog. Feet at y = 0, facing +z.
    /// A later art phase replaces these references (e.g. with prefabs) in the catalog; views never name a class.</summary>
    public static class GrayboxModels
    {
        public static void Guardian(LowPoly.Builder b)
        {
            b.Prism(6, 0.4f, 0.34f, 0.1f, 0.8f, Vector3.zero, 30f);            // stocky body
            b.Ball(new Vector3(0f, 0.98f, 0f), new Vector3(0.22f, 0.2f, 0.22f));
            b.Prism(6, 0.24f, 0.1f, 1.02f, 1.22f, Vector3.zero, 30f);           // helmet
            b.Box(new Vector3(0f, 0.5f, 0.42f), new Vector3(0.62f, 0.72f, 0.1f)); // shield slab
            b.Box(new Vector3(0f, 0.5f, 0.48f), new Vector3(0.14f, 0.5f, 0.06f)); // shield boss
        }

        public static void Rider(LowPoly.Builder b)
        {
            b.Box(new Vector3(0f, 0.5f, 0f), new Vector3(0.36f, 0.32f, 0.85f));   // horse body
            for (int i = 0; i < 4; i++)
                b.Box(new Vector3(i % 2 == 0 ? -0.12f : 0.12f, 0.17f, i < 2 ? -0.3f : 0.3f), new Vector3(0.1f, 0.34f, 0.1f));
            b.Box(new Vector3(0f, 0.75f, 0.46f), new Vector3(0.2f, 0.4f, 0.18f), Quaternion.Euler(-30f, 0f, 0f)); // neck
            b.Box(new Vector3(0f, 0.92f, 0.6f), new Vector3(0.18f, 0.16f, 0.3f));                             // horse head
            b.Prism(5, 0.16f, 0.12f, 0.62f, 1.05f, new Vector3(0f, 0f, -0.05f));                              // rider
            b.Ball(new Vector3(0f, 1.17f, -0.05f), new Vector3(0.13f, 0.13f, 0.13f));
            b.Box(new Vector3(0.18f, 1.0f, 0.2f), new Vector3(0.05f, 0.05f, 0.7f), Quaternion.Euler(-15f, 0f, 0f)); // lance
        }

        public static void Archer(LowPoly.Builder b)
        {
            b.Prism(5, 0.22f, 0.15f, 0f, 0.85f, Vector3.zero);
            b.Ball(new Vector3(0f, 0.98f, 0f), new Vector3(0.15f, 0.15f, 0.15f));
            b.Prism(5, 0.18f, 0f, 0.98f, 1.3f, Vector3.zero);                   // hood
            for (int i = 0; i < 5; i++)                                           // bow arc
            {
                float a = Mathf.Lerp(-60f, 60f, i / 4f) * Mathf.Deg2Rad;
                var p = new Vector3(0.32f, 0.65f + Mathf.Sin(a) * 0.42f, 0.12f + Mathf.Cos(a) * 0.18f);
                b.Box(p, new Vector3(0.05f, 0.2f, 0.05f), Quaternion.Euler(-Mathf.Rad2Deg * a, 0f, 0f));
            }
            b.Box(new Vector3(0.32f, 0.65f, 0.12f), new Vector3(0.02f, 0.78f, 0.02f)); // string
        }

        public static void Mage(LowPoly.Builder b)
        {
            b.Prism(8, 0.42f, 0.1f, 0f, 0.95f, Vector3.zero, 22.5f);            // robe cone
            b.Ball(new Vector3(0f, 1.03f, 0f), new Vector3(0.16f, 0.16f, 0.16f));
            b.Prism(6, 0.26f, 0f, 1.1f, 1.55f, Vector3.zero);                   // hat
            b.Box(new Vector3(0.36f, 0.6f, 0.1f), new Vector3(0.05f, 1.1f, 0.05f)); // staff
            b.Ball(new Vector3(0.36f, 1.22f, 0.1f), new Vector3(0.13f, 0.13f, 0.13f), 5, 3); // orb
        }

        public static void Tower(LowPoly.Builder b)
        {
            b.Prism(6, 0.62f, 0.5f, 0f, 1.45f, Vector3.zero, 30f);
            b.Prism(6, 0.62f, 0.62f, 1.45f, 1.6f, Vector3.zero, 30f);
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.Deg2Rad * (60f * i);
                b.Box(new Vector3(Mathf.Cos(a) * 0.52f, 1.72f, Mathf.Sin(a) * 0.52f), new Vector3(0.2f, 0.24f, 0.2f), Quaternion.Euler(0f, -60f * i, 0f));
            }
            b.Prism(6, 0.4f, 0f, 1.6f, 2.15f, Vector3.zero, 30f);                // roof
            b.Box(new Vector3(0f, 0.3f, 0.6f), new Vector3(0.26f, 0.5f, 0.06f)); // door
        }

        public static void Healer(LowPoly.Builder b)
        {
            b.Ball(new Vector3(0f, 0.42f, 0f), new Vector3(0.36f, 0.4f, 0.36f), 7, 5); // rounded body
            b.Ball(new Vector3(0f, 0.92f, 0f), new Vector3(0.17f, 0.17f, 0.17f));
            b.Box(new Vector3(0f, 0.5f, 0.36f), new Vector3(0.1f, 0.34f, 0.06f));     // cross
            b.Box(new Vector3(0f, 0.54f, 0.36f), new Vector3(0.3f, 0.1f, 0.06f));
            for (int i = 0; i < 8; i++)                                               // halo
            {
                float a = Mathf.Deg2Rad * (45f * i);
                b.Box(new Vector3(Mathf.Cos(a) * 0.2f, 1.18f, Mathf.Sin(a) * 0.2f), new Vector3(0.1f, 0.03f, 0.04f), Quaternion.Euler(0f, -45f * i + 90f, 0f));
            }
        }

    }
}
