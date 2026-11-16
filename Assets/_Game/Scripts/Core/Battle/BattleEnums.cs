namespace VerdantSiege.Battle
{
    public enum BattleState
    {
        Running = 0,
        Won = 1,
        Lost = 2
    }

    /// <summary>Result of a deployment attempt. Anything except Ok leaves the battle unchanged.</summary>
    public enum DeployResult
    {
        Ok = 0,
        BattleOver,
        InvalidCard,
        OnCooldown,
        NotEnoughVitality,
        OutOfBounds,
        Occupied,
        BlockedTerrain,
        UnsupportedTerrain
    }

    public enum PickupSource
    {
        Sky = 0,
        Defender = 1,
        Enemy = 2
    }

    public enum EnemyActivity
    {
        Walking = 0,
        Attacking = 1
    }

    public static class DeployResultText
    {
        /// <summary>Short player-facing explanation.</summary>
        public static string Describe(DeployResult r)
        {
            switch (r)
            {
                case DeployResult.Ok: return "";
                case DeployResult.BattleOver: return "The battle is over";
                case DeployResult.InvalidCard: return "Choose a defender first";
                case DeployResult.OnCooldown: return "Still regrowing";
                case DeployResult.NotEnoughVitality: return "Not enough Vitality";
                case DeployResult.OutOfBounds: return "Plant inside the garden";
                case DeployResult.Occupied: return "That spot is taken";
                case DeployResult.BlockedTerrain: return "Nothing can grow there";
                case DeployResult.UnsupportedTerrain: return "Needs special ground";
                default: return r.ToString();
            }
        }
    }
}
