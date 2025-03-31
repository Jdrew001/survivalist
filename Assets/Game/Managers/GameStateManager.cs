using Assets.Game.Managers.States;
using UnityEngine;
using Zenject;

namespace Assets.Game.Managers
{
    public class GameStateManager : BaseManager
    {
        private GameState CurrentState;

        [Inject]
        public IGameStateFactory StateFactory;

        private void Start()
        {
            if (StateFactory == null)
            {
                Debug.LogError("StateFactory injection failed!");
                return;
            }
            Debug.Log("StateFactory successfully injected.");
            SetState(StateFactory.CreateStartState(this));
        }

        private void Update()
        {
            CurrentState?.Update();
        }

        // Switch to a new state, calling Exit on the old state and Enter on the new
        public void SetState(GameState newState)
        {
            CurrentState?.Exit();
            CurrentState = newState;
            CurrentState.Enter();
        }
    }
}
