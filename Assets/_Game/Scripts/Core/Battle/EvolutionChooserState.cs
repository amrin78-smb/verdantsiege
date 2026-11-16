using System;

namespace VerdantSiege.Battle
{
    /// <summary>
    /// The evolution chooser's rules, kept out of the UI so they are testable: which defender it is open for,
    /// when it opens (tapping a ready defender), when it closes (tapping elsewhere, choosing successfully, the
    /// defender dying/evolving, the battle ending) and what a choice does (an atomic <see cref="BattleSimulation.TryEvolve"/>).
    /// Battle time keeps running while it is open.
    /// </summary>
    public sealed class EvolutionChooserState
    {
        private readonly BattleSimulation _sim;
        private int _targetId;

        public DefenderRuntime Target { get; private set; }
        public bool IsOpen => Target != null;
        /// <summary>Raised whenever the chooser opens, closes or switches defender.</summary>
        public event Action Changed;
        /// <summary>A choice was refused (result) with the chooser kept open (e.g. not enough Vitality).</summary>
        public event Action<EvolveResult> Denied;

        public EvolutionChooserState(BattleSimulation sim) => _sim = sim ?? throw new ArgumentNullException(nameof(sim));

        /// <summary>Opens for a ready defender. Returns false (and stays as it was) otherwise.</summary>
        public bool Open(DefenderRuntime d)
        {
            if (_sim.State != BattleState.Running || d == null || !d.EvolutionReady) return false;
            if (Target == d && _targetId == d.Id) return true;
            Target = d;
            _targetId = d.Id;
            Changed?.Invoke();
            return true;
        }

        public void Close()
        {
            if (Target == null) return;
            Target = null;
            _targetId = 0;
            Changed?.Invoke();
        }

        /// <summary>
        /// A tap on the battlefield resolved to <paramref name="tapped"/> (null = empty ground). A ready defender opens
        /// (or switches) the chooser; anything else closes it. Returns true when the chooser is open afterwards.
        /// </summary>
        public bool OnBattlefieldTap(DefenderRuntime tapped)
        {
            if (tapped != null && tapped.EvolutionReady) return Open(tapped);
            Close();
            return false;
        }

        /// <summary>Applies option <paramref name="option"/>. Success closes; a refusal it can recover from keeps it open.</summary>
        public EvolveResult Choose(int option)
        {
            if (!Validate()) return EvolveResult.NoDefender;
            EvolveResult r = _sim.TryEvolve(Target, option);
            if (r == EvolveResult.Ok) Close();
            else if (r == EvolveResult.NotEnoughVitality || r == EvolveResult.InvalidOption) Denied?.Invoke(r);
            else Close();
            return r;
        }

        /// <summary>Closes when the target is gone (died, recycled), already evolved, or the battle ended. Returns IsOpen.</summary>
        public bool Validate()
        {
            if (Target == null) return false;
            if (_sim.State != BattleState.Running || !Target.alive || Target.Id != _targetId || Target.evolution != null) Close();
            return IsOpen;
        }
    }
}
