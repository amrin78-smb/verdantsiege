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
        Attacking = 1,  // biting a defender in contact
        Windup = 2,     // telegraphing an ability (crouch, sac inflate, shimmer); stands still
        Leaping = 3,    // Vaultling in the air over a defender
        Burrowed = 4,   // tunnelling: untargetable except by traps and splash
        Surfacing = 5,  // Burrower emerging (still untargetable, stands still)
        Ranged = 6      // stopped at range, attacking with projectiles (Sapper)
    }

    /// <summary>Defender ability state for presentation (shooters/blockers stay Ready).</summary>
    public enum DefenderAbilityState
    {
        Ready = 0,
        Windup = 1,     // Snapjaw opening its jaws, trap trembling
        Recovering = 2  // Snapjaw digesting, trap rearming
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
