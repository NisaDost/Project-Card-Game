using System;
using System.Linq;
using HexPortal.Core.Data;

namespace HexPortal.Sim
{
    // AI-vs-AI batch simulator. M0: argument parsing and a Catalog summary only; matches arrive in M5.
    public static class Program
    {
        public static int Main(string[] args)
        {
            int games = 1000;
            ulong seed = 1;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--games" && i + 1 < args.Length) games = int.Parse(args[++i]);
                else if (args[i] == "--seed" && i + 1 < args.Length) seed = ulong.Parse(args[++i]);
                else
                {
                    Console.Error.WriteLine("Usage: Sim [--games N] [--seed S]");
                    return 1;
                }
            }

            Console.WriteLine($"HexPortal Sim (M0) games={games} seed={seed}");
            Console.WriteLine($"Units: {Catalog.Units.Count}  Character cards: {Catalog.CharacterCards.Sum(c => c.Copies)}");
            Console.WriteLine($"Support cards: {Catalog.SupportCards.Sum(c => c.Copies)}  Quests: {Catalog.Quests.Count}  Passives: {Catalog.Passives.Count}  Events: {Catalog.MapEvents.Count}");
            Console.WriteLine($"Mana cap: {Catalog.ManaCap}  Energy/turn: {Catalog.EnergyPerTurn}  Round limit: {Catalog.RoundLimit}");
            Console.WriteLine("Match simulation is not implemented yet (M5).");
            return 0;
        }
    }
}
