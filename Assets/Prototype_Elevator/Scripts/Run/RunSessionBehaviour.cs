using UnityEngine;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.Run
{
    /// <summary>Thin Unity adapter; all run decisions live in RunSession.</summary>
    public sealed class RunSessionBehaviour : MonoBehaviour
    {
        [SerializeField] private int _seed = 1337;
        [SerializeField] private float _startingWeight;
        [SerializeField] private float _startingMoney;
        [SerializeField] private float _anteRatio = FloorSession.DefaultAnteRatio;
        [SerializeField] private float _anteEscalation = FloorSession.DefaultAnteEscalation;

        public RunSession Session { get; private set; }

        private void Awake()
        {
            ResetRun();
        }

        public void ResetRun()
        {
            Session = new RunSession(_seed, _startingWeight, _startingMoney,
                _anteRatio, _anteEscalation);
        }

        public bool SelectContract(int choiceIndex) => Session != null && Session.SelectContract(choiceIndex);
        public bool PushYourLuck() => Session != null && Session.PushYourLuck();
        public SpinResolution Spin() => Session == null ? default(SpinResolution) : Session.Spin();
        public FloorResult Bank() => Session == null ? null : Session.Bank();
        public FloorResult ForceResolve() => Session == null ? null : Session.ForceResolve();
    }
}
