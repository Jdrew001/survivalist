using Assets.Game.Services.Interfaces;
using UnityEngine;

namespace Assets.Game.Managers
{
    public class StartState : GameState
    {

        public StartState(GameStateManager manager) : base(manager)
        {
        }

        public override void Enter()
        {
            Debug.Log("Entering START State");
            this._testingService.showMessage("Welcome to the Start State!");
        }

        public override void Update()
        {
            
        }

        public override void Exit()
        {
            Debug.Log("Exiting START State");
        }
    }
}