namespace EliteDarts.Entities
{
    public class MatchStatsDto
    {
        public string Player1Name { get; set; } = "";
        public string Player2Name { get; set; } = "";

        public double Player1ThreeDartAverage { get; set; }
        public double Player2ThreeDartAverage { get; set; }

        public double Player1First9Average { get; set; }
        public double Player2First9Average { get; set; }

        public int Player1HighestCheckout { get; set; }
        public int Player2HighestCheckout { get; set; }

        public int Player1180Count { get; set; }
        public int Player2180Count { get; set; }

        public int Player1140PlusCount { get; set; }
        public int Player2140PlusCount { get; set; }

        public int Player1100PlusCount { get; set; }
        public int Player2100PlusCount { get; set; }

        public int Player1ShortestLegDarts { get; set; }
        public int Player2ShortestLegDarts { get; set; }

        public string WinnerName { get; set; } = "";
        public string FinalScoreText { get; set; } = "";
    }
}