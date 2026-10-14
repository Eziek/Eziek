using System;

namespace Wyspa.World
{
    // Czyste C# (bez UnityEngine): generator, mapa i malarz wyspy dają się testować poza Unity.

    /// <summary>Kafelek świata 32x32: c = kolumna (w prawo), r = wiersz (w dół), jak w prototypie (tiles_proto.py).</summary>
    [Serializable]
    public struct TilePos : IEquatable<TilePos>
    {
        public int c;
        public int r;

        public TilePos(int c, int r)
        {
            this.c = c;
            this.r = r;
        }

        public bool Equals(TilePos o) => c == o.c && r == o.r;
        public override bool Equals(object o) => o is TilePos t && Equals(t);
        public override int GetHashCode() => c * 73856093 ^ r * 19349663;
        public override string ToString() => $"{c},{r}";
        public static bool operator ==(TilePos a, TilePos b) => a.Equals(b);
        public static bool operator !=(TilePos a, TilePos b) => !a.Equals(b);
    }

    public enum TileKind { Grass, Soil }

    public enum ZoneKind { None, Cemetery, Swamp, Ruins }

    /// <summary>Niespodzianka pod kafelkiem (GDD §13.4). Spring i Monolith to „dziwne kafelki”.</summary>
    public enum FindKind { None, Coins, NightSeed, Shard, Lore, Spring, Monolith }

    /// <summary>Dekoracje na lądzie (usuwanie w §14, Etap 3c).</summary>
    public enum DecoKind { TreeRound, Rock, Bush, DeadTree }

    /// <summary>Deterministyczny generator liczb (xorshift64*): ten sam wynik na każdym komputerze i w każdej wersji .NET.</summary>
    public class WRandom
    {
        ulong state;

        public WRandom(long seed)
        {
            state = (ulong)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL;
            if (state == 0)
                state = 0x1234567UL;
            for (int i = 0; i < 4; i++)
                Next();
        }

        ulong Next()
        {
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;
            return state * 0x2545F4914F6CDD1DUL;
        }

        /// <summary>0 &lt;= x &lt; 1</summary>
        public double Value() => (Next() >> 11) * (1.0 / (1UL << 53));

        public double Range(double a, double b) => a + (b - a) * Value();

        /// <summary>a &lt;= x &lt; b</summary>
        public int Range(int a, int b) => b <= a ? a : a + (int)(Value() * (b - a));
    }

    /// <summary>Hash położenia: stały „szum” przypięty do miejsca w świecie (zakup kafelka nie zmienia odległych pikseli).</summary>
    public static class WHash
    {
        public static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 0x27D4EB2Du;
                h ^= (uint)x * 0x165667B1u;
                h = (h << 13) | (h >> 19);
                h ^= (uint)y * 0xC2B2AE3Du;
                h *= 0x85EBCA77u;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>0 &lt;= x &lt; 1</summary>
        public static double Value(int x, int y, int seed) => Hash(x, y, seed) / 4294967296.0;
    }
}
