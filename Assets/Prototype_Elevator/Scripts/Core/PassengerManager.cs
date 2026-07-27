using System.Collections.Generic;
using UnityEngine;

namespace Ascend.Prototype
{
    /// <summary>Generates floor passenger candidates and owns the boarded passenger load.</summary>
    public class PassengerManager : MonoBehaviour
    {
        [SerializeField] private PrototypeConfig _config;
        [SerializeField] private List<PassengerDefinition> _pool = new List<PassengerDefinition>();

        private readonly List<PassengerDefinition> _candidates = new List<PassengerDefinition>();
        private readonly List<PassengerDefinition> _boarded = new List<PassengerDefinition>();
        private readonly List<EffectDefinition> _activeEffects = new List<EffectDefinition>();
        private System.Random _rng = new System.Random(0);

        public IReadOnlyList<PassengerDefinition> Candidates => _candidates;
        public IReadOnlyList<PassengerDefinition> Boarded => _boarded;

        public float TotalWeight
        {
            get
            {
                float total = 0f;
                foreach (PassengerDefinition passenger in _boarded)
                    if (passenger != null) total += passenger.weight;
                return total;
            }
        }

        public float TotalAllowedWeightBonus
        {
            get
            {
                float total = 0f;
                foreach (PassengerDefinition passenger in _boarded)
                    if (passenger != null) total += passenger.allowedWeightBonus;
                return total;
            }
        }

        public IReadOnlyList<EffectDefinition> ActiveEffects => _activeEffects;

        /// <summary>Initialises the deterministic candidate RNG for this run.</summary>
        public void InitializeSeed(int seed)
        {
            _rng = new System.Random(unchecked(seed * 31 ^ 0x9A55));
        }

        /// <summary>Clears all boarded passengers and current candidates.</summary>
        public void ResetPassengers()
        {
            _candidates.Clear();
            _boarded.Clear();
            RebuildActiveEffects();
        }

        /// <summary>Draws this floor's candidates without replacement from the configured pool.</summary>
        public void GenerateCandidates(int floorIndex)
        {
            _candidates.Clear();

            if (_config == null)
            {
                Debug.LogWarning("[상승] PassengerManager: config is missing; no passenger candidates generated.");
                return;
            }

            if (_pool == null || _pool.Count == 0)
            {
                Debug.LogWarning("[상승] PassengerManager: passenger pool is empty; no passenger candidates generated.");
                return;
            }

            var available = new List<PassengerDefinition>();
            foreach (PassengerDefinition passenger in _pool)
                if (passenger != null) available.Add(passenger);

            if (available.Count == 0)
            {
                Debug.LogWarning("[상승] PassengerManager: passenger pool contains no valid definitions; no passenger candidates generated.");
                return;
            }

            int targetCount = Mathf.Min(Mathf.Max(0, _config.passengerCandidatesPerFloor), available.Count);
            for (int i = 0; i < targetCount; i++)
            {
                int selectedIndex = _rng.Next(available.Count);
                _candidates.Add(available[selectedIndex]);
                available.RemoveAt(selectedIndex);
            }

            Debug.Log($"[상승] Passenger candidates generated for floor {floorIndex}: {_candidates.Count}");
        }

        public bool CanBoard(PassengerDefinition passenger)
        {
            if (passenger == null || _config == null)
                return false;

            return _boarded.Count < Mathf.Max(0, _config.maxPassengerSlots);
        }

        /// <summary>Boards and removes the candidate at the given index.</summary>
        public bool Board(int candidateIndex)
        {
            if (candidateIndex < 0 || candidateIndex >= _candidates.Count)
            {
                Debug.LogWarning($"[상승] PassengerManager: candidate index {candidateIndex} is unavailable.");
                return false;
            }

            PassengerDefinition passenger = _candidates[candidateIndex];
            if (!CanBoard(passenger))
            {
                Debug.LogWarning($"[상승] PassengerManager: cannot board candidate {candidateIndex}; passenger slots are full or passenger/config is missing.");
                return false;
            }

            _boarded.Add(passenger);
            _candidates.RemoveAt(candidateIndex);
            RebuildActiveEffects();
            Debug.Log($"[상승] Passenger boarded: {(string.IsNullOrEmpty(passenger.displayName) ? passenger.id : passenger.displayName)}");
            return true;
        }

        private void RebuildActiveEffects()
        {
            _activeEffects.Clear();
            foreach (PassengerDefinition passenger in _boarded)
            {
                if (passenger == null || passenger.effects == null)
                    continue;

                foreach (EffectDefinition effect in passenger.effects)
                    if (effect != null) _activeEffects.Add(effect);
            }
        }
    }
}
