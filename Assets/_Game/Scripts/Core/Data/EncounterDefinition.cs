using System.Collections.Generic;
using UnityEngine;

namespace VerdantSiege.Data
{
    /// <summary>One authored mission: battlefield, economy rules, roster and waves.</summary>
    [CreateAssetMenu(menuName = "Verdant Siege/Encounter", fileName = "Encounter_")]
    public sealed class EncounterDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "encounter";
        public string displayName = "Encounter";
        [TextArea(2, 3)] public string briefing = "";
        public string biomeId = "verdant_heartlands";

        [Header("Battlefield")]
        public BattlefieldDefinition battlefield;
        [Min(1)] public int heartrootHealth = 5;

        [Header("Vitality")]
        [Min(0)] public int startingVitality = 150;
        [Min(1)] public int vitalityCap = 1500;
        [Tooltip("Seconds between sunlight drops (0 = none).")] [Min(0f)] public float skyDropInterval = 9f;
        [Min(0f)] public float skyDropFirstDelay = 3f;
        [Min(1)] public int skyDropAmount = 25;
        [Tooltip("Seconds a pickup stays collectable.")] [Min(2f)] public float pickupLifetime = 12f;

        [Header("Sunlight Pools (Verdant Heartlands)")]
        [Tooltip("Seconds between sunlight pools appearing (0 = feature off).")] [Min(0f)] public float sunlightPoolInterval = 0f;
        [Min(1f)] public float sunlightPoolDuration = 9f;
        [Min(1)] public int sunlightPoolCount = 2;
        [Tooltip("Attack/generation speed multiplier for defenders standing in a pool.")] [Min(1f)] public float sunlightBoost = 1.6f;

        [Header("Flow")]
        [Tooltip("Seconds of calm before the first wave.")] [Min(0f)] public float prepTime = 20f;
        [Tooltip("Seconds between a cleared wave and the next.")] [Min(0f)] public float interWaveGap = 5f;
        [Tooltip("0 = new random seed every attempt.")] public int fixedSeed = 0;

        [Header("Content")]
        public List<DefenderDefinition> roster = new List<DefenderDefinition>();
        public List<WaveDefinition> waves = new List<WaveDefinition>();

        [Header("Boss (optional)")]
        [Tooltip("If set, the encounter is won by defeating the boss; waves act as its escort.")]
        public BossDefinition boss;
        [Tooltip("The boss rises when this wave starts (0 = with the first wave).")]
        [Min(0)] public int bossArrivesAtWave = 0;

        [Header("Rewards")]
        [Min(0)] public int seedReward = 50;
        [Tooltip("Extra seeds when the Heartroot is undamaged.")] [Min(0)] public int flawlessBonus = 25;
    }
}
