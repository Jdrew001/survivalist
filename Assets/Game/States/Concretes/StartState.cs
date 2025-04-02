using Assets.Game.Services.Interfaces;
using System;
using UnityEngine;

namespace Assets.Game.Managers
{
    [Serializable]
    public class StartState : GameState
    {

        [SerializeField] private float difficultyLevel;
        public float DifficultyLevel => difficultyLevel;

        public StartState() { }

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