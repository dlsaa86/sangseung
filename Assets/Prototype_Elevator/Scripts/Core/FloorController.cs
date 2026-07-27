using UnityEngine;

namespace Ascend.Prototype
{
    /// <summary>
    /// Manages current floor number and required power computation.
    /// Config-driven — no hardcoded balance values.
    /// </summary>
    public class FloorController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private PrototypeConfig _config;

        private int   _currentFloor;
        private float _requiredPower;

        /// <summary>Current floor index (0-based).</summary>
        public int   CurrentFloor  => _currentFloor;

        /// <summary>Power required to ascend from the current floor.</summary>
        public float RequiredPower => _requiredPower;

        /// <summary>
        /// Sets the active floor and computes base required power (no overload applied).
        /// Call <see cref="UpdateRequiredPower"/> afterwards if overload state is known.
        /// </summary>
        public void EnterFloor(int floor)
        {
            _currentFloor  = floor;
            _requiredPower = FloorMath.ComputeRequiredPower(_config, floor, 0f, false);
        }

        /// <summary>
        /// Recomputes required power for the given floor, optionally applying the
        /// overload multiplier from config.
        /// </summary>
        public float ComputeRequiredPower(int floor, float totalWeight, bool isOverloaded = false)
        {
            return FloorMath.ComputeRequiredPower(_config, floor, totalWeight, isOverloaded);
        }

        /// <summary>Compatibility overload for callers that do not track load yet.</summary>
        public float ComputeRequiredPower(int floor, bool isOverloaded = false)
        {
            return ComputeRequiredPower(floor, 0f, isOverloaded);
        }

        /// <summary>
        /// Refreshes <see cref="RequiredPower"/> for the current floor with the given overload state.
        /// </summary>
        public void UpdateRequiredPower(float totalWeight, bool isOverloaded)
        {
            _requiredPower = ComputeRequiredPower(_currentFloor, totalWeight, isOverloaded);
        }

        /// <summary>Compatibility overload for callers that do not track load yet.</summary>
        public void UpdateRequiredPower(bool isOverloaded)
        {
            UpdateRequiredPower(0f, isOverloaded);
        }
    }
}
