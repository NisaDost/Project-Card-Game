namespace HexPortal.Core
{
    /// <summary>S-01, S-02: a new match from one seed. Which human plays A (who moves first) is the client's choice
    /// (v2.8 S3); the engine only knows the seats A and B.</summary>
    public static class Match
    {
        /// <summary>The map (B-20), full pools, dealt hands (D-03, D-04), the open Market (D-05), and each player's quest
        /// and passive offers (S-03, S-04). The match is in the setup phase: players then choose and place (S-05).</summary>
        public static GameState Create(ulong seed)
        {
            var s = new GameState(MapGenerator.Generate(seed), seed);
            Pools.Deal(s);
            foreach (var p in new[] { PlayerId.A, PlayerId.B }) s.SetDealtHandCount(p, s.GetHand(p).Count);
            Pools.OpenMarket(s, new EventLog(s));
            Setup.Offer(s);
            return s;
        }
    }
}
