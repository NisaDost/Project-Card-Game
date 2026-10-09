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

        /// <summary>U-44 Portal Bearer: a slim robed courier carrying a standing portal ring above its head.</summary>
        public static void PortalBearer(LowPoly.Builder b)
        {
            b.Prism(6, 0.3f, 0.14f, 0f, 0.85f, Vector3.zero, 30f);            // robe
            b.Ball(new Vector3(0f, 0.97f, 0f), new Vector3(0.15f, 0.15f, 0.15f));
            for (int i = 0; i < 10; i++)                                        // portal ring held aloft
            {
                float a = Mathf.PI * 2f * i / 10f;
                b.Box(new Vector3(Mathf.Cos(a) * 0.3f, 1.55f + Mathf.Sin(a) * 0.3f, 0f), new Vector3(0.2f, 0.07f, 0.08f),
                    Quaternion.Euler(0f, 0f, Mathf.Rad2Deg * a + 90f));
            }
            b.Box(new Vector3(0.24f, 0.95f, 0f), new Vector3(0.06f, 0.6f, 0.06f));  // arms up to the ring
            b.Box(new Vector3(-0.24f, 0.95f, 0f), new Vector3(0.06f, 0.6f, 0.06f));
        }

        /// <summary>U-40 Gatherer: a short worker with a big sack on the back and a pickaxe over the shoulder.</summary>
        public static void Gatherer(LowPoly.Builder b)
        {
            b.Prism(6, 0.26f, 0.2f, 0f, 0.7f, Vector3.zero, 30f);               // body
            b.Ball(new Vector3(0f, 0.84f, 0f), new Vector3(0.15f, 0.15f, 0.15f));
            b.Prism(6, 0.19f, 0.19f, 0.9f, 0.96f, Vector3.zero, 30f);           // cap brim
            b.Ball(new Vector3(0f, 0.6f, -0.32f), new Vector3(0.28f, 0.3f, 0.24f), 6, 4); // sack
            b.Box(new Vector3(0f, 0.93f, -0.3f), new Vector3(0.12f, 0.08f, 0.12f));      // sack knot
            b.Box(new Vector3(0.27f, 0.75f, 0.08f), new Vector3(0.05f, 0.75f, 0.05f), Quaternion.Euler(20f, 0f, 0f)); // handle
            b.Box(new Vector3(0.27f, 1.1f, 0.21f), new Vector3(0.06f, 0.07f, 0.5f), Quaternion.Euler(20f, 0f, 0f));  // pick head
        }

        /// <summary>U-41 Besieger: a low wheeled battering ram with a pointed head.</summary>
        public static void Besieger(LowPoly.Builder b)
        {
            b.Box(new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.14f, 0.8f));     // chassis
            for (int i = 0; i < 4; i++)                                              // wheels
                b.Ball(new Vector3(i % 2 == 0 ? -0.3f : 0.3f, 0.17f, i < 2 ? -0.26f : 0.26f), new Vector3(0.05f, 0.16f, 0.16f), 8, 3);
            b.Box(new Vector3(0f, 0.5f, 0f), new Vector3(0.2f, 0.2f, 0.9f));       // ram log
            b.Box(new Vector3(0f, 0.5f, 0.5f), new Vector3(0.22f, 0.22f, 0.22f), Quaternion.Euler(0f, 0f, 45f)); // iron head
            b.Prism(4, 0.38f, 0.05f, 0.62f, 1.05f, Vector3.zero, 45f);              // roof
        }

        /// <summary>U-42 Assassin: slim, hooded, crouched forward with two daggers.</summary>
        public static void Assassin(LowPoly.Builder b)
        {
            b.Prism(5, 0.15f, 0.11f, 0f, 0.78f, Vector3.zero);                     // slim body
            b.Ball(new Vector3(0f, 0.9f, 0.04f), new Vector3(0.12f, 0.13f, 0.12f));
            b.Prism(5, 0.15f, 0f, 0.9f, 1.25f, new Vector3(0f, 0f, -0.03f));       // pointed hood
            b.Box(new Vector3(0f, 0.55f, -0.16f), new Vector3(0.3f, 0.5f, 0.04f), Quaternion.Euler(-10f, 0f, 0f)); // cape
            for (int s = -1; s <= 1; s += 2)
            {
                b.Box(new Vector3(0.2f * s, 0.5f, 0.12f), new Vector3(0.05f, 0.05f, 0.12f));                    // grip
                b.Box(new Vector3(0.2f * s, 0.55f, 0.32f), new Vector3(0.03f, 0.1f, 0.3f), Quaternion.Euler(-20f, 0f, 0f)); // blade
            }
        }

        /// <summary>U-43 Flyer: a round bird-like body with wide wings and a tail. Lifted by ArtCatalog Hover.</summary>
        public static void Flyer(LowPoly.Builder b)
        {
            b.Ball(new Vector3(0f, 0.42f, 0f), new Vector3(0.22f, 0.22f, 0.3f), 6, 4); // body
            b.Ball(new Vector3(0f, 0.62f, 0.25f), new Vector3(0.13f, 0.13f, 0.13f));     // head
            b.Box(new Vector3(0f, 0.6f, 0.4f), new Vector3(0.07f, 0.06f, 0.14f));        // beak
            for (int s = -1; s <= 1; s += 2)
            {
                b.Box(new Vector3(0.36f * s, 0.55f, 0f), new Vector3(0.5f, 0.04f, 0.3f), Quaternion.Euler(0f, 0f, 25f * s)); // inner wing
                b.Box(new Vector3(0.76f * s, 0.78f, -0.04f), new Vector3(0.36f, 0.03f, 0.22f), Quaternion.Euler(0f, 0f, 40f * s)); // wing tip
            }
            b.Box(new Vector3(0f, 0.45f, -0.36f), new Vector3(0.26f, 0.03f, 0.2f), Quaternion.Euler(15f, 0f, 0f)); // tail
        }
    }
}
